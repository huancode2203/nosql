using System.Globalization;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class GradebookService(
    MongoContext db,
    ScoreNormalizationService normalizer) : IGradebookService
{
    public async Task<GradebookDto> GetAsync(
        string lecturerCode,
        string? sectionId,
        CancellationToken ct)
    {
        var section = await FindSectionAsync(lecturerCode, sectionId, ct)
                      ?? throw new NotFoundException("Không tìm thấy lớp học phần được phân công");

        var courseConfig = await db.Courses
            .Find(x => x.Id == section.CourseId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        var studentIds = section.Students
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var students = await db.Students
            .Find(
                Builders<Student>.Filter.In(x => x.Id, studentIds)
                & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        var rows = students
            .OrderBy(x => x.StudentCode)
            .Select(student =>
            {
                var course = FindCourseRecord(student, section.Id);

                var displayScores = section.GradingSchemeSnapshot.Components.ToDictionary(
                    component => component.ComponentId,
                    component =>
                    {
                        var score = course?.Scores
                            .FirstOrDefault(x => x.ComponentId == component.ComponentId);

                        if (score is null) return null;
                        if (!string.IsNullOrWhiteSpace(score.RawInput)) return score.RawInput;

                        return score.Score?.ToString(
                            "0.################",
                            CultureInfo.InvariantCulture);
                    });

                var numericScores = section.GradingSchemeSnapshot.Components.ToDictionary(
                    component => component.ComponentId,
                    component => course?.Scores
                        .FirstOrDefault(x => x.ComponentId == component.ComponentId)
                        ?.Score);

                var evaluation = GradePolicy.Evaluate(
                    section.GradingSchemeSnapshot,
                    courseConfig?.GradeScale,
                    numericScores);

                return new GradebookStudentDto(
                    student.Id,
                    student.StudentCode,
                    student.FullName,
                    displayScores,
                    evaluation.FinalScore,
                    evaluation.LetterGrade,
                    evaluation.Passed,
                    course?.Version ?? 0);
            })
            .ToList();

        return new GradebookDto(
            section.Id,
            section.ClassSectionCode,
            section.CourseName,
            section.GradeStatus,
            section.GradingSchemeSnapshot.Components
                .Select(x => new GradebookComponentDto(
                    x.ComponentId,
                    x.Name,
                    x.Weight,
                    x.MaxScore))
                .ToList(),
            rows);
    }

    public async Task UpdateAsync(
        string lecturerCode,
        string sectionId,
        GradeUpdateRequest request,
        string userId,
        CancellationToken ct)
    {
        if (request.Students.Count == 0)
            throw new AppException("Bảng điểm không có dữ liệu để lưu.");

        if (request.Students.GroupBy(x => x.StudentId).Any(x => x.Count() > 1))
            throw new AppException("Danh sách điểm chứa sinh viên bị trùng.");

        var section = await db.ClassSections
            .Find(x =>
                x.Id == sectionId
                && x.LecturerCode == lecturerCode
                && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần được phân công.");

        if (section.GradeStatus is "Locked" or "Published")
            throw new ForbiddenException(
                "Bảng điểm đã khóa hoặc đã công bố; không thể sửa trực tiếp.");

        if (section.GradeStatus == "Submitted")
            throw new ForbiddenException(
                "Bảng điểm đã gửi duyệt; cần được trả lại Draft trước khi sửa.");

        await EnsureGradeWindowAsync(section, ct);

        var enrolledIds = section.Students
            .Select(x => x.StudentId)
            .Distinct()
            .ToHashSet(StringComparer.Ordinal);

        if (request.Students.Any(x => !enrolledIds.Contains(x.StudentId)))
            throw new ForbiddenException("Có sinh viên không thuộc lớp học phần.");

        if (request.Publish
            && request.Students.Select(x => x.StudentId).Distinct().Count()
               != enrolledIds.Count)
        {
            throw new AppException(
                "Khi gửi duyệt phải gửi đầy đủ bảng điểm của toàn bộ sinh viên trong lớp.");
        }

        var requestedIds = request.Students
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var students = await db.Students
            .Find(
                Builders<Student>.Filter.In(x => x.Id, requestedIds)
                & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        if (students.Count != requestedIds.Count)
            throw new NotFoundException("Có sinh viên không tồn tại.");

        var rowsById = request.Students.ToDictionary(
            x => x.StudentId,
            StringComparer.Ordinal);

        var writeModels = new List<WriteModel<Student>>();
        var now = DateTime.UtcNow;

        foreach (var student in students)
        {
            var row = rowsById[student.Id];
            var course = FindCourseRecord(student, section.Id)
                         ?? throw new NotFoundException(
                             $"Không có hồ sơ môn học của sinh viên {student.StudentCode}.");

            if (row.Version.HasValue && row.Version.Value != course.Version)
            {
                throw new ConflictException(
                    $"Dữ liệu của sinh viên {student.StudentCode} đã được người khác thay đổi. Hãy tải lại bảng điểm.");
            }

            ValidateScoreKeys(section.GradingSchemeSnapshot, row.Scores);
            ApplyScores(
                section.GradingSchemeSnapshot,
                course,
                row,
                userId,
                now);

            var scoreMap = section.GradingSchemeSnapshot.Components.ToDictionary(
                x => x.ComponentId,
                x => course.Scores
                    .FirstOrDefault(s => s.ComponentId == x.ComponentId)
                    ?.Score);

            if (request.Publish)
            {
                GradePolicy.ValidateForPublish(
                    section.GradingSchemeSnapshot,
                    scoreMap);

                if (course.Scores.Any(x => x.RequiresConfirmation))
                {
                    throw new AppException(
                        $"Sinh viên {student.StudentCode} còn điểm chuẩn hóa cần xác nhận.");
                }
            }

            course.ScoreStatus = request.Publish ? "Submitted" : "Draft";
            course.PublishedAt = null;
            course.Version++;
            student.UpdatedAt = now;

            writeModels.Add(
                new ReplaceOneModel<Student>(
                    Builders<Student>.Filter.Eq(x => x.Id, student.Id),
                    student));
        }

        if (writeModels.Count > 0)
        {
            await db.Students.BulkWriteAsync(
                writeModels,
                new BulkWriteOptions { IsOrdered = false },
                ct);
        }

        var previousStatus = section.GradeStatus;
        section.GradeStatus = request.Publish ? "Submitted" : "Draft";
        section.UpdatedAt = now;

        await db.ClassSections.ReplaceOneAsync(
            x => x.Id == section.Id,
            section,
            cancellationToken: ct);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = userId,
                UserName = lecturerCode,
                Role = "Lecturer",
                Action = request.Publish ? "GRADE_SUBMIT" : "GRADE_UPDATE",
                Entity = "ClassSection",
                EntityId = section.Id,
                Before = new { GradeStatus = previousStatus },
                After = new
                {
                    GradeStatus = section.GradeStatus,
                    StudentCount = request.Students.Count
                },
                Note = request.Publish
                    ? "Giảng viên gửi bảng điểm để quản trị viên kiểm tra."
                    : "Lưu bản nháp.",
                Result = "Success"
            },
            cancellationToken: ct);
    }

    private void ApplyScores(
        GradingSchemeVersion scheme,
        StudentCourseRecord course,
        GradeUpdateStudent row,
        string userId,
        DateTime now)
    {
        foreach (var definition in scheme.Components)
        {
            if (!row.Scores.TryGetValue(
                    definition.ComponentId,
                    out var rawValue))
            {
                continue;
            }

            var result = normalizer.Normalize(
                rawValue,
                (decimal)definition.MaxScore);

            var confirmed = row.ConfirmedComponents?.Contains(
                definition.ComponentId,
                StringComparer.OrdinalIgnoreCase) == true;

            if (result.RequiresConfirmation && !confirmed)
            {
                throw new AppException(
                    result.Warning
                    ?? $"Điểm {definition.Name} cần được xác nhận.");
            }

            var score = course.Scores.FirstOrDefault(
                x => x.ComponentId == definition.ComponentId);

            if (score is null)
            {
                score = new ScoreComponent
                {
                    ComponentId = definition.ComponentId,
                    ComponentName = definition.Name,
                    Type = definition.Type,
                    Weight = definition.Weight,
                    MaxScore = definition.MaxScore,
                    IsRequired = definition.IsRequired,
                    MinimumScore = definition.MinimumScore,
                    CloMappings = definition.CloMappings
                };

                course.Scores.Add(score);
            }

            score.RawInput = result.RawInput;
            score.Score = result.NormalizedValue.HasValue
                ? (double)result.NormalizedValue.Value
                : null;
            score.NormalizationType = result.NormalizationType;
            score.RequiresConfirmation =
                result.RequiresConfirmation && !confirmed;
            score.Status = result.NormalizedValue.HasValue
                ? "Graded"
                : result.NormalizationType == "Empty"
                    ? "NotGraded"
                    : result.NormalizationType;
            score.EnteredBy = userId;
            score.EnteredAt ??= now;
            score.UpdatedAt = now;
        }
    }

    private static void ValidateScoreKeys(
        GradingSchemeVersion scheme,
        IReadOnlyDictionary<string, string?> scores)
    {
        var validKeys = scheme.Components
            .Select(x => x.ComponentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalid = scores.Keys
            .FirstOrDefault(x => !validKeys.Contains(x));

        if (invalid is not null)
            throw new AppException($"Cột điểm {invalid} không hợp lệ.");
    }

    private async Task<ClassSection?> FindSectionAsync(
        string lecturerCode,
        string? sectionId,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(sectionId)
            && sectionId != "default")
        {
            return await db.ClassSections
                .Find(x =>
                    x.Id == sectionId
                    && x.LecturerCode == lecturerCode
                    && !x.IsDeleted)
                .FirstOrDefaultAsync(ct);
        }

        var sections = await db.ClassSections
            .Find(x => x.LecturerCode == lecturerCode && !x.IsDeleted)
            .SortByDescending(x => x.StartDate)
            .ToListAsync(ct);

        return sections.FirstOrDefault(
                   x => x.GradeStatus is "Draft" or "Reopened")
               ?? sections.FirstOrDefault();
    }

    private async Task EnsureGradeWindowAsync(
        ClassSection section,
        CancellationToken ct)
    {
        var enforceWindow = await db.SystemSettings
            .Find(x => x.Key == "grade.enforceWindow" && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (enforceWindow?.Value is not bool enabled
            || !enabled
            || section.GradeStatus == "Reopened")
        {
            return;
        }

        var semester = await db.Semesters
            .Find(x => x.Id == section.SemesterId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (semester is not null
            && (DateTime.UtcNow < semester.GradeEntryStart
                || DateTime.UtcNow > semester.GradeEntryEnd))
        {
            throw new ForbiddenException(
                "Ngoài thời gian nhập điểm được quy định.");
        }
    }

    private static StudentCourseRecord? FindCourseRecord(
        Student student,
        string classSectionId) =>
        student.AcademicRecords
            .SelectMany(x => x.Semesters)
            .SelectMany(x => x.Courses)
            .FirstOrDefault(x => x.ClassSectionId == classSectionId);
}
