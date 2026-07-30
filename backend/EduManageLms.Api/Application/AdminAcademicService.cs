using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminAcademicService(MongoContext db) : IAdminAcademicService
{
    public async Task<CourseDesignDto> GetCourseDesignAsync(
        string courseId,
        CancellationToken ct)
    {
        var course = await db.Courses
            .Find(x => x.Id == courseId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");

        return MapCourseDesign(course);
    }

    public async Task<CourseDesignDto> SaveCourseDesignAsync(
        string courseId,
        SaveCourseDesignRequest request,
        string userId,
        CancellationToken ct)
    {
        var course = await db.Courses
            .Find(x => x.Id == courseId && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");

        ValidateDesign(request);

        var previousVersion = course.GradingSchemes
            .Where(x => x.Active)
            .Select(x => (int?)x.Version)
            .Max();

        foreach (var oldScheme in course.GradingSchemes)
        {
            oldScheme.Active = false;
        }

        var nextVersion = course.GradingSchemes.Count == 0
            ? 1
            : course.GradingSchemes.Max(x => x.Version) + 1;

        var newScheme = CloneScheme(request.Scheme);
        newScheme.Version = nextVersion;
        newScheme.EffectiveFrom = DateTime.UtcNow;
        newScheme.Active = true;

        course.Clos = request.Clos
            .Select(CloneClo)
            .ToList();

        course.GradingSchemes.Add(newScheme);
        course.UpdatedAt = DateTime.UtcNow;

        await db.Courses.ReplaceOneAsync(
            x => x.Id == course.Id && !x.IsDeleted,
            course,
            cancellationToken: ct);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = userId,
                Role = "Admin",
                Action = "GRADING_DESIGN_VERSION_CREATE",
                Entity = "Course",
                EntityId = course.Id,
                Before = new
                {
                    ActiveVersion = previousVersion
                },
                After = new
                {
                    ActiveVersion = newScheme.Version,
                    newScheme.AcademicYear,
                    ComponentCount = newScheme.Components.Count,
                    CloCount = course.Clos.Count
                },
                Note = "Tạo phiên bản cấu trúc điểm và CLO mới.",
                Result = "Success"
            },
            cancellationToken: ct);

        return MapCourseDesign(course);
    }

    public Task<PagedResult<Dictionary<string, object?>>> GetAuditLogsAsync(
        string? search,
        int page,
        int size,
        CancellationToken ct) =>
        GetAuditLogsAsync(
            search,
            role: null,
            action: null,
            result: null,
            from: null,
            to: null,
            page,
            size,
            ct);

    public async Task<PagedResult<Dictionary<string, object?>>> GetAuditLogsAsync(
        string? search,
        string? role,
        string? action,
        string? result,
        DateTime? from,
        DateTime? to,
        int page,
        int size,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var filter = Builders<AuditLog>.Filter.Eq(
            x => x.IsDeleted,
            false);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(
                    search.Trim()),
                "i");

            filter &= Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Regex(x => x.UserName, regex),
                Builders<AuditLog>.Filter.Regex(x => x.UserId, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Action, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Entity, regex),
                Builders<AuditLog>.Filter.Regex(x => x.EntityId!, regex),
                Builders<AuditLog>.Filter.Regex(x => x.Note!, regex));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                x => x.Role,
                role.Trim());
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                x => x.Action,
                action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result))
        {
            filter &= Builders<AuditLog>.Filter.Eq(
                x => x.Result,
                result.Trim());
        }

        if (from.HasValue)
        {
            filter &= Builders<AuditLog>.Filter.Gte(
                x => x.CreatedAt,
                from.Value.ToUniversalTime());
        }

        if (to.HasValue)
        {
            filter &= Builders<AuditLog>.Filter.Lte(
                x => x.CreatedAt,
                to.Value.ToUniversalTime());
        }

        var total = await db.AuditLogs.CountDocumentsAsync(
            filter,
            cancellationToken: ct);

        var items = await db.AuditLogs
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(ct);

        var mapped = items
            .Select(MapAuditLog)
            .ToList();

        return PagedResult<Dictionary<string, object?>>.Create(
            mapped,
            page,
            size,
            total);
    }

    public async Task<AdminReportDto> GetReportsAsync(
        CancellationToken ct)
    {
        var students = await db.Students
            .Find(x => !x.IsDeleted)
            .ToListAsync(ct);

        var sections = await db.ClassSections
            .Find(x => !x.IsDeleted)
            .ToListAsync(ct);

        var pendingReopenCount =
            await db.GradeReopenRequests.CountDocumentsAsync(
                x => x.Status == "Pending" && !x.IsDeleted,
                cancellationToken: ct);

        var recentActivities = await db.AuditLogs
            .Find(x => !x.IsDeleted)
            .SortByDescending(x => x.CreatedAt)
            .Limit(10)
            .ToListAsync(ct);

        var cards = new List<DashboardCardDto>
        {
            new(
                "Sinh viên",
                students.Count,
                "school",
                null,
                "primary"),
            new(
                "Lớp học phần",
                sections.Count,
                "class",
                null,
                "primary"),
            new(
                "Bảng điểm đã công bố",
                sections.Count(x => x.GradeStatus == "Published"),
                "publish",
                null,
                "success"),
            new(
                "Yêu cầu mở điểm",
                pendingReopenCount,
                "lock_open",
                null,
                "warning")
        };

        var studentsByFaculty = students
            .GroupBy(x =>
                string.IsNullOrWhiteSpace(x.Faculty.FacultyName)
                    ? "Chưa xác định"
                    : x.Faculty.FacultyName)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var gradeStatus = sections
            .GroupBy(x =>
                string.IsNullOrWhiteSpace(x.GradeStatus)
                    ? "Unknown"
                    : x.GradeStatus)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var learningStatus = students
            .GroupBy(x =>
                string.IsNullOrWhiteSpace(x.Status)
                    ? "Unknown"
                    : x.Status)
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .OrderByDescending(x => x.Value)
            .ToList();

        var cloPipeline = new[]
        {
            BsonDocument.Parse(
                "{ $match: { isDeleted: false } }"),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords' }"),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords.semesters' }"),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords.semesters.courses' }"),
            BsonDocument.Parse(
                "{ $match: { " +
                "'academicRecords.semesters.courses.scoreStatus': " +
                "'Published' } }"),
            BsonDocument.Parse(
                "{ $unwind: " +
                "'$academicRecords.semesters.courses.scores' }"),
            BsonDocument.Parse(
                "{ $match: { " +
                "'academicRecords.semesters.courses.scores.score': " +
                "{ $ne: null }, " +
                "'academicRecords.semesters.courses.scores.maxScore': " +
                "{ $gt: 0 } } }"),
            BsonDocument.Parse(
                "{ $unwind: " +
                "'$academicRecords.semesters.courses.scores.cloMappings' }"),
            BsonDocument.Parse(
                "{ $group: { " +
                "_id: " +
                "'$academicRecords.semesters.courses.scores." +
                "cloMappings.cloCode', " +
                "avg: { $avg: { $multiply: [ " +
                "{ $divide: [ " +
                "'$academicRecords.semesters.courses.scores.score', " +
                "'$academicRecords.semesters.courses.scores.maxScore' " +
                "] }, 100 ] } } } }"),
            BsonDocument.Parse(
                "{ $project: { _id: 0, label: '$_id', " +
                "value: { $round: ['$avg', 2] } } }"),
            BsonDocument.Parse(
                "{ $sort: { label: 1 } }")
        };

        var cloDocuments = await db.Students
            .Aggregate<BsonDocument>(cloPipeline)
            .ToListAsync(ct);

        var cloAchievement = cloDocuments
            .Where(x =>
                x.TryGetValue("label", out var label)
                && label.IsString
                && x.TryGetValue("value", out var value)
                && value.IsNumeric)
            .Select(x => new ChartItemDto(
                x["label"].AsString,
                x["value"].ToDouble()))
            .ToList();

        var activities = recentActivities
            .Select(x => new ActivityDto(
                x.Action,
                $"{x.Entity}"
                + (string.IsNullOrWhiteSpace(x.UserName)
                    ? string.Empty
                    : $" · {x.UserName}"),
                x.CreatedAt.ToString("dd/MM HH:mm"),
                ActivityIcon(x.Action)))
            .ToList();

        return new AdminReportDto(
            cards,
            studentsByFaculty,
            gradeStatus,
            learningStatus,
            cloAchievement,
            activities);
    }

    public async Task<PagedResult<Dictionary<string, object?>>> GetReopenRequestsAsync(
        string? status,
        string? search,
        int page,
        int size,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);

        var filter = Builders<GradeReopenRequest>.Filter.Eq(
            x => x.IsDeleted,
            false);

        if (!string.IsNullOrWhiteSpace(status)
            && !status.Equals(
                "All",
                StringComparison.OrdinalIgnoreCase))
        {
            filter &= Builders<GradeReopenRequest>.Filter.Eq(
                x => x.Status,
                status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(
                    search.Trim()),
                "i");

            filter &= Builders<GradeReopenRequest>.Filter.Or(
                Builders<GradeReopenRequest>.Filter.Regex(
                    x => x.ClassSectionCode,
                    regex),
                Builders<GradeReopenRequest>.Filter.Regex(
                    x => x.LecturerCode,
                    regex),
                Builders<GradeReopenRequest>.Filter.Regex(
                    x => x.Reason,
                    regex));
        }

        var total =
            await db.GradeReopenRequests.CountDocumentsAsync(
                filter,
                cancellationToken: ct);

        var requests = await db.GradeReopenRequests
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(ct);

        var items = requests
            .Select(MapReopenRequest)
            .ToList();

        return PagedResult<Dictionary<string, object?>>.Create(
            items,
            page,
            size,
            total);
    }

    public async Task<Dictionary<string, object?>> GetReopenRequestAsync(
        string id,
        CancellationToken ct)
    {
        var request = await db.GradeReopenRequests
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                "Không tìm thấy yêu cầu mở điểm.");

        return MapReopenRequest(request);
    }

    public async Task<Dictionary<string, object?>> ReviewReopenRequestAsync(
        string id,
        bool approve,
        string note,
        string userId,
        CancellationToken ct)
    {
        note = note?.Trim() ?? string.Empty;

        if (!approve && string.IsNullOrWhiteSpace(note))
        {
            throw new AppException(
                "Phải nhập lý do từ chối yêu cầu.");
        }

        var request = await db.GradeReopenRequests
            .Find(x => x.Id == id && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                "Không tìm thấy yêu cầu mở điểm.");

        if (!request.Status.Equals(
            "Pending",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException(
                "Yêu cầu đã được xử lý trước đó.");
        }

        var section = await db.ClassSections
            .Find(x =>
                x.Id == request.ClassSectionId
                && !x.IsDeleted)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(
                "Không tìm thấy lớp học phần của yêu cầu.");

        var previousSectionStatus = section.GradeStatus;
        var now = DateTime.UtcNow;

        if (approve)
        {
            if (section.GradeStatus is not ("Published" or "Locked"))
            {
                throw new AppException(
                    "Chỉ được mở lại bảng điểm đang Published hoặc Locked.");
            }

            var studentIds = section.Students
                .Select(x => x.StudentId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var students = studentIds.Count == 0
                ? []
                : await db.Students
                    .Find(
                        Builders<Student>.Filter.In(
                            x => x.Id,
                            studentIds)
                        & Builders<Student>.Filter.Eq(
                            x => x.IsDeleted,
                            false))
                    .ToListAsync(ct);

            var writes = new List<WriteModel<Student>>();

            foreach (var student in students)
            {
                var course = student.AcademicRecords
                    .SelectMany(x => x.Semesters)
                    .SelectMany(x => x.Courses)
                    .FirstOrDefault(x =>
                        x.ClassSectionId == section.Id);

                if (course is null)
                {
                    continue;
                }

                course.ScoreStatus = "Reopened";
                course.PublishedAt = null;
                course.Version++;
                student.UpdatedAt = now;

                writes.Add(
                    new ReplaceOneModel<Student>(
                        Builders<Student>.Filter.Eq(
                            x => x.Id,
                            student.Id),
                        student));
            }

            if (writes.Count > 0)
            {
                await db.Students.BulkWriteAsync(
                    writes,
                    new BulkWriteOptions
                    {
                        IsOrdered = false
                    },
                    ct);
            }

            section.GradeStatus = "Reopened";
            section.UpdatedAt = now;

            await db.ClassSections.ReplaceOneAsync(
                x => x.Id == section.Id && !x.IsDeleted,
                section,
                cancellationToken: ct);
        }

        request.Status = approve ? "Approved" : "Rejected";
        request.ReviewedAt = now;
        request.ReviewedBy = userId;
        request.ReviewNote = note;
        request.UpdatedAt = now;

        await db.GradeReopenRequests.ReplaceOneAsync(
            x =>
                x.Id == request.Id
                && x.Status == "Pending"
                && !x.IsDeleted,
            request,
            cancellationToken: ct);

        await db.AuditLogs.InsertOneAsync(
            new AuditLog
            {
                UserId = userId,
                Role = "Admin",
                Action = approve
                    ? "GRADE_REOPEN_APPROVE"
                    : "GRADE_REOPEN_REJECT",
                Entity = "GradeReopenRequest",
                EntityId = request.Id,
                Before = new
                {
                    RequestStatus = "Pending",
                    GradeStatus = previousSectionStatus
                },
                After = new
                {
                    RequestStatus = request.Status,
                    GradeStatus = approve
                        ? section.GradeStatus
                        : previousSectionStatus
                },
                Note = note,
                Result = "Success"
            },
            cancellationToken: ct);

        await NotifyLecturerAsync(
            request,
            section,
            approve,
            note,
            userId,
            ct);

        return MapReopenRequest(request);
    }

    private async Task NotifyLecturerAsync(
        GradeReopenRequest request,
        ClassSection section,
        bool approved,
        string note,
        string senderId,
        CancellationToken ct)
    {
        var lecturerUser = await db.Users
            .Find(x =>
                x.LecturerCode == request.LecturerCode
                && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (lecturerUser is null)
        {
            return;
        }

        await db.Notifications.InsertOneAsync(
            new Notification
            {
                Title = approved
                    ? "Yêu cầu mở bảng điểm đã được duyệt"
                    : "Yêu cầu mở bảng điểm bị từ chối",
                Content =
                    $"Lớp {section.ClassSectionCode}: "
                    + (approved
                        ? "bảng điểm đã được mở lại để chỉnh sửa."
                        : "yêu cầu mở lại bảng điểm không được duyệt.")
                    + (string.IsNullOrWhiteSpace(note)
                        ? string.Empty
                        : $" Ghi chú: {note}"),
                Type = "Grade",
                Priority = approved ? "High" : "Normal",
                SenderId = senderId,
                RecipientIds = [lecturerUser.Id],
                AudienceType = "SpecificUsers",
                DisplayFrom = DateTime.UtcNow,
                Status = "Sent"
            },
            cancellationToken: ct);
    }

    private static void ValidateDesign(
        SaveCourseDesignRequest request)
    {
        if (request.Clos.Count == 0)
        {
            throw new AppException(
                "Môn học phải có ít nhất một CLO.");
        }

        if (request.Scheme.Components.Count == 0)
        {
            throw new AppException(
                "Cấu trúc điểm phải có ít nhất một thành phần.");
        }

        if (string.IsNullOrWhiteSpace(
            request.Scheme.AcademicYear))
        {
            throw new AppException(
                "Phải nhập năm học áp dụng.");
        }

        var cloCodes = request.Clos
            .Select(x => x.CloCode?.Trim() ?? string.Empty)
            .ToList();

        if (cloCodes.Any(string.IsNullOrWhiteSpace))
        {
            throw new AppException(
                "Mã CLO không được để trống.");
        }

        if (cloCodes
            .GroupBy(
                x => x,
                StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1))
        {
            throw new AppException(
                "Mã CLO không được trùng nhau.");
        }

        if (request.Clos.Any(x =>
            x.Threshold < 0
            || x.Threshold > 100
            || x.Weight < 0
            || x.Weight > 100))
        {
            throw new AppException(
                "Ngưỡng và trọng số CLO phải trong khoảng 0–100.");
        }

        var totalWeight = request.Scheme.Components
            .Sum(x => x.Weight);

        if (Math.Abs(totalWeight - 100) > 0.001)
        {
            throw new AppException(
                $"Tổng trọng số phải bằng 100%, "
                + $"hiện tại là {totalWeight:0.##}%.");
        }

        var componentIds = request.Scheme.Components
            .Select(x =>
                x.ComponentId?.Trim() ?? string.Empty)
            .ToList();

        if (componentIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new AppException(
                "Mã thành phần điểm không được để trống.");
        }

        if (componentIds
            .GroupBy(
                x => x,
                StringComparer.OrdinalIgnoreCase)
            .Any(x => x.Count() > 1))
        {
            throw new AppException(
                "Mã thành phần điểm không được trùng nhau.");
        }

        var availableCloCodes = cloCodes.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        foreach (var component in request.Scheme.Components)
        {
            if (component.Weight < 0
                || component.Weight > 100)
            {
                throw new AppException(
                    $"Trọng số {component.Name} không hợp lệ.");
            }

            if (component.MaxScore <= 0
                || component.MaxScore > 100)
            {
                throw new AppException(
                    $"Điểm tối đa {component.Name} không hợp lệ.");
            }

            if (component.MinimumScore.HasValue
                && (component.MinimumScore < 0
                    || component.MinimumScore > component.MaxScore))
            {
                throw new AppException(
                    $"Điểm tối thiểu {component.Name} không hợp lệ.");
            }

            foreach (var mapping in component.CloMappings)
            {
                if (!availableCloCodes.Contains(mapping.CloCode))
                {
                    throw new AppException(
                        $"CLO {mapping.CloCode} chưa tồn tại.");
                }

                if (mapping.MappingWeight < 0
                    || mapping.MappingWeight > 100)
                {
                    throw new AppException(
                        $"Trọng số ánh xạ CLO của "
                        + $"{component.Name} không hợp lệ.");
                }
            }

            if (component.CloMappings.Count > 0)
            {
                var mappingTotal = component.CloMappings
                    .Sum(x => x.MappingWeight);

                if (Math.Abs(mappingTotal - 100) > 0.001)
                {
                    throw new AppException(
                        $"Tổng ánh xạ CLO của {component.Name} "
                        + $"phải bằng 100%, hiện tại "
                        + $"{mappingTotal:0.##}%.");
                }
            }
        }
    }

    private static CourseDesignDto MapCourseDesign(
        Course course) =>
        new(
            course.Id,
            course.CourseCode,
            course.CourseName,
            course.Clos,
            course.GradingSchemes
                .OrderByDescending(x => x.Version)
                .ToList());

    private static Dictionary<string, object?> MapAuditLog(
        AuditLog item) =>
        new()
        {
            ["id"] = item.Id,
            ["userId"] = item.UserId,
            ["userName"] = item.UserName,
            ["role"] = item.Role,
            ["action"] = item.Action,
            ["entity"] = item.Entity,
            ["entityId"] = item.EntityId,
            ["before"] = item.Before,
            ["after"] = item.After,
            ["result"] = item.Result,
            ["ipAddress"] = item.IpAddress,
            ["userAgent"] = item.UserAgent,
            ["createdAt"] = item.CreatedAt,
            ["note"] = item.Note
        };

    private static Dictionary<string, object?> MapReopenRequest(
        GradeReopenRequest item) =>
        new()
        {
            ["id"] = item.Id,
            ["classSectionId"] = item.ClassSectionId,
            ["classSectionCode"] = item.ClassSectionCode,
            ["lecturerCode"] = item.LecturerCode,
            ["reason"] = item.Reason,
            ["status"] = item.Status,
            ["reviewedAt"] = item.ReviewedAt,
            ["reviewedBy"] = item.ReviewedBy,
            ["reviewNote"] = item.ReviewNote,
            ["createdAt"] = item.CreatedAt,
            ["updatedAt"] = item.UpdatedAt
        };

    private static string ActivityIcon(
        string action)
    {
        if (action.Contains(
            "GRADE",
            StringComparison.OrdinalIgnoreCase))
        {
            return "grading";
        }

        if (action.Contains(
            "BACKUP",
            StringComparison.OrdinalIgnoreCase)
            || action.Contains(
                "RESTORE",
                StringComparison.OrdinalIgnoreCase))
        {
            return "database";
        }

        if (action.Contains(
            "LOGIN",
            StringComparison.OrdinalIgnoreCase))
        {
            return "login";
        }

        return "history";
    }

    private static CloDefinition CloneClo(
        CloDefinition source) =>
        new()
        {
            CloCode = source.CloCode.Trim().ToUpperInvariant(),
            Name = source.Name.Trim(),
            Description = source.Description?.Trim() ?? string.Empty,
            BloomLevel = source.BloomLevel,
            Threshold = source.Threshold,
            Weight = source.Weight,
            Active = source.Active
        };

    private static GradingSchemeVersion CloneScheme(
        GradingSchemeVersion source) =>
        new()
        {
            Version = source.Version,
            AcademicYear = source.AcademicYear.Trim(),
            Components = source.Components
                .Select(component =>
                    new GradingComponentDefinition
                    {
                        ComponentId = component.ComponentId
                            .Trim()
                            .ToUpperInvariant(),
                        Name = component.Name.Trim(),
                        Type = component.Type,
                        Weight = component.Weight,
                        MaxScore = component.MaxScore,
                        IsRequired = component.IsRequired,
                        MinimumScore = component.MinimumScore,
                        IsFinalCondition = component.IsFinalCondition,
                        CloMappings = component.CloMappings
                            .Select(mapping =>
                                new CloMapping
                                {
                                    CloCode = mapping.CloCode
                                        .Trim()
                                        .ToUpperInvariant(),
                                    MappingWeight =
                                        mapping.MappingWeight
                                })
                            .ToList()
                    })
                .ToList(),
            PassingScore = source.PassingScore,
            RoundingMode = source.RoundingMode,
            DecimalPlaces = source.DecimalPlaces,
            EffectiveFrom = source.EffectiveFrom,
            Active = source.Active
        };
}
