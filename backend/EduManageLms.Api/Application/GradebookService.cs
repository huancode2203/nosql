using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class GradebookService(MongoContext db) : IGradebookService
{
    public async Task<GradebookDto> GetAsync(string lecturerCode, string? sectionId, CancellationToken ct)
    {
        var section = await FindSectionAsync(lecturerCode, sectionId, ct)
                      ?? throw new NotFoundException("Không tìm thấy lớp học phần được phân công");
        var courseConfig = await db.Courses.Find(x => x.Id == section.CourseId && !x.IsDeleted).FirstOrDefaultAsync(ct);

        var studentIds = section.Students.Select(x => x.StudentId).Distinct().ToList();
        var students = await db.Students
            .Find(Builders<Student>.Filter.In(x => x.Id, studentIds) & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        var rows = students.OrderBy(x => x.StudentCode).Select(student =>
        {
            var course = FindCourseRecord(student, section.Id);
            var scores = section.GradingSchemeSnapshot.Components.ToDictionary(
                component => component.ComponentId,
                component => course?.Scores.FirstOrDefault(score => score.ComponentId == component.ComponentId)?.Score);
            var evaluation = GradePolicy.Evaluate(section.GradingSchemeSnapshot, courseConfig?.GradeScale, scores);
            return new GradebookStudentDto(
                student.Id,
                student.StudentCode,
                student.FullName,
                scores,
                evaluation.FinalScore,
                evaluation.LetterGrade,
                evaluation.Passed);
        }).ToList();

        return new GradebookDto(
            section.Id,
            section.ClassSectionCode,
            section.CourseName,
            section.GradeStatus,
            section.GradingSchemeSnapshot.Components
                .Select(x => new GradebookComponentDto(x.ComponentId, x.Name, x.Weight, x.MaxScore))
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
        if (request.Students.Count == 0) throw new AppException("Bảng điểm không có dữ liệu để lưu");
        if (request.Students.GroupBy(x => x.StudentId).Any(x => x.Count() > 1))
            throw new AppException("Danh sách điểm chứa sinh viên bị trùng");

        var section = await db.ClassSections
            .Find(x => x.Id == sectionId && x.LecturerCode == lecturerCode && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần được phân công");

        if (section.GradeStatus == "Locked") throw new ForbiddenException("Bảng điểm đã khóa");
        if (section.GradeStatus == "Published") throw new ForbiddenException("Bảng điểm đã công bố; cần được Admin mở lại");
        await EnsureGradeWindowAsync(section, ct);

        var enrolledIds = section.Students.Select(x => x.StudentId).Distinct().ToHashSet(StringComparer.Ordinal);
        if (request.Students.Any(x => !enrolledIds.Contains(x.StudentId)))
            throw new ForbiddenException("Có sinh viên không thuộc lớp học phần");
        if (request.Publish && request.Students.Select(x => x.StudentId).Distinct().Count() != enrolledIds.Count)
            throw new AppException("Khi công bố phải gửi đầy đủ bảng điểm của toàn bộ sinh viên trong lớp");

        var requestedIds = request.Students.Select(x => x.StudentId).Distinct().ToList();
        var students = await db.Students
            .Find(Builders<Student>.Filter.In(x => x.Id, requestedIds) & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);
        if (students.Count != requestedIds.Count) throw new NotFoundException("Có sinh viên không tồn tại");

        var rowsById = request.Students.ToDictionary(x => x.StudentId, StringComparer.Ordinal);
        var writeModels = new List<WriteModel<Student>>();
        var now = DateTime.UtcNow;

        foreach (var student in students)
        {
            var row = rowsById[student.Id];
            var course = FindCourseRecord(student, section.Id)
                         ?? throw new NotFoundException($"Không có hồ sơ môn học của sinh viên {student.StudentCode}");

            ValidateScoreKeys(section.GradingSchemeSnapshot, row.Scores);
            ApplyScores(section.GradingSchemeSnapshot, course, row.Scores);
            var scoreMap = section.GradingSchemeSnapshot.Components.ToDictionary(
                x => x.ComponentId,
                x => course.Scores.FirstOrDefault(s => s.ComponentId == x.ComponentId)?.Score);

            if (request.Publish) GradePolicy.ValidateForPublish(section.GradingSchemeSnapshot, scoreMap);
            course.ScoreStatus = request.Publish ? "Published" : "InProgress";
            course.PublishedAt = request.Publish ? now : null;
            student.UpdatedAt = now;
            writeModels.Add(new ReplaceOneModel<Student>(Builders<Student>.Filter.Eq(x => x.Id, student.Id), student));
        }

        if (writeModels.Count > 0)
            await db.Students.BulkWriteAsync(writeModels, new BulkWriteOptions { IsOrdered = false }, ct);

        var previousStatus = section.GradeStatus;
        section.GradeStatus = request.Publish ? "Published" : "InProgress";
        section.UpdatedAt = now;
        await db.ClassSections.ReplaceOneAsync(x => x.Id == section.Id, section, cancellationToken: ct);
        await db.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            UserName = lecturerCode,
            Role = "Lecturer",
            Action = request.Publish ? "PublishGrades" : "SaveGrades",
            Entity = "ClassSection",
            EntityId = section.Id,
            Before = new { GradeStatus = previousStatus },
            After = new { GradeStatus = section.GradeStatus, StudentCount = request.Students.Count },
            Result = "Success"
        }, cancellationToken: ct);
    }

    private async Task<ClassSection?> FindSectionAsync(string lecturerCode, string? sectionId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(sectionId) && sectionId != "default")
        {
            return await db.ClassSections
                .Find(x => x.Id == sectionId && x.LecturerCode == lecturerCode && !x.IsDeleted)
                .FirstOrDefaultAsync(ct);
        }

        var sections = await db.ClassSections
            .Find(x => x.LecturerCode == lecturerCode && !x.IsDeleted)
            .SortByDescending(x => x.StartDate)
            .ToListAsync(ct);
        return sections.FirstOrDefault(x => x.GradeStatus is "InProgress" or "Draft" or "Reopened")
               ?? sections.FirstOrDefault();
    }

    private async Task EnsureGradeWindowAsync(ClassSection section, CancellationToken ct)
    {
        var enforceWindow = await db.SystemSettings
            .Find(x => x.Key == "grade.enforceWindow" && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (enforceWindow?.Value is not bool enabled || !enabled || section.GradeStatus == "Reopened") return;

        var semester = await db.Semesters.Find(x => x.Id == section.SemesterId && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (semester is not null && (DateTime.UtcNow < semester.GradeEntryStart || DateTime.UtcNow > semester.GradeEntryEnd))
            throw new ForbiddenException("Ngoài thời gian nhập điểm được quy định");
    }

    private static StudentCourseRecord? FindCourseRecord(Student student, string classSectionId) =>
        student.AcademicRecords.SelectMany(x => x.Semesters).SelectMany(x => x.Courses)
            .FirstOrDefault(x => x.ClassSectionId == classSectionId);

    private static void ValidateScoreKeys(GradingSchemeVersion scheme, IReadOnlyDictionary<string, double?> scores)
    {
        var validKeys = scheme.Components.Select(x => x.ComponentId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = scores.Keys.FirstOrDefault(x => !validKeys.Contains(x));
        if (invalid is not null) throw new AppException($"Cột điểm {invalid} không hợp lệ");

        foreach (var entry in scores)
        {
            var definition = scheme.Components.First(x => x.ComponentId.Equals(entry.Key, StringComparison.OrdinalIgnoreCase));
            if (entry.Value is < 0 || entry.Value > definition.MaxScore)
                throw new AppException($"Điểm {definition.Name} phải từ 0 đến {definition.MaxScore}");
        }
    }

    private static void ApplyScores(
        GradingSchemeVersion scheme,
        StudentCourseRecord course,
        IReadOnlyDictionary<string, double?> values)
    {
        foreach (var definition in scheme.Components)
        {
            if (!values.TryGetValue(definition.ComponentId, out var value)) continue;
            var score = course.Scores.FirstOrDefault(x => x.ComponentId == definition.ComponentId);
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

            score.Score = value;
            score.Status = value.HasValue ? "Graded" : "NotGraded";
        }
    }
}
