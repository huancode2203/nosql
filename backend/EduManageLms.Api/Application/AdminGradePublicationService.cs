using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminGradePublicationService(MongoContext db)
{
    public async Task PublishAsync(
        string sectionId,
        string adminUserId,
        string reason,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppException("Phải nhập lý do hoặc ghi chú công bố.");

        var section = await db.ClassSections
            .Find(x => x.Id == sectionId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy lớp học phần.");

        if (section.GradeStatus != "Submitted")
        {
            throw new AppException(
                "Chỉ được công bố bảng điểm đang ở trạng thái Submitted.");
        }

        var studentIds = section.Students
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var students = await db.Students
            .Find(
                Builders<Student>.Filter.In(x => x.Id, studentIds)
                & Builders<Student>.Filter.Eq(x => x.IsDeleted, false))
            .ToListAsync(ct);

        if (students.Count != studentIds.Count)
            throw new AppException("Danh sách sinh viên của lớp học phần không đầy đủ.");

        var now = DateTime.UtcNow;
        var writes = new List<WriteModel<Student>>();

        foreach (var student in students)
        {
            var course = student.AcademicRecords
                .SelectMany(x => x.Semesters)
                .SelectMany(x => x.Courses)
                .FirstOrDefault(x => x.ClassSectionId == section.Id)
                ?? throw new AppException(
                    $"Sinh viên {student.StudentCode} thiếu hồ sơ môn học.");

            var scores = section.GradingSchemeSnapshot.Components.ToDictionary(
                x => x.ComponentId,
                x => course.Scores
                    .FirstOrDefault(s => s.ComponentId == x.ComponentId)
                    ?.Score);

            GradePolicy.ValidateForPublish(
                section.GradingSchemeSnapshot,
                scores);

            if (course.Scores.Any(x => x.RequiresConfirmation))
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
}
