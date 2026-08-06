using ClosedXML.Excel;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminAcademicService(MongoContext db) : IAdminAcademicService
{
    public async Task<IReadOnlyCollection<AdminCourseOptionDto>> GetGradingCoursesAsync(
        CancellationToken ct)
    {
        var documents = await db.Database
            .GetCollection<BsonDocument>("courses")
            .Find(Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .Sort(Builders<BsonDocument>.Sort.Ascending("courseCode"))
            .Project(
                Builders<BsonDocument>.Projection
                    .Include("_id")
                    .Include("courseCode")
                    .Include("courseName")
                    .Include("code")
                    .Include("name"))
            .ToListAsync(ct);

        return documents
            .Select(document => new AdminCourseOptionDto(
                ReadId(document),
                ReadString(document, "courseCode", "code"),
                ReadString(document, "courseName", "name")))
            .ToList();
    }

    public async Task<AdminReportOptionsDto> GetReportOptionsAsync(
        CancellationToken ct)
    {
        var academicYears = await LoadLookupOptionsAsync(
            "academicYears",
            ["academicYearCode", "code"],
            ["academicYearName", "name"],
            ct);
        var semesters = await LoadLookupOptionsAsync(
            "semesters",
            ["semesterCode", "code"],
            ["semesterName", "name"],
            ct);
        var faculties = await LoadLookupOptionsAsync(
            "faculties",
            ["facultyCode", "code"],
            ["facultyName", "name"],
            ct);
        var programs = await LoadLookupOptionsAsync(
            "programs",
            ["programCode", "code"],
            ["programName", "name"],
            ct);

        return new AdminReportOptionsDto(
            academicYears,
            semesters,
            faculties,
            programs);
    }

    public async Task<AdminNotificationOptionsDto> GetNotificationOptionsAsync(
        CancellationToken ct)
    {
        var faculties = await LoadLookupOptionsAsync(
            "faculties",
            ["facultyCode", "code"],
            ["facultyName", "name"],
            ct);
        var classSections = await LoadLookupOptionsAsync(
            "classSections",
            ["classSectionCode", "sectionCode", "code"],
            ["courseName", "classSectionName", "name"],
            ct);

        return new AdminNotificationOptionsDto(
            faculties,
            classSections);
    }

    public async Task<CourseDesignDto> GetCourseDesignAsync(
        string courseId,
        CancellationToken ct)
    {
        var course = await FindCourseDocumentAsync(courseId, ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");

        return MapCourseDesign(course);
    }

    public async Task<CourseDesignDto> SaveCourseDesignAsync(
        string courseId,
        SaveCourseDesignRequest request,
        string userId,
        CancellationToken ct)
    {
        var course = await FindCourseDocumentAsync(courseId, ct)
            ?? throw new NotFoundException("Không tìm thấy môn học.");

        ValidateDesign(request);

        var current = MapCourseDesign(course);
        var schemes = current.GradingSchemes
            .Select(CloneScheme)
            .ToList();

        var previousVersion = schemes
            .Where(x => x.Active)
            .Select(x => (int?)x.Version)
            .Max();

        foreach (var oldScheme in schemes)
        {
            oldScheme.Active = false;
        }

        var nextVersion = schemes.Count == 0
            ? 1
            : schemes.Max(x => x.Version) + 1;

        var newScheme = CloneScheme(request.Scheme);
        newScheme.Version = nextVersion;
        newScheme.EffectiveFrom = DateTime.UtcNow;
        newScheme.Active = true;

        var clos = request.Clos
            .Select(CloneClo)
            .ToList();

        schemes.Add(newScheme);
        var updatedAt = DateTime.UtcNow;
        var courses = db.Database.GetCollection<BsonDocument>("courses");
        var courseObjectId = ParseObjectId(courseId);

        using var session = await db.Client.StartSessionAsync(
            cancellationToken: ct);
        await session.WithTransactionAsync(
            async (_, token) =>
            {
                var updateResult = await courses.UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", courseObjectId)
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true),
                    Builders<BsonDocument>.Update
                        .Set(
                            "clos",
                            new BsonArray(
                                clos.Select(item =>
                                    (BsonValue)item.ToBsonDocument())))
                        .Set(
                            "gradingSchemes",
                            new BsonArray(
                                schemes.Select(item =>
                                    (BsonValue)item.ToBsonDocument())))
                        .Set("updatedAt", updatedAt),
                    cancellationToken: token);
                if (updateResult.MatchedCount != 1)
                {
                    throw new ConflictException(
                        "Môn học đã thay đổi hoặc không còn tồn tại.");
                }

                await db.AuditLogs.InsertOneAsync(
                    session,
                    new AuditLog
                    {
                        UserId = userId,
                        Role = "Admin",
                        Action = "GRADING_DESIGN_VERSION_CREATE",
                        Entity = "Course",
                        EntityId = current.CourseId,
                        Before = new
                        {
                            ActiveVersion = previousVersion
                        },
                        After = new
                        {
                            ActiveVersion = newScheme.Version,
                            newScheme.AcademicYear,
                            ComponentCount = newScheme.Components.Count,
                            CloCount = clos.Count
                        },
                        Note = "Tạo phiên bản cấu trúc điểm và CLO mới.",
                        Result = "Success"
                    },
                    cancellationToken: token);

                return true;
            },
            new TransactionOptions(
                readPreference: ReadPreference.Primary,
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority),
            ct);

        return new CourseDesignDto(
            current.CourseId,
            current.CourseCode,
            current.CourseName,
            clos,
            schemes.OrderByDescending(item => item.Version).ToList());
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
        string? academicYearId,
        string? semesterId,
        string? facultyId,
        string? programId,
        CancellationToken ct)
    {
        var studentFilter = Builders<Student>.Filter.Eq(
            x => x.IsDeleted,
            false);
        if (!string.IsNullOrWhiteSpace(facultyId))
            studentFilter &= Builders<Student>.Filter.Eq(
                x => x.Faculty.FacultyId,
                facultyId);
        if (!string.IsNullOrWhiteSpace(programId))
            studentFilter &= Builders<Student>.Filter.Eq(
                x => x.Program.ProgramId,
                programId);

        var students = await db.Students
            .Find(studentFilter)
            .ToListAsync(ct);

        var sectionFilter = Builders<ClassSection>.Filter.Eq(
            x => x.IsDeleted,
            false);
        if (!string.IsNullOrWhiteSpace(academicYearId))
            sectionFilter &= Builders<ClassSection>.Filter.Eq(
                x => x.AcademicYearId,
                academicYearId);
        if (!string.IsNullOrWhiteSpace(semesterId))
            sectionFilter &= Builders<ClassSection>.Filter.Eq(
                x => x.SemesterId,
                semesterId);
        if (!string.IsNullOrWhiteSpace(facultyId)
            || !string.IsNullOrWhiteSpace(programId))
        {
            var studentIds = students.Select(student => student.Id).ToArray();
            var enrollmentFilter =
                Builders<StudentEnrollmentSnapshot>.Filter.In(
                    enrollment => enrollment.StudentId,
                    studentIds);
            sectionFilter &= Builders<ClassSection>.Filter.ElemMatch(
                section => section.Students,
                enrollmentFilter);
        }

        var sections = await db.ClassSections
            .Find(sectionFilter)
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

        var studentObjectIds = new BsonArray(
            students.Select(student =>
                (BsonValue)new BsonObjectId(ObjectId.Parse(student.Id))));
        var cloPipeline = new[]
        {
            new BsonDocument(
                "$match",
                new BsonDocument
                {
                    ["isDeleted"] = false,
                    ["_id"] = new BsonDocument("$in", studentObjectIds)
                }),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords' }"),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords.semesters' }"),
            BsonDocument.Parse(
                "{ $unwind: '$academicRecords.semesters.courses' }"),
            BsonDocument.Parse(
                "{ $match: { " +
                "'academicRecords.semesters.courses.scoreStatus': " +
                "{ $in: ['Published', 'Locked'] } } }"),
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

    public async Task<byte[]> ExportReportAsync(
        string? academicYearId,
        string? semesterId,
        string? facultyId,
        string? programId,
        CancellationToken ct)
    {
        var report = await GetReportsAsync(
            academicYearId,
            semesterId,
            facultyId,
            programId,
            ct);

        using var workbook = new XLWorkbook();
        var overview = workbook.Worksheets.Add("Tong quan");
        overview.Cell("A1").Value = "BÁO CÁO ĐÀO TẠO EDUMANAGE LMS";
        overview.Range("A1:B1").Merge();
        overview.Range("A1:B1").Style.Font.SetBold().Font.SetFontSize(16);
        overview.Cell("A3").Value = "Chỉ số";
        overview.Cell("B3").Value = "Giá trị";
        overview.Range("A3:B3").Style.Font.SetBold();
        var row = 4;
        foreach (var card in report.Cards)
        {
            overview.Cell(row, 1).Value = card.Label;
            overview.Cell(row, 2).Value = card.Value?.ToString() ?? string.Empty;
            row++;
        }

        WriteChartSheet(workbook, "Sinh vien theo khoa", report.StudentsByFaculty);
        WriteChartSheet(workbook, "Trang thai bang diem", report.GradeStatus);
        WriteChartSheet(workbook, "Trang thai hoc tap", report.LearningStatus);
        WriteChartSheet(workbook, "CLO", report.CloAchievement);
        foreach (var sheet in workbook.Worksheets)
            sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportReportPdfAsync(
        string? academicYearId,
        string? semesterId,
        string? facultyId,
        string? programId,
        CancellationToken ct)
    {
        var report = await GetReportsAsync(
            academicYearId,
            semesterId,
            facultyId,
            programId,
            ct);
        var filters = new[]
        {
            $"Năm học: {academicYearId ?? "Tất cả"}",
            $"Học kỳ: {semesterId ?? "Tất cả"}",
            $"Khoa: {facultyId ?? "Tất cả"}",
            $"Chương trình: {programId ?? "Tất cả"}"
        };
        return AdminReportPdfBuilder.Build(
            report,
            string.Join(" · ", filters),
            DateTime.Now);
    }

    private static void WriteChartSheet(
        XLWorkbook workbook,
        string name,
        IReadOnlyCollection<ChartItemDto> items)
    {
        var sheet = workbook.Worksheets.Add(name);
        sheet.Cell(1, 1).Value = "Nhãn";
        sheet.Cell(1, 2).Value = "Giá trị";
        sheet.Range(1, 1, 1, 2).Style.Font.SetBold();
        var row = 2;
        foreach (var item in items)
        {
            sheet.Cell(row, 1).Value = item.Label;
            sheet.Cell(row, 2).Value = item.Value;
            row++;
        }
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
        var writes = new List<WriteModel<Student>>();

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

            section.GradeStatus = "Reopened";
            section.UpdatedAt = now;
        }

        request.Status = approve ? "Approved" : "Rejected";
        request.ReviewedAt = now;
        request.ReviewedBy = userId;
        request.ReviewNote = note;
        request.UpdatedAt = now;

        using var session = await db.Client.StartSessionAsync(
            cancellationToken: ct);
        await session.WithTransactionAsync(
            async (current, _) =>
            {
                if (approve && writes.Count > 0)
                {
                    await db.Students.BulkWriteAsync(
                        current,
                        writes,
                        new BulkWriteOptions { IsOrdered = false },
                        ct);
                }

                if (approve)
                {
                    var sectionResult = await db.ClassSections.ReplaceOneAsync(
                        current,
                        x =>
                            x.Id == section.Id
                            && (x.GradeStatus == "Published"
                                || x.GradeStatus == "Locked")
                            && !x.IsDeleted,
                        section,
                        cancellationToken: ct);
                    if (sectionResult.ModifiedCount != 1)
                        throw new ConflictException(
                            "Trạng thái bảng điểm đã thay đổi. Vui lòng tải lại.");
                }

                var requestResult = await db.GradeReopenRequests.ReplaceOneAsync(
                    current,
                    x =>
                        x.Id == request.Id
                        && x.Status == "Pending"
                        && !x.IsDeleted,
                    request,
                    cancellationToken: ct);
                if (requestResult.ModifiedCount != 1)
                    throw new ConflictException(
                        "Yêu cầu đã được xử lý trước đó.");

                await db.AuditLogs.InsertOneAsync(
                    current,
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
                    current,
                    request,
                    section,
                    approve,
                    note,
                    userId,
                    ct);
                return true;
            },
            new TransactionOptions(
                ReadConcern.Snapshot,
                ReadPreference.Primary,
                WriteConcern.WMajority),
            ct);

        return MapReopenRequest(request);
    }

    private async Task NotifyLecturerAsync(
        IClientSessionHandle session,
        GradeReopenRequest request,
        ClassSection section,
        bool approved,
        string note,
        string senderId,
        CancellationToken ct)
    {
        var lecturerUser = await db.Users
            .Find(session, x =>
                x.LecturerCode == request.LecturerCode
                && !x.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (lecturerUser is null)
        {
            return;
        }

        await db.Notifications.InsertOneAsync(
            session,
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
        if (request is null
            || request.Clos is null
            || request.Scheme is null)
        {
            throw new AppException(
                "Dữ liệu cấu trúc điểm không hợp lệ.");
        }

        if (request.Clos.Count == 0)
        {
            throw new AppException(
                "Môn học phải có ít nhất một CLO.");
        }

        if (request.Scheme.Components is null
            || request.Scheme.Components.Count == 0)
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

            var mappings = component.CloMappings ?? [];
            foreach (var mapping in mappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.CloCode)
                    || !availableCloCodes.Contains(mapping.CloCode))
                {
                    throw new AppException(
                        $"CLO {mapping.CloCode} chưa tồn tại "
                        + "hoặc chưa được chọn.");
                }

                if (mapping.MappingWeight < 0
                    || mapping.MappingWeight > 100)
                {
                    throw new AppException(
                        $"Trọng số ánh xạ CLO của "
                        + $"{component.Name} không hợp lệ.");
                }
            }

            if (mappings.Count > 0)
            {
                var mappingTotal = mappings
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

    private async Task<BsonDocument?> FindCourseDocumentAsync(
        string courseId,
        CancellationToken ct)
    {
        var objectId = ParseObjectId(courseId);
        return await db.Database
            .GetCollection<BsonDocument>("courses")
            .Find(
                Builders<BsonDocument>.Filter.Eq("_id", objectId)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyCollection<AdminLookupOptionDto>> LoadLookupOptionsAsync(
        string collectionName,
        string[] codeFields,
        string[] nameFields,
        CancellationToken ct)
    {
        var fields = codeFields
            .Concat(nameFields)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var projections = fields
            .Select(field =>
                Builders<BsonDocument>.Projection.Include(field))
            .ToList();
        projections.Add(
            Builders<BsonDocument>.Projection.Include("_id"));

        var documents = await db.Database
            .GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .Project(
                Builders<BsonDocument>.Projection.Combine(projections))
            .ToListAsync(ct);

        return documents
            .Select(document => new AdminLookupOptionDto(
                ReadId(document),
                ReadString(document, codeFields),
                ReadString(document, nameFields)))
            .OrderBy(item => item.Code)
            .ThenBy(item => item.Name)
            .ToList();
    }

    private static CourseDesignDto MapCourseDesign(
        BsonDocument course)
    {
        var clos = ReadDocuments(course, "clos")
            .Select(MapClo)
            .ToList();
        var schemes = ReadDocuments(course, "gradingSchemes")
            .Select(MapScheme)
            .OrderByDescending(item => item.Version)
            .ToList();

        return new CourseDesignDto(
            ReadId(course),
            ReadString(course, "courseCode", "code"),
            ReadString(course, "courseName", "name"),
            clos,
            schemes);
    }

    private static CloDefinition MapClo(BsonDocument document) =>
        new()
        {
            CloCode = ReadString(document, "cloCode", "code"),
            Name = ReadString(document, "name", "cloName"),
            Description = ReadString(document, "description"),
            BloomLevel = ReadStringOrDefault(
                document,
                "Apply",
                "bloomLevel",
                "bloom"),
            Threshold = ReadDouble(document, 50, "threshold"),
            Weight = ReadDouble(document, 0, "weight"),
            Active = ReadBool(document, true, "active")
        };

    private static GradingSchemeVersion MapScheme(
        BsonDocument document) =>
        new()
        {
            Version = ReadInt(document, 1, "version"),
            AcademicYear = ReadString(
                document,
                "academicYear",
                "academicYearName"),
            Components = ReadDocuments(document, "components")
                .Select(MapComponent)
                .ToList(),
            PassingScore = ReadDouble(
                document,
                4,
                "passingScore",
                "passScore"),
            RoundingMode = ReadStringOrDefault(
                document,
                "Normal",
                "roundingMode",
                "rounding"),
            DecimalPlaces = ReadInt(
                document,
                2,
                "decimalPlaces"),
            EffectiveFrom = ReadDate(
                document,
                DateTime.UtcNow,
                "effectiveFrom"),
            Active = ReadBool(document, true, "active")
        };

    private static GradingComponentDefinition MapComponent(
        BsonDocument document) =>
        new()
        {
            ComponentId = ReadString(
                document,
                "componentId",
                "code"),
            Name = ReadString(
                document,
                "name",
                "componentName"),
            Type = ReadStringOrDefault(
                document,
                "Assignment",
                "type"),
            Weight = ReadDouble(document, 0, "weight"),
            MaxScore = ReadDouble(
                document,
                10,
                "maxScore"),
            IsRequired = ReadBool(
                document,
                false,
                "isRequired",
                "required"),
            MinimumScore = ReadNullableDouble(
                document,
                "minimumScore",
                "minScore"),
            IsFinalCondition = ReadBool(
                document,
                false,
                "isFinalCondition",
                "finalCondition"),
            CloMappings = ReadDocuments(document, "cloMappings")
                .Select(MapCloMapping)
                .ToList()
        };

    private static CloMapping MapCloMapping(
        BsonDocument document) =>
        new()
        {
            CloCode = ReadString(
                document,
                "cloCode",
                "code"),
            MappingWeight = ReadDouble(
                document,
                100,
                "mappingWeight",
                "weight")
        };

    private static IEnumerable<BsonDocument> ReadDocuments(
        BsonDocument document,
        string field)
    {
        if (!document.TryGetValue(field, out var value)
            || !value.IsBsonArray)
        {
            return [];
        }

        return value.AsBsonArray
            .Where(item => item.IsBsonDocument)
            .Select(item => item.AsBsonDocument);
    }

    private static string ReadId(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out var value))
        {
            return string.Empty;
        }

        return value.IsObjectId
            ? value.AsObjectId.ToString()
            : value.IsString
                ? value.AsString
                : value.ToString();
    }

    private static string ReadString(
        BsonDocument document,
        params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value)
                || value.IsBsonNull)
            {
                continue;
            }

            return value.IsString
                ? value.AsString
                : value.ToString();
        }

        return string.Empty;
    }

    private static string ReadStringOrDefault(
        BsonDocument document,
        string fallback,
        params string[] fields)
    {
        var value = ReadString(document, fields);
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }

    private static double ReadDouble(
        BsonDocument document,
        double fallback,
        params string[] fields) =>
        ReadNullableDouble(document, fields) ?? fallback;

    private static double? ReadNullableDouble(
        BsonDocument document,
        params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value)
                || value.IsBsonNull)
            {
                continue;
            }

            if (value.IsNumeric)
            {
                return value.ToDouble();
            }

            if (value.IsString
                && double.TryParse(
                    value.AsString.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int ReadInt(
        BsonDocument document,
        int fallback,
        params string[] fields)
    {
        var value = ReadNullableDouble(document, fields);
        return value.HasValue
            ? Convert.ToInt32(value.Value)
            : fallback;
    }

    private static bool ReadBool(
        BsonDocument document,
        bool fallback,
        params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value)
                || value.IsBsonNull)
            {
                continue;
            }

            if (value.IsBoolean)
            {
                return value.AsBoolean;
            }

            if (value.IsNumeric)
            {
                return Math.Abs(value.ToDouble()) > double.Epsilon;
            }

            if (value.IsString
                && bool.TryParse(value.AsString, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static DateTime ReadDate(
        BsonDocument document,
        DateTime fallback,
        params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value)
                || value.IsBsonNull)
            {
                continue;
            }

            if (value.IsValidDateTime)
            {
                return value.ToUniversalTime();
            }

            if (value.IsString
                && DateTime.TryParse(
                    value.AsString,
                    out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return fallback;
    }

    private static ObjectId ParseObjectId(string id) =>
        ObjectId.TryParse(id, out var objectId)
            ? objectId
            : throw new AppException("Id môn học không hợp lệ.");

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
            Name = source.Name?.Trim() ?? string.Empty,
            Description = source.Description?.Trim() ?? string.Empty,
            BloomLevel = string.IsNullOrWhiteSpace(source.BloomLevel)
                ? "Apply"
                : source.BloomLevel,
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
            Components = (source.Components ?? [])
                .Select(component =>
                    new GradingComponentDefinition
                    {
                        ComponentId = component.ComponentId
                            .Trim()
                            .ToUpperInvariant(),
                        Name = component.Name?.Trim() ?? string.Empty,
                        Type = string.IsNullOrWhiteSpace(component.Type)
                            ? "Assignment"
                            : component.Type,
                        Weight = component.Weight,
                        MaxScore = component.MaxScore,
                        IsRequired = component.IsRequired,
                        MinimumScore = component.MinimumScore,
                        IsFinalCondition = component.IsFinalCondition,
                        CloMappings = (component.CloMappings ?? [])
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
            RoundingMode = string.IsNullOrWhiteSpace(source.RoundingMode)
                ? "Normal"
                : source.RoundingMode,
            DecimalPlaces = source.DecimalPlaces,
            EffectiveFrom = source.EffectiveFrom,
            Active = source.Active
        };
}
