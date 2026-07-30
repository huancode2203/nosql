using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class AdminResourceService(MongoContext db) : IAdminResourceService
{
    private static readonly Dictionary<string, string> ResourceCollections = new(StringComparer.OrdinalIgnoreCase)
    {
        ["users"] = "users",
        ["students"] = "students",
        ["lecturers"] = "lecturers",
        ["faculties"] = "faculties",
        ["programs"] = "programs",
        ["academic-years"] = "academicYears",
        ["semesters"] = "semesters",
        ["courses"] = "courses",
        ["class-sections"] = "classSections",
        ["notifications"] = "notifications",
        ["system-settings"] = "systemSettings",
        ["grade-reopen-requests"] = "gradeReopenRequests"
    };

    public async Task<PagedResult<Dictionary<string, object?>>> ListAsync(
        string resource,
        string? search,
        bool deletedOnly,
        int page,
        int size,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);

        var collection = Collection(resource);
        var filter = deletedOnly
            ? Builders<BsonDocument>.Filter.Eq("isDeleted", true)
            : Builders<BsonDocument>.Filter.Ne("isDeleted", true);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(RegexEscape(search.Trim()), "i");
            var fields = SearchFields(resource);
            filter &= Builders<BsonDocument>.Filter.Or(
                fields.Select(field => Builders<BsonDocument>.Filter.Regex(field, regex)));
        }

        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var documents = await collection.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
            .Skip((page - 1) * size)
            .Limit(size)
            .ToListAsync(ct);

        return PagedResult<Dictionary<string, object?>>.Create(
            documents.Select(Map).ToList(), page, size, total);
    }

    public async Task<Dictionary<string, object?>> GetAsync(string resource, string id, CancellationToken ct)
    {
        var document = await Collection(resource)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", ParseId(id)))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        return Map(document);
    }

    public async Task<Dictionary<string, object?>> CreateAsync(
        string resource,
        Dictionary<string, object?> body,
        AdminActor actor,
        CancellationToken ct)
    {
        SanitizeBody(resource, body);
        NormalizeBody(resource, body);
        await ResolveReferencesAsync(resource, body, ct);
        ValidateResourceBody(resource, body, updating: false);
        ValidateSpecializedFields(resource, body);
        await EnsureUniqueAsync(resource, body, currentId: null, ct);

        var document = ToBson(body);
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            document["permissionsConfigured"] = body.ContainsKey("permissions");
            var password = GetString(body, "password");
            if (string.IsNullOrWhiteSpace(password)) password = "Lms@123456";
            ValidatePassword(password);
            document.Remove("password");
            document["passwordHash"] = BCrypt.Net.BCrypt.HashPassword(password);
            document["failedLoginCount"] = 0;
            document["refreshTokens"] = new BsonArray();
        }

        document["_id"] = ObjectId.GenerateNewId();
        document["createdAt"] = DateTime.UtcNow;
        document["updatedAt"] = DateTime.UtcNow;
        document["isDeleted"] = false;
        await InTransactionAsync(
            async (session, token) =>
            {
                await EnsureSingleCurrentAcademicYearAsync(
                    session,
                    resource,
                    body,
                    currentId: null,
                    token);
                await Collection(resource).InsertOneAsync(
                    session,
                    document,
                    cancellationToken: token);
                await SynchronizeAccountAndProfileAsync(
                    session,
                    resource,
                    document,
                    token);
                await WriteAuditAsync(
                    session,
                    actor,
                    $"CREATE_{resource.ToUpperInvariant().Replace('-', '_')}",
                    resource,
                    document["_id"].AsObjectId.ToString(),
                    null,
                    Map(document),
                    token);
                return true;
            },
            ct);
        return Map(document);
    }

    public async Task<Dictionary<string, object?>> UpdateAsync(
        string resource,
        string id,
        Dictionary<string, object?> body,
        AdminActor actor,
        CancellationToken ct)
    {
        var oid = ParseId(id);
        var existing = await Collection(resource)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        SanitizeBody(resource, body);
        NormalizeBody(resource, body);
        await ResolveReferencesAsync(resource, body, ct);
        ValidateResourceBody(resource, body, updating: true);
        ValidateSpecializedFields(resource, body);
        await EnsureUniqueAsync(resource, body, id, ct);

        var updateDocument = ToBson(body);
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase)
            && body.ContainsKey("permissions"))
            updateDocument["permissionsConfigured"] = true;
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase) &&
            body.TryGetValue("password", out var passwordValue) &&
            !string.IsNullOrWhiteSpace(ValueAsString(passwordValue)))
        {
            var password = ValueAsString(passwordValue)!;
            ValidatePassword(password);
            updateDocument["passwordHash"] = BCrypt.Net.BCrypt.HashPassword(password);
        }

        updateDocument.Remove("password");
        updateDocument["updatedAt"] = DateTime.UtcNow;

        if (updateDocument.ElementCount == 1 && updateDocument.Contains("updatedAt"))
            throw new AppException("Không có dữ liệu hợp lệ để cập nhật");

        var afterDocument = existing.DeepClone().AsBsonDocument;
        foreach (var element in updateDocument)
            afterDocument[element.Name] = element.Value;

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                await EnsureSingleCurrentAcademicYearAsync(
                    session,
                    resource,
                    body,
                    id,
                    token);
                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    new BsonDocument("$set", updateDocument),
                    cancellationToken: token);

                if (updateResult.MatchedCount == 0)
                    throw new NotFoundException();

                await SynchronizeAccountAndProfileAsync(
                    session,
                    resource,
                    afterDocument,
                    token);
                await WriteAuditAsync(
                    session,
                    actor,
                    $"UPDATE_{resource.ToUpperInvariant().Replace('-', '_')}",
                    resource,
                    id,
                    Map(existing),
                    Map(afterDocument),
                    token);

                return updateResult;
            },
            ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
        return await GetAsync(resource, id, ct);
    }

    public async Task DeleteAsync(
        string resource,
        string id,
        AdminActor actor,
        CancellationToken ct)
    {
        var oid = ParseId(id);
        await EnsureCanDeleteAsync(resource, oid, ct);

        var existing = await Collection(resource)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    Builders<BsonDocument>.Update
                        .Set("isDeleted", true)
                        .Set("updatedAt", DateTime.UtcNow),
                    cancellationToken: token);
                await WriteAuditAsync(
                    session,
                    actor,
                    $"DELETE_{resource.ToUpperInvariant().Replace('-', '_')}",
                    resource,
                    id,
                    Map(existing),
                    new { IsDeleted = true },
                    token);
                return updateResult;
            },
            ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
    }

    public async Task RestoreAsync(
        string resource,
        string id,
        AdminActor actor,
        CancellationToken ct)
    {
        var oid = ParseId(id);
        var existing = await Collection(resource)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        await EnsureRestoreConflictsAsync(resource, existing, ct);

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid),
                    Builders<BsonDocument>.Update
                        .Set("isDeleted", false)
                        .Set("updatedAt", DateTime.UtcNow),
                    cancellationToken: token);
                await WriteAuditAsync(
                    session,
                    actor,
                    $"RESTORE_{resource.ToUpperInvariant().Replace('-', '_')}",
                    resource,
                    id,
                    Map(existing),
                    new { IsDeleted = false },
                    token);
                return updateResult;
            },
            ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
    }

    private async Task ResolveReferencesAsync(
        string resource,
        Dictionary<string, object?> body,
        CancellationToken ct)
    {
        if (resource.Equals("semesters", StringComparison.OrdinalIgnoreCase)
            && TryId(body, "academicYearId", out var academicYearId))
        {
            var year = await FindRequiredAsync(
                "academicYears",
                academicYearId,
                "năm học",
                ct);
            body["academicYearId"] = year["_id"].AsObjectId.ToString();
            body["academicYearName"] = year.GetValue(
                "academicYearName",
                year.GetValue("academicYearCode", "")).AsString;
        }

        if (resource.Equals("courses", StringComparison.OrdinalIgnoreCase)
            && TryId(body, "facultyId", out var courseFacultyId))
        {
            body["faculty"] = await BuildFacultySnapshotAsync(
                courseFacultyId,
                ct);
            body.Remove("facultyId");
        }

        if (resource.Equals("programs", StringComparison.OrdinalIgnoreCase)
            && TryId(body, "facultyId", out var programFacultyId))
        {
            body["faculty"] = await BuildFacultySnapshotAsync(
                programFacultyId,
                ct);
            body.Remove("facultyId");
        }

        if (resource.Equals("students", StringComparison.OrdinalIgnoreCase))
        {
            if (TryId(body, "facultyId", out var studentFacultyId))
            {
                body["faculty"] = await BuildFacultySnapshotAsync(
                    studentFacultyId,
                    ct);
                body.Remove("facultyId");
            }
            if (TryId(body, "programId", out var studentProgramId))
            {
                body["program"] = await BuildProgramSnapshotAsync(
                    studentProgramId,
                    ct);
                body.Remove("programId");
            }
        }

        if (resource.Equals("lecturers", StringComparison.OrdinalIgnoreCase)
            && TryId(body, "facultyId", out var lecturerFacultyId))
        {
            body["faculty"] = await BuildFacultySnapshotAsync(
                lecturerFacultyId,
                ct);
            body.Remove("facultyId");
        }

        if (resource.Equals("class-sections", StringComparison.OrdinalIgnoreCase))
        {
            if (TryId(body, "courseId", out var courseId))
            {
                var course = await FindRequiredAsync(
                    "courses",
                    courseId,
                    "môn học",
                    ct);
                body["courseId"] = course["_id"].AsObjectId.ToString();
                body["courseCode"] = course.GetValue("courseCode", "").AsString;
                body["courseName"] = course.GetValue("courseName", "").AsString;
            }

            if (TryId(body, "lecturerId", out var lecturerId))
            {
                var lecturer = await FindRequiredAsync(
                    "lecturers",
                    lecturerId,
                    "giảng viên",
                    ct);
                body["lecturerId"] = lecturer["_id"].AsObjectId.ToString();
                body["lecturerCode"] = lecturer.GetValue(
                    "lecturerCode",
                    "").AsString;
                body["lecturerName"] = lecturer.GetValue(
                    "fullName",
                    "").AsString;
            }

            if (TryId(body, "semesterId", out var semesterId))
            {
                var semester = await FindRequiredAsync(
                    "semesters",
                    semesterId,
                    "học kỳ",
                    ct);
                body["semesterId"] = semester["_id"].AsObjectId.ToString();
                body["semesterCode"] = semester.GetValue(
                    "semesterCode",
                    "").AsString;
                body["semesterName"] = semester.GetValue(
                    "semesterName",
                    "").AsString;
                body["academicYearId"] = IdAsString(
                    semester.GetValue("academicYearId", BsonNull.Value));
                body["academicYearName"] = semester.GetValue(
                    "academicYearName",
                    "").AsString;
            }
        }

        if (resource.Equals("notifications", StringComparison.OrdinalIgnoreCase))
            await ResolveNotificationRecipientsAsync(body, ct);
    }

    private async Task<BsonDocument> BuildFacultySnapshotAsync(
        string facultyId,
        CancellationToken ct)
    {
        var faculty = await FindRequiredAsync(
            "faculties",
            facultyId,
            "khoa",
            ct);
        return new BsonDocument
        {
            ["facultyId"] = faculty["_id"],
            ["facultyCode"] = faculty.GetValue("facultyCode", ""),
            ["facultyName"] = faculty.GetValue("facultyName", "")
        };
    }

    private async Task<BsonDocument> BuildProgramSnapshotAsync(
        string programId,
        CancellationToken ct)
    {
        var program = await FindRequiredAsync(
            "programs",
            programId,
            "chương trình đào tạo",
            ct);
        return new BsonDocument
        {
            ["programId"] = program["_id"],
            ["programCode"] = program.GetValue("programCode", ""),
            ["programName"] = program.GetValue("programName", ""),
            ["requiredCredits"] = program.GetValue("requiredCredits", 0)
        };
    }

    private async Task<BsonDocument> FindRequiredAsync(
        string collectionName,
        string id,
        string label,
        CancellationToken ct)
    {
        var document = await db.Database
            .GetCollection<BsonDocument>(collectionName)
            .Find(
                Builders<BsonDocument>.Filter.Eq("_id", ParseId(id))
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct);
        return document
            ?? throw new AppException($"Không tìm thấy {label} đã chọn");
    }

    private async Task ResolveNotificationRecipientsAsync(
        Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var audienceType = GetString(body, "audienceType")?.Trim() ?? "All";
        if (audienceType is not ("Faculty" or "ClassSection"))
            return;

        var audienceId = GetString(body, "audienceId");
        if (string.IsNullOrWhiteSpace(audienceId))
            throw new AppException("Phải chọn khoa hoặc lớp học phần nhận thông báo");

        var studentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lecturerCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (audienceType == "Faculty")
        {
            var faculty = await FindRequiredAsync(
                "faculties",
                audienceId,
                "khoa",
                ct);
            body["audienceName"] = faculty.GetValue(
                "facultyName",
                faculty.GetValue("facultyCode", "")).AsString;
            var facultyObjectId = ParseId(audienceId);
            var facultyFilter = Builders<BsonDocument>.Filter.In(
                "faculty.facultyId",
                new BsonValue[]
                {
                    new BsonObjectId(facultyObjectId),
                    new BsonString(audienceId)
                });

            var students = await db.Database
                .GetCollection<BsonDocument>("students")
                .Find(facultyFilter & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
                .Project(Builders<BsonDocument>.Projection.Include("studentCode"))
                .ToListAsync(ct);
            foreach (var student in students)
                studentCodes.Add(student.GetValue("studentCode", "").AsString);

            var lecturers = await db.Database
                .GetCollection<BsonDocument>("lecturers")
                .Find(facultyFilter & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
                .Project(Builders<BsonDocument>.Projection.Include("lecturerCode"))
                .ToListAsync(ct);
            foreach (var lecturer in lecturers)
                lecturerCodes.Add(lecturer.GetValue("lecturerCode", "").AsString);
        }
        else
        {
            var section = await db.ClassSections
                .Find(x => x.Id == audienceId && !x.IsDeleted)
                .FirstOrDefaultAsync(ct)
                ?? throw new AppException("Không tìm thấy lớp học phần đã chọn");
            body["audienceName"] = section.ClassSectionCode;
            foreach (var student in section.Students)
                studentCodes.Add(student.StudentCode);
            lecturerCodes.Add(section.LecturerCode);
        }

        var userDocuments = db.Database.GetCollection<BsonDocument>("users");
        var userFilter = Builders<BsonDocument>.Filter.In(
                "studentCode",
                studentCodes.Select(code => new BsonString(code)))
            | Builders<BsonDocument>.Filter.In(
                "lecturerCode",
                lecturerCodes.Select(code => new BsonString(code)));
        var recipients = await userDocuments
            .Find(userFilter & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync(ct);

        body["recipientIds"] = recipients
            .Select(item => item["_id"].AsObjectId.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task SynchronizeAccountAndProfileAsync(
        IClientSessionHandle session,
        string resource,
        BsonDocument document,
        CancellationToken ct)
    {
        if (resource.Equals("students", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureLinkedUserAsync(
                session,
                "Student",
                "studentCode",
                document.GetValue("studentCode", "").AsString,
                document.GetValue("fullName", "").AsString,
                document.GetValue("email", "").AsString,
                document.GetValue("status", "Studying").AsString,
                ct);
            return;
        }

        if (resource.Equals("lecturers", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureLinkedUserAsync(
                session,
                "Lecturer",
                "lecturerCode",
                document.GetValue("lecturerCode", "").AsString,
                document.GetValue("fullName", "").AsString,
                document.GetValue("email", "").AsString,
                document.GetValue("status", "Active").AsString,
                ct);
            return;
        }

        if (!resource.Equals("users", StringComparison.OrdinalIgnoreCase))
            return;

        var role = document.GetValue("role", "").AsString;
        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureProfileAsync(
                session,
                "students",
                "studentCode",
                document.GetValue("studentCode", "").AsString,
                document,
                "Studying",
                ct);
        }
        else if (role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureProfileAsync(
                session,
                "lecturers",
                "lecturerCode",
                document.GetValue("lecturerCode", "").AsString,
                document,
                "Active",
                ct);
        }
    }

    private async Task EnsureLinkedUserAsync(
        IClientSessionHandle session,
        string role,
        string codeField,
        string code,
        string fullName,
        string email,
        string profileStatus,
        CancellationToken ct)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(codeField, code)
            & Builders<BsonDocument>.Filter.Ne("isDeleted", true);
        var users = db.Database.GetCollection<BsonDocument>("users");
        var existing = await users.Find(session, filter).FirstOrDefaultAsync(ct);

        var active = profileStatus is not ("Inactive" or "Suspended" or "Graduated");
        if (existing is null)
        {
            var account = new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["username"] = code.ToLowerInvariant(),
                ["email"] = email.ToLowerInvariant(),
                ["fullName"] = fullName,
                ["passwordHash"] = BCrypt.Net.BCrypt.HashPassword("Lms@123456"),
                ["role"] = role,
                ["permissions"] = new BsonArray(),
                ["permissionsConfigured"] = false,
                ["status"] = active ? "Active" : "Inactive",
                [codeField] = code,
                ["failedLoginCount"] = 0,
                ["refreshTokens"] = new BsonArray(),
                ["createdAt"] = DateTime.UtcNow,
                ["updatedAt"] = DateTime.UtcNow,
                ["isDeleted"] = false
            };
            await users.InsertOneAsync(session, account, cancellationToken: ct);
            return;
        }

        await users.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]),
            Builders<BsonDocument>.Update
                .Set("fullName", fullName)
                .Set("email", email.ToLowerInvariant())
                .Set("role", role)
                .Set("status", active ? "Active" : "Inactive")
                .Set(codeField, code)
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);
    }

    private async Task EnsureProfileAsync(
        IClientSessionHandle session,
        string collectionName,
        string codeField,
        string code,
        BsonDocument user,
        string defaultStatus,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new AppException(
                codeField == "studentCode"
                    ? "Tài khoản sinh viên phải có mã sinh viên"
                    : "Tài khoản giảng viên phải có mã giảng viên");

        var collection = db.Database.GetCollection<BsonDocument>(collectionName);
        var existing = await collection
            .Find(
                session,
                Builders<BsonDocument>.Filter.Eq(codeField, code)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            var profile = new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                [codeField] = code,
                ["fullName"] = user.GetValue("fullName", ""),
                ["email"] = user.GetValue("email", ""),
                ["status"] = defaultStatus,
                ["createdAt"] = DateTime.UtcNow,
                ["updatedAt"] = DateTime.UtcNow,
                ["isDeleted"] = false
            };
            if (collectionName == "students")
            {
                profile["faculty"] = new BsonDocument();
                profile["program"] = new BsonDocument();
                profile["academicRecords"] = new BsonArray();
            }
            else
            {
                profile["faculty"] = new BsonDocument();
                profile["specializations"] = new BsonArray();
            }
            await collection.InsertOneAsync(session, profile, cancellationToken: ct);
            return;
        }

        await collection.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]),
            Builders<BsonDocument>.Update
                .Set("fullName", user.GetValue("fullName", ""))
                .Set("email", user.GetValue("email", ""))
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);
    }

    private async Task WriteAuditAsync(
        IClientSessionHandle session,
        AdminActor actor,
        string action,
        string entity,
        string entityId,
        object? before,
        object? after,
        CancellationToken ct)
    {
        await db.AuditLogs.InsertOneAsync(
            session,
            new AuditLog
            {
                UserId = actor.UserId,
                UserName = actor.UserName,
                Role = actor.Role,
                Action = action,
                Entity = entity,
                EntityId = entityId,
                Before = before,
                After = after,
                IpAddress = actor.IpAddress,
                UserAgent = actor.UserAgent,
                Result = "Success"
            },
            cancellationToken: ct);
    }

    private async Task<T> InTransactionAsync<T>(
        Func<IClientSessionHandle, CancellationToken, Task<T>> action,
        CancellationToken ct)
    {
        using var session = await db.Client.StartSessionAsync(
            cancellationToken: ct);
        return await session.WithTransactionAsync(
            (current, token) => action(current, token),
            new TransactionOptions(
                ReadConcern.Snapshot,
                ReadPreference.Primary,
                WriteConcern.WMajority),
            ct);
    }

    private async Task EnsureRestoreConflictsAsync(
        string resource,
        BsonDocument document,
        CancellationToken ct)
    {
        var fields = UniqueFields(resource);
        foreach (var field in fields)
        {
            if (!document.TryGetValue(field, out var value)
                || value.IsBsonNull
                || (value.IsString && string.IsNullOrWhiteSpace(value.AsString)))
                continue;
            var duplicate = await Collection(resource)
                .Find(
                    Builders<BsonDocument>.Filter.Eq(field, value)
                    & Builders<BsonDocument>.Filter.Ne("_id", document["_id"])
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
                .FirstOrDefaultAsync(ct);
            if (duplicate is not null)
                throw new ConflictException(
                    $"Không thể khôi phục vì {field} đang được bản ghi khác sử dụng");
        }
    }

    private IMongoCollection<BsonDocument> Collection(string resource)
    {
        if (!ResourceCollections.TryGetValue(resource, out var collectionName))
            throw new NotFoundException("Tài nguyên không được hỗ trợ");
        return db.Database.GetCollection<BsonDocument>(collectionName);
    }

    private static IEnumerable<string> SearchFields(string resource) => resource switch
    {
        "students" => ["studentCode", "fullName", "email", "administrativeClass", "faculty.facultyName"],
        "lecturers" => ["lecturerCode", "fullName", "email", "department", "faculty.facultyName"],
        "faculties" => ["facultyCode", "facultyName", "deanName"],
        "programs" => ["programCode", "programName", "faculty.facultyName", "applicableCohort"],
        "academic-years" => ["academicYearCode", "academicYearName", "status"],
        "semesters" => ["semesterCode", "semesterName", "academicYearName", "status"],
        "courses" => ["courseCode", "courseName", "englishName", "faculty.facultyName"],
        "class-sections" => ["classSectionCode", "courseName", "lecturerName", "semesterName"],
        "notifications" => ["title", "content", "type", "priority", "audienceType"],
        "system-settings" => ["key", "group", "description"],
        "grade-reopen-requests" => ["classSectionCode", "lecturerCode", "reason", "status"],
        _ => ["username", "fullName", "email", "role", "status"]
    };

    private static void ValidateResourceBody(
        string resource,
        Dictionary<string, object?> body,
        bool updating)
    {
        if (updating)
        {
            if (body.Count == 0) throw new AppException("Không có dữ liệu để cập nhật");
            return;
        }

        string[] required = resource switch
        {
            "faculties" => ["facultyCode", "facultyName"],
            "programs" => ["programCode", "programName"],
            "academic-years" => ["academicYearCode", "academicYearName"],
            "semesters" => ["semesterCode", "semesterName", "academicYearId"],
            "courses" => ["courseCode", "courseName"],
            "class-sections" => ["classSectionCode", "courseId", "lecturerId", "semesterId"],
            "notifications" => ["title", "content"],
            "system-settings" => ["key", "value"],
            "users" => ["username", "email", "fullName", "role"],
            "students" => ["studentCode", "fullName", "email"],
            "lecturers" => ["lecturerCode", "fullName", "email"],
            _ => []
        };

        var missing = required
            .Where(field => !body.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(ValueAsString(value)))
            .ToList();
        if (missing.Count > 0)
            throw new AppException($"Thiếu trường bắt buộc: {string.Join(", ", missing)}");

        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            var role = GetString(body, "role");
            if (role == "Student"
                && string.IsNullOrWhiteSpace(GetString(body, "studentCode")))
                throw new AppException("Tài khoản sinh viên phải có mã sinh viên");
            if (role == "Lecturer"
                && string.IsNullOrWhiteSpace(GetString(body, "lecturerCode")))
                throw new AppException("Tài khoản giảng viên phải có mã giảng viên");
        }
    }

    private static void ValidateSpecializedFields(
        string resource,
        Dictionary<string, object?> body)
    {
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            var role = GetString(body, "role");
            if (role is not null && role is not ("Admin" or "Lecturer" or "Student"))
                throw new AppException("Vai trò tài khoản không hợp lệ");

            if (body.TryGetValue("permissions", out var rawPermissions))
            {
                var permissions = StringList(rawPermissions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var invalid = permissions
                    .Where(permission => !AppPermissions.All.Contains(permission))
                    .ToArray();
                if (invalid.Length > 0)
                    throw new AppException(
                        $"Quyền không hợp lệ: {string.Join(", ", invalid)}");
                body["permissions"] = permissions;
            }
        }

        if (resource.Equals("academic-years", StringComparison.OrdinalIgnoreCase))
            ValidateDateOrder(body, "startDate", "endDate", "Thời gian năm học");

        if (resource.Equals("semesters", StringComparison.OrdinalIgnoreCase))
        {
            ValidateDateOrder(body, "startDate", "endDate", "Thời gian học kỳ");
            ValidateDateOrder(
                body,
                "gradeEntryStart",
                "gradeEntryEnd",
                "Thời gian nhập điểm");
        }

        if (resource.Equals("class-sections", StringComparison.OrdinalIgnoreCase))
            ValidateDateOrder(body, "startDate", "endDate", "Thời gian lớp học phần");

        if (resource.Equals("notifications", StringComparison.OrdinalIgnoreCase))
        {
            var audienceType = GetString(body, "audienceType") ?? "All";
            var allowed = new[]
            {
                "All", "Student", "Lecturer", "Admin", "SpecificUsers",
                "Faculty", "ClassSection"
            };
            if (!allowed.Contains(audienceType, StringComparer.OrdinalIgnoreCase))
                throw new AppException("Phạm vi người nhận thông báo không hợp lệ");
        }

        if (resource.Equals("system-settings", StringComparison.OrdinalIgnoreCase))
            ValidateSystemSetting(body);
    }

    private static void ValidateDateOrder(
        Dictionary<string, object?> body,
        string startKey,
        string endKey,
        string label)
    {
        if (!TryDate(body, startKey, out var start)
            || !TryDate(body, endKey, out var end))
            return;
        if (start >= end)
            throw new AppException($"{label}: ngày bắt đầu phải trước ngày kết thúc");
    }

    private static void ValidateSystemSetting(
        Dictionary<string, object?> body)
    {
        var key = GetString(body, "key")?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            throw new AppException("Khóa cấu hình không được để trống");

        var value = GetString(body, "value")?.Trim() ?? string.Empty;
        if (key.Equals("Grade.PassingScore", StringComparison.OrdinalIgnoreCase))
            ValidateNumberRange(value, 0, 10, "Điểm đạt");
        else if (key.Equals("Clo.DefaultThreshold", StringComparison.OrdinalIgnoreCase))
            ValidateNumberRange(value, 0, 100, "Ngưỡng đạt CLO");
        else if (key.Equals("Security.MaxFailedLogins", StringComparison.OrdinalIgnoreCase))
            ValidateNumberRange(value, 1, 20, "Số lần đăng nhập sai tối đa");
        else if (key.Equals("Grade.DecimalPlaces", StringComparison.OrdinalIgnoreCase))
            ValidateNumberRange(value, 0, 4, "Số chữ số thập phân");
        else if (key.EndsWith(".Enabled", StringComparison.OrdinalIgnoreCase)
                 && !bool.TryParse(value, out _))
            throw new AppException("Giá trị cấu hình bật/tắt phải là true hoặc false");
    }

    private static void ValidateNumberRange(
        string raw,
        double minimum,
        double maximum,
        string label)
    {
        if (!double.TryParse(
                raw.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value)
            || value < minimum
            || value > maximum)
            throw new AppException(
                $"{label} phải nằm trong khoảng {minimum:0.##}–{maximum:0.##}");
    }

    private static bool TryDate(
        Dictionary<string, object?> body,
        string key,
        out DateTime value)
    {
        value = default;
        return body.TryGetValue(key, out var raw)
            && DateTime.TryParse(ValueAsString(raw), out value);
    }

    private static void SanitizeBody(string resource, Dictionary<string, object?> body)
    {
        if (body.Keys.Any(key => key.StartsWith('$') || key.Contains('.')))
            throw new AppException("Tên trường dữ liệu không hợp lệ");

        foreach (var protectedField in new[] { "id", "_id", "createdAt", "updatedAt", "isDeleted" })
            body.Remove(protectedField);

        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var protectedField in new[]
                     {
                         "passwordHash", "refreshTokens", "failedLoginCount", "lockedUntil", "lastLoginAt",
                         "permissionsConfigured", "avatarUrl"
                     })
                body.Remove(protectedField);
        }

        if (!resource.Equals("users", StringComparison.OrdinalIgnoreCase))
            body.Remove("permissions");
    }

    private static void NormalizeBody(string resource, Dictionary<string, object?> body)
    {
        foreach (var field in new[] { "email", "username" })
        {
            if (!body.TryGetValue(field, out var raw)) continue;
            var value = ValueAsString(raw)?.Trim().ToLowerInvariant();
            if (value is not null) body[field] = value;
        }

        foreach (var field in resource switch
                 {
                     "students" => new[] { "studentCode" },
                     "lecturers" => new[] { "lecturerCode" },
                     "faculties" => new[] { "facultyCode" },
                     "programs" => new[] { "programCode" },
                     "academic-years" => new[] { "academicYearCode" },
                     "semesters" => new[] { "semesterCode" },
                     "courses" => new[] { "courseCode" },
                     "class-sections" => new[] { "classSectionCode" },
                     _ => Array.Empty<string>()
                 })
        {
            if (!body.TryGetValue(field, out var raw)) continue;
            var value = ValueAsString(raw)?.Trim().ToUpperInvariant();
            if (value is not null) body[field] = value;
        }

        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var field in new[] { "studentCode", "lecturerCode" })
            {
                if (!body.TryGetValue(field, out var raw)) continue;
                var value = ValueAsString(raw)?.Trim().ToUpperInvariant();
                if (value is not null) body[field] = value;
            }
        }
    }

    private async Task EnsureUniqueAsync(
        string resource,
        Dictionary<string, object?> body,
        string? currentId,
        CancellationToken ct)
    {
        var fields = UniqueFields(resource);

        foreach (var field in fields)
        {
            if (!body.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(ValueAsString(raw))) continue;
            var filter = Builders<BsonDocument>.Filter.Eq(field, ConvertValue(raw));
            if (!string.IsNullOrWhiteSpace(currentId))
                filter &= Builders<BsonDocument>.Filter.Ne("_id", ParseId(currentId));

            var existing = await Collection(resource).Find(filter).FirstOrDefaultAsync(ct);
            if (existing is not null) throw new AppException($"Giá trị {field} đã tồn tại");
        }

        if (resource.Equals("semesters", StringComparison.OrdinalIgnoreCase) &&
            body.TryGetValue("semesterCode", out var semesterCode) &&
            body.TryGetValue("academicYearId", out var academicYearId))
        {
            var filter = Builders<BsonDocument>.Filter.Eq("semesterCode", ConvertValue(semesterCode)) &
                         Builders<BsonDocument>.Filter.Eq("academicYearId", ConvertValue(academicYearId));
            if (!string.IsNullOrWhiteSpace(currentId))
                filter &= Builders<BsonDocument>.Filter.Ne("_id", ParseId(currentId));
            if (await Collection(resource).Find(filter).FirstOrDefaultAsync(ct) is not null)
                throw new AppException("Học kỳ đã tồn tại trong năm học này");
        }
    }

    private async Task EnsureSingleCurrentAcademicYearAsync(
        IClientSessionHandle session,
        string resource,
        Dictionary<string, object?> body,
        string? currentId,
        CancellationToken ct)
    {
        if (!resource.Equals("academic-years", StringComparison.OrdinalIgnoreCase)) return;
        if (!body.TryGetValue("isCurrent", out var raw) || !ToBoolean(raw)) return;

        var filter = Builders<BsonDocument>.Filter.Eq("isCurrent", true);
        if (!string.IsNullOrWhiteSpace(currentId))
            filter &= Builders<BsonDocument>.Filter.Ne("_id", ParseId(currentId));

        await Collection(resource).UpdateManyAsync(
            session,
            filter,
            Builders<BsonDocument>.Update
                .Set("isCurrent", false)
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);
    }

    private async Task EnsureCanDeleteAsync(string resource, ObjectId id, CancellationToken ct)
    {
        var references = resource switch
        {
            "faculties" => new[]
            {
                ("students", "faculty.facultyId"),
                ("lecturers", "faculty.facultyId"),
                ("courses", "faculty.facultyId"),
                ("programs", "faculty.facultyId")
            },
            "programs" => new[] { ("students", "program.programId") },
            "academic-years" => new[] { ("semesters", "academicYearId"), ("classSections", "academicYearId") },
            "semesters" => new[] { ("classSections", "semesterId") },
            "courses" => new[] { ("classSections", "courseId") },
            "lecturers" => new[] { ("classSections", "lecturerId") },
            "class-sections" => new[] { ("assignments", "classSectionId"), ("materials", "classSectionId") },
            _ => Array.Empty<(string collection, string field)>()
        };

        foreach (var (collectionName, field) in references)
        {
            var collection = db.Database.GetCollection<BsonDocument>(collectionName);
            var filter = Builders<BsonDocument>.Filter.In(field, new BsonValue[] { new BsonObjectId(id), new BsonString(id.ToString()) });
            if (await collection.Find(filter).FirstOrDefaultAsync(ct) is not null)
                throw new AppException("Không thể xóa vì dữ liệu đang được sử dụng. Hãy ngừng hoạt động hoặc đóng dữ liệu thay vì xóa.", 409);
        }
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new AppException("Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt");
    }

    private static bool ToBoolean(object? value)
    {
        if (value is bool boolean) return boolean;
        if (value is System.Text.Json.JsonElement json &&
            json.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            return json.GetBoolean();
        return bool.TryParse(ValueAsString(value), out var parsed) && parsed;
    }

    private static string? GetString(Dictionary<string, object?> body, string key) =>
        body.TryGetValue(key, out var value) ? ValueAsString(value) : null;

    private static string[] UniqueFields(string resource) => resource switch
    {
        "users" => ["username", "email"],
        "students" => ["studentCode", "email"],
        "lecturers" => ["lecturerCode", "email"],
        "faculties" => ["facultyCode"],
        "programs" => ["programCode"],
        "academic-years" => ["academicYearCode"],
        "courses" => ["courseCode"],
        "class-sections" => ["classSectionCode"],
        "system-settings" => ["key"],
        _ => []
    };

    private static bool TryId(
        Dictionary<string, object?> body,
        string key,
        out string id)
    {
        id = GetString(body, key)?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            return false;
        _ = ParseId(id);
        return true;
    }

    private static string IdAsString(BsonValue value) =>
        value switch
        {
            { IsObjectId: true } => value.AsObjectId.ToString(),
            { IsString: true } => value.AsString,
            _ => string.Empty
        };

    private static IReadOnlyCollection<string> StringList(object? value)
    {
        if (value is null)
            return [];
        if (value is IEnumerable<string> strings)
            return strings.Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToArray();
        if (value is BsonArray bsonArray)
            return bsonArray
                .Where(item => item.IsString)
                .Select(item => item.AsString.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
        if (value is System.Text.Json.JsonElement json
            && json.ValueKind == System.Text.Json.JsonValueKind.Array)
            return json.EnumerateArray()
                .Where(item => item.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(item => item.GetString()?.Trim() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToArray();

        var single = ValueAsString(value)?.Trim();
        return string.IsNullOrWhiteSpace(single)
            ? []
            : single.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
    }

    private static string? ValueAsString(object? value)
    {
        if (value is null) return null;
        if (value is System.Text.Json.JsonElement json)
        {
            return json.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => json.GetString(),
                System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => null,
                _ => json.ToString()
            };
        }
        return value.ToString();
    }

    private static string RegexEscape(string value) => System.Text.RegularExpressions.Regex.Escape(value);
    private static ObjectId ParseId(string id) =>
        ObjectId.TryParse(id, out var oid) ? oid : throw new AppException("Id không hợp lệ");

    private static Dictionary<string, object?> Map(BsonDocument document)
    {
        var result = document.Elements.ToDictionary(
            element => element.Name == "_id" ? "id" : element.Name,
            element => BsonTypeMapper.MapToDotNetValue(element.Value));
        foreach (var sensitive in new[]
                 {
                     "passwordHash", "refreshTokens", "failedLoginCount",
                     "lockedUntil"
                 })
            result.Remove(sensitive);

        FlattenSnapshot(result, document, "faculty", "facultyId", "facultyName");
        FlattenSnapshot(result, document, "program", "programId", "programName");
        return result;
    }

    private static void FlattenSnapshot(
        Dictionary<string, object?> result,
        BsonDocument document,
        string snapshotName,
        string idName,
        string displayName)
    {
        if (!document.TryGetValue(snapshotName, out var snapshotValue)
            || !snapshotValue.IsBsonDocument)
            return;
        var snapshot = snapshotValue.AsBsonDocument;
        if (snapshot.TryGetValue(idName, out var id))
            result[idName] = IdAsString(id);
        if (snapshot.TryGetValue(displayName, out var display))
            result[displayName] = display.IsString ? display.AsString : display.ToString();
    }

    private static BsonDocument ToBson(Dictionary<string, object?> values) =>
        new(values.Where(pair => pair.Value is not null)
            .Select(pair => new BsonElement(pair.Key, ConvertValue(pair.Value))));

    private static BsonValue ConvertValue(object? value)
    {
        if (value is null) return BsonNull.Value;
        if (value is BsonValue bsonValue) return bsonValue;
        if (value is IEnumerable<string> strings)
            return new BsonArray(strings);
        if (value is System.Text.Json.JsonElement json)
        {
            return json.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String =>
                    json.TryGetDateTime(out var date)
                        ? new BsonDateTime(date)
                        : new BsonString(json.GetString() ?? string.Empty),
                System.Text.Json.JsonValueKind.Number =>
                    json.TryGetInt64(out var integer)
                        ? new BsonInt64(integer)
                        : new BsonDouble(json.GetDouble()),
                System.Text.Json.JsonValueKind.True => BsonBoolean.True,
                System.Text.Json.JsonValueKind.False => BsonBoolean.False,
                System.Text.Json.JsonValueKind.Array =>
                    new BsonArray(json.EnumerateArray().Select(item => ConvertValue(item))),
                System.Text.Json.JsonValueKind.Object =>
                    new BsonDocument(json.EnumerateObject()
                        .Select(property => new BsonElement(property.Name, ConvertValue(property.Value)))),
                _ => BsonNull.Value
            };
        }
        return BsonValue.Create(value);
    }
}
