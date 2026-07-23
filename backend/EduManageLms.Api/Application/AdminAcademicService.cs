using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminAcademicService(MongoContext db) : IAdminAcademicService
{
    public async Task<CourseDesignDto> GetCourseDesignAsync(string courseId, CancellationToken ct)
    {
        var course = await db.Courses.Find(x => x.Id == courseId && !x.IsDeleted).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Không tìm thấy môn học");
        return Map(course);
    }

    public async Task<CourseDesignDto> SaveCourseDesignAsync(
        string courseId,
        SaveCourseDesignRequest request,
        string userId,
        CancellationToken ct)
    {
        var course = await db.Courses.Find(x => x.Id == courseId && !x.IsDeleted).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Không tìm thấy môn học");

        ValidateDesign(request);
        var nextVersion = course.GradingSchemes.Count == 0 ? 1 : course.GradingSchemes.Max(x => x.Version) + 1;
        var scheme = request.Scheme;
        scheme.Version = nextVersion;
        scheme.EffectiveFrom = DateTime.UtcNow;
        scheme.Active = true;
        foreach (var old in course.GradingSchemes) old.Active = false;
        course.GradingSchemes.Add(scheme);
        course.Clos = request.Clos.ToList();
        course.UpdatedAt = DateTime.UtcNow;

        await db.Courses.ReplaceOneAsync(x => x.Id == course.Id, course, cancellationToken: ct);
        await db.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            Role = "Admin",
            Action = "CreateGradingSchemeVersion",
            Entity = "Course",
            EntityId = course.Id,
            After = new { scheme.Version, Components = scheme.Components.Count, Clos = course.Clos.Count }
        }, cancellationToken: ct);

        return Map(course);
    }

    public async Task<PagedResult<Dictionary<string, object?>>> GetAuditLogsAsync(
        string? search,
        int page,
        int size,
        CancellationToken ct)
    {
        var filter = Builders<AuditLog>.Filter.Eq(x => x.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(search, "i");
            filter &= Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Regex(x => x.UserName, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Action, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Entity, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Role, regex));
        }

        var total = await db.AuditLogs.CountDocumentsAsync(filter, cancellationToken: ct);
        var items = await db.AuditLogs.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(ct);

        var mapped = items.Select(x => new Dictionary<string, object?>
        {
            ["id"] = x.Id,
            ["userName"] = x.UserName,
            ["role"] = x.Role,
            ["action"] = x.Action,
            ["entity"] = x.Entity,
            ["entityId"] = x.EntityId,
            ["result"] = x.Result,
            ["ipAddress"] = x.IpAddress,
            ["createdAt"] = x.CreatedAt,
            ["note"] = x.Note
        }).ToList();

        return PagedResult<Dictionary<string, object?>>.Create(mapped, page, size, total);
    }

    public async Task<AdminReportDto> GetReportsAsync(CancellationToken ct)
    {
        var students = await db.Students.Find(x => !x.IsDeleted).ToListAsync(ct);
        var sections = await db.ClassSections.Find(x => !x.IsDeleted).ToListAsync(ct);
        var activities = await db.AuditLogs.Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .Limit(10)
            .ToListAsync(ct);

        var cards = new List<DashboardCardDto>
        {
            new("Sinh viên", students.Count, "school", null, "primary"),
            new("Lớp học phần", sections.Count, "class", null, "primary"),
            new("Bảng điểm đã công bố", sections.Count(x => x.GradeStatus == "Published"), "publish", null, "success"),
            new("Yêu cầu mở điểm", await db.GradeReopenRequests.CountDocumentsAsync(x => x.Status == "Pending" && !x.IsDeleted, cancellationToken: ct), "lock_open", null, "warning")
        };

        var studentsByFaculty = students
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Faculty.FacultyName) ? "Chưa xác định" : x.Faculty.FacultyName)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var gradeStatus = sections
            .GroupBy(x => x.GradeStatus)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .ToList();

        var learningStatus = students
            .GroupBy(x => x.Status)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .ToList();

        var cloPipeline = new[]
        {
            BsonDocument.Parse("{ $match: { isDeleted: false } }"),
            BsonDocument.Parse("{ $unwind: '$academicRecords' }"),
            BsonDocument.Parse("{ $unwind: '$academicRecords.semesters' }"),
            BsonDocument.Parse("{ $unwind: '$academicRecords.semesters.courses' }"),
            BsonDocument.Parse("{ $unwind: '$academicRecords.semesters.courses.scores' }"),
            BsonDocument.Parse("{ $unwind: '$academicRecords.semesters.courses.scores.cloMappings' }"),
            BsonDocument.Parse("{ $group: { _id: '$academicRecords.semesters.courses.scores.cloMappings.cloCode', avg: { $avg: { $multiply: [ { $divide: [ { $ifNull: ['$academicRecords.semesters.courses.scores.score', 0] }, '$academicRecords.semesters.courses.scores.maxScore' ] }, 100 ] } } } }"),
            BsonDocument.Parse("{ $project: { _id: 0, label: '$_id', value: { $round: ['$avg', 2] } } }"),
            BsonDocument.Parse("{ $sort: { label: 1 } }"),
        };
        var cloDocs = await db.Students.Aggregate<BsonDocument>(cloPipeline).ToListAsync(ct);
        var cloAchievement = cloDocs.Select(x => new ChartItemDto(x["label"].AsString, x["value"].ToDouble())).ToList();

        return new AdminReportDto(
            cards,
            studentsByFaculty,
            gradeStatus,
            learningStatus,
            cloAchievement,
            activities.Select(x => new ActivityDto(x.Action, $"{x.Entity} - {x.UserName}", x.CreatedAt.ToString("dd/MM HH:mm"), "history")).ToList());
    }

    public async Task<Dictionary<string, object?>> ReviewReopenRequestAsync(
        string id,
        bool approve,
        string note,
        string userId,
        CancellationToken ct)
    {
        var request = await db.GradeReopenRequests.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy yêu cầu");
        if (request.Status != "Pending") throw new AppException("Yêu cầu đã được xử lý");

        request.Status = approve ? "Approved" : "Rejected";
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedBy = userId;
        request.ReviewNote = note;
        request.UpdatedAt = DateTime.UtcNow;
        await db.GradeReopenRequests.ReplaceOneAsync(x => x.Id == request.Id, request, cancellationToken: ct);

        if (approve)
        {
            await db.ClassSections.UpdateOneAsync(
                x => x.Id == request.ClassSectionId,
                Builders<ClassSection>.Update
                    .Set(x => x.GradeStatus, "Reopened")
                    .Set(x => x.UpdatedAt, DateTime.UtcNow),
                cancellationToken: ct);
        }

        await db.AuditLogs.InsertOneAsync(new AuditLog
        {
            UserId = userId,
            Role = "Admin",
            Action = approve ? "ApproveGradeReopen" : "RejectGradeReopen",
            Entity = "ClassSection",
            EntityId = request.ClassSectionId,
            After = new { request.Status, note }
        }, cancellationToken: ct);

        return new Dictionary<string, object?>
        {
            ["id"] = request.Id,
            ["status"] = request.Status,
            ["reviewedAt"] = request.ReviewedAt,
            ["reviewNote"] = request.ReviewNote
        };
    }

    private static void ValidateDesign(SaveCourseDesignRequest request)
    {
        if (request.Clos.Count == 0) throw new AppException("Môn học phải có ít nhất một CLO");
        if (request.Scheme.Components.Count == 0) throw new AppException("Cấu trúc điểm phải có ít nhất một thành phần");
        var total = request.Scheme.Components.Sum(x => x.Weight);
        if (Math.Abs(total - 100) > 0.001) throw new AppException($"Tổng trọng số phải bằng 100%, hiện tại là {total}%");
        if (request.Scheme.Components.Any(x => x.Weight < 0 || x.MaxScore <= 0))
            throw new AppException("Trọng số không được âm và điểm tối đa phải lớn hơn 0");
        var codes = request.Clos.Select(x => x.CloCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = request.Scheme.Components.SelectMany(x => x.CloMappings).FirstOrDefault(x => !codes.Contains(x.CloCode));
        if (invalid is not null) throw new AppException($"CLO {invalid.CloCode} chưa tồn tại trong môn học");
    }

    private static CourseDesignDto Map(Course course) => new(
        course.Id,
        course.CourseCode,
        course.CourseName,
        course.Clos,
        course.GradingSchemes.OrderByDescending(x => x.Version).ToList());
}
