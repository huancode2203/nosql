using System.Globalization;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed record AdminGradebookSummaryDto(
    string ClassSectionId,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    string LecturerCode,
    string LecturerName,
    string AcademicYearName,
    string SemesterName,
    string Status,
    int StudentCount,
    int CompletedStudentCount,
    int InvalidStudentCount,
    int ConfirmationWarningCount,
    bool ReadyToPublish,
    DateTime UpdatedAt);

public sealed record AdminGradebookComponentDto(
    string ComponentId,
    string ComponentName,
    double Weight,
    double MaxScore,
    bool Required,
    double? MinimumScore);

public sealed record AdminGradebookStudentDto(
    string StudentId,
    string StudentCode,
    string FullName,
    IReadOnlyDictionary<string, double?> Scores,
    double FinalScore,
    string LetterGrade,
    bool Passed,
    bool RequiresConfirmation,
    IReadOnlyCollection<string> ValidationMessages);

public sealed record AdminGradebookDetailDto(
    string ClassSectionId,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    string LecturerCode,
    string LecturerName,
    string AcademicYearName,
    string SemesterCode,
    string SemesterName,
    string Status,
    DateTime UpdatedAt,
    IReadOnlyCollection<AdminGradebookComponentDto> Components,
    IReadOnlyCollection<AdminGradebookStudentDto> Students,
    int CompletedStudentCount,
    int InvalidStudentCount,
    int ConfirmationWarningCount,
    bool ReadyToPublish);

public sealed class AdminGradePublicationService(MongoContext db)
{
    public async Task<PagedResult<AdminGradebookSummaryDto>> ListAsync(
        string? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken ct)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var filter = Builders<ClassSection>.Filter.Eq(x => x.IsDeleted, false);
        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? "Submitted"
            : status.Trim();

        if (!normalizedStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            filter &= Builders<ClassSection>.Filter.Eq(
                x => x.GradeStatus,
                normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(search.Trim()),
                "i");

            filter &= Builders<ClassSection>.Filter.Or(
                Builders<ClassSection>.Filter.Regex(x => x.ClassSectionCode, regex),
                Builders<ClassSection>.Filter.Regex(x => x.CourseCode, regex),
                Builders<ClassSection>.Filter.Regex(x => x.CourseName, regex),
                Builders<ClassSection>.Filter.Regex(x => x.LecturerCode, regex),
                Builders<ClassSection>.Filter.Regex(x => x.LecturerName, regex));
        }

        var total = await db.ClassSections.CountDocumentsAsync(
            filter,
            cancellationToken: ct);

        var sections = await db.ClassSections
            .Find(filter)
            .SortByDescending(x => x.UpdatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        var summaries = new List<AdminGradebookSummaryDto>(sections.Count);

        foreach (var section in sections)
        {
            var detail = await BuildDetailAsync(section, ct);

            summaries.Add(new AdminGradebookSummaryDto(
                detail.ClassSectionId,
                detail.ClassSectionCode,
                detail.CourseCode,
                detail.CourseName,
                detail.LecturerCode,
                detail.LecturerName,
                detail.AcademicYearName,
                detail.SemesterName,
                detail.Status,
                detail.Students.Count,
                detail.CompletedStudentCount,
                detail.InvalidStudentCount,
                detail.ConfirmationWarningCount,
                detail.ReadyToPublish,
                detail.UpdatedAt));
        }

        return PagedResult<AdminGradebookSummaryDto>.Create(
            summaries,
            pageNumber,
            pageSize,
            total);
    }

    public async Task<AdminGradebookDetailDto> GetAsync(
        string sectionId,
        CancellationToken ct)
    {
        var section = await db.ClassSections
            .Find(x => x.Id == sectionId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần.");

        return await BuildDetailAsync(section, ct);
    }

    public async Task ReturnToDraftAsync(
        string sectionId,
        string adminUserId,
        string reason,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppException(
                "Phải nhập lý do trả lại bảng điểm cho giảng viên.");
        }

        var section = await db.ClassSections
            .Find(x => x.Id == sectionId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần.");

        if (section.GradeStatus != "Submitted")
        {
            throw new AppException(
                "Chỉ được trả lại bảng điểm đang ở trạng thái Submitted.");
        }

        var students = await LoadStudentsAsync(section, ct);
        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<Student>>();

        foreach (var student in students)
        {
            var course = FindCourseRecord(student, section.Id);
            if (course is null)
            {
                continue;
            }

            course.ScoreStatus = "Draft";
            course.PublishedAt = null;
            course.Version++;
            student.UpdatedAt = now;

            writes.Add(
                new ReplaceOneModel<Student>(
                    Builders<Student>.Filter.Eq(x => x.Id, student.Id),
                    student));
        }

        if (writes.Count > 0)
        {
            await db.Students.BulkWriteAsync(
                writes,
                new BulkWriteOptions { IsOrdered = false },
                ct);
        }

        var previousStatus = section.GradeStatus;
        section.GradeStatus = "Draft";
        section.UpdatedAt = now;

        await db.ClassSections.ReplaceOneAsync(
            x => x.Id == section.Id,
            section,
            cancellationToken: ct);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = adminUserId,
                Role = "Admin",
                Action = "GRADE_RETURN",
                Entity = "ClassSection",
                EntityId = section.Id,
                Before = new { GradeStatus = previousStatus },
                After = new { GradeStatus = section.GradeStatus },
                Note = reason,
                Result = "Success"
            },
            cancellationToken: ct);
    }

    public async Task PublishAsync(
        string sectionId,
        string adminUserId,
        string reason,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppException(
                "Phải nhập lý do hoặc ghi chú công bố.");
        }

        var section = await db.ClassSections
            .Find(x => x.Id == sectionId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần.");

        if (section.GradeStatus != "Submitted")
        {
            throw new AppException(
                "Chỉ được công bố bảng điểm đang ở trạng thái Submitted.");
        }

        var detail = await BuildDetailAsync(section, ct);
        if (!detail.ReadyToPublish)
        {
            throw new AppException(
                $"Bảng điểm chưa hợp lệ: {detail.InvalidStudentCount} sinh viên có lỗi, "
                + $"{detail.ConfirmationWarningCount} trường hợp cần xác nhận.");
        }

        var students = await LoadStudentsAsync(section, ct);
        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<Student>>();

        foreach (var student in students)
        {
            var course = FindCourseRecord(student, section.Id)
                ?? throw new AppException(
                    $"Sinh viên {student.StudentCode} thiếu hồ sơ môn học.");

            var scores = section.GradingSchemeSnapshot.Components.ToDictionary(
                component => component.ComponentId,
                component => course.Scores
                    .FirstOrDefault(score =>
                        score.ComponentId == component.ComponentId)
                    ?.Score);

            GradePolicy.ValidateForPublish(
                section.GradingSchemeSnapshot,
                scores);

            if (course.Scores.Any(score => score.RequiresConfirmation))
            {
                throw new AppException(
                    $"Sinh viên {student.StudentCode} còn điểm cần xác nhận.");
            }

            course.ScoreStatus = "Published";
            course.PublishedAt = now;
            course.Version++;
            student.UpdatedAt = now;

            writes.Add(
                new ReplaceOneModel<Student>(
                    Builders<Student>.Filter.Eq(x => x.Id, student.Id),
                    student));
        }

        if (writes.Count > 0)
        {
            await db.Students.BulkWriteAsync(
                writes,
                new BulkWriteOptions { IsOrdered = false },
                ct);
        }

        var previousStatus = section.GradeStatus;
        section.GradeStatus = "Published";
        section.UpdatedAt = now;

        await db.ClassSections.ReplaceOneAsync(
            x => x.Id == section.Id,
            section,
            cancellationToken: ct);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = adminUserId,
                Role = "Admin",
                Action = "GRADE_PUBLISH",
                Entity = "ClassSection",
                EntityId = section.Id,
                Before = new { GradeStatus = previousStatus },
                After = new
                {
                    GradeStatus = section.GradeStatus,
                    PublishedAt = now
                },
                Note = reason,
                Result = "Success"
            },
            cancellationToken: ct);
    }

    private async Task<AdminGradebookDetailDto> BuildDetailAsync(
        ClassSection section,
        CancellationToken ct)
    {
        var courseConfig = await db.Courses
            .Find(x => x.Id == section.CourseId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        var students = await LoadStudentsAsync(section, ct);
        var rows = new List<AdminGradebookStudentDto>();
        var completedCount = 0;
        var invalidCount = 0;
        var confirmationCount = 0;

        foreach (var student in students.OrderBy(x => x.StudentCode))
        {
            var course = FindCourseRecord(student, section.Id);
            var messages = new List<string>();
            var scoreMap = new Dictionary<string, double?>();

            foreach (var component in section.GradingSchemeSnapshot.Components)
            {
                var score = course?.Scores.FirstOrDefault(
                    item => item.ComponentId == component.ComponentId);

                scoreMap[component.ComponentId] = score?.Score;

                if (component.IsRequired && score?.Score is null)
                {
                    messages.Add($"Thiếu điểm {component.Name}.");
                }

                if (score?.Score is not null
                    && (score.Score < 0 || score.Score > component.MaxScore))
                {
                    messages.Add(
                        $"{component.Name} ngoài khoảng 0–{component.MaxScore:0.##}.");
                }

                if (score?.RequiresConfirmation == true)
                {
                    messages.Add(
                        $"{component.Name} còn yêu cầu xác nhận chuẩn hóa.");
                }
            }

            if (course is null)
            {
                messages.Add("Thiếu hồ sơ môn học trong kết quả sinh viên.");
            }
            else
            {
                try
                {
                    GradePolicy.ValidateForPublish(
                        section.GradingSchemeSnapshot,
                        scoreMap);
                }
                catch (AppException exception)
                {
                    messages.Add(exception.Message);
                }
            }

            var evaluation = GradePolicy.Evaluate(
                section.GradingSchemeSnapshot,
                courseConfig?.GradeScale,
                scoreMap);

            var requiresConfirmation =
                course?.Scores.Any(score => score.RequiresConfirmation) == true;

            if (requiresConfirmation)
            {
                confirmationCount++;
            }

            var normalizedMessages = messages
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedMessages.Count == 0)
            {
                completedCount++;
            }
            else
            {
                invalidCount++;
            }

            rows.Add(new AdminGradebookStudentDto(
                student.Id,
                student.StudentCode,
                student.FullName,
                scoreMap,
                evaluation.FinalScore,
                evaluation.LetterGrade,
                evaluation.Passed,
                requiresConfirmation,
                normalizedMessages));
        }

        var missingStudentCount = Math.Max(
            0,
            section.Students
                .Select(item => item.StudentId)
                .Distinct()
                .Count()
            - students.Count);

        invalidCount += missingStudentCount;

        return new AdminGradebookDetailDto(
            section.Id,
            section.ClassSectionCode,
            section.CourseCode,
            section.CourseName,
            section.LecturerCode,
            section.LecturerName,
            section.AcademicYearName,
            section.SemesterCode,
            section.SemesterName,
            section.GradeStatus,
            section.UpdatedAt,
            section.GradingSchemeSnapshot.Components
                .Select(component => new AdminGradebookComponentDto(
                    component.ComponentId,
                    component.Name,
                    component.Weight,
                    component.MaxScore,
                    component.IsRequired,
                    component.MinimumScore))
                .ToList(),
            rows,
            completedCount,
            invalidCount,
            confirmationCount,
            section.GradeStatus == "Submitted"
                && invalidCount == 0
                && confirmationCount == 0
                && rows.Count > 0);
    }

    private async Task<List<Student>> LoadStudentsAsync(
        ClassSection section,
        CancellationToken ct)
    {
        var studentIds = section.Students
            .Select(item => item.StudentId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (studentIds.Count == 0)
        {
            return [];
        }

        return await db.Students
            .Find(
                Builders<Student>.Filter.In(x => x.Id, studentIds)
                & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);
    }

    private static StudentCourseRecord? FindCourseRecord(
        Student student,
        string classSectionId) =>
        student.AcademicRecords
            .SelectMany(record => record.Semesters)
            .SelectMany(semester => semester.Courses)
            .FirstOrDefault(course =>
                course.ClassSectionId == classSectionId);
}
