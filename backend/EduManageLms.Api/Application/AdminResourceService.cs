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
        NormalizeRolePermissions(resource, body);
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
                    body.TryGetValue("isCurrent", out var rawCurrent)
                    && ToBoolean(rawCurrent),
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
                    null,
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
            .Find(
                Builders<BsonDocument>.Filter.Eq("_id", oid)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        SanitizeBody(resource, body);
        NormalizeBody(resource, body);
        await ResolveReferencesAsync(resource, body, ct);
        NormalizeRolePermissions(resource, body, existing);
        ValidateResourceBody(resource, body, updating: true);
        ValidateSpecializedFields(resource, body);
        await EnsureUniqueAsync(resource, body, id, ct);

        var fieldsToUnset = body
            .Where(pair => IsClearValue(pair.Value))
            .Select(pair => pair.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        fieldsToUnset.Remove("password");
        updateDocument["updatedAt"] = DateTime.UtcNow;

        if (updateDocument.ElementCount == 1
            && updateDocument.Contains("updatedAt")
            && fieldsToUnset.Count == 0)
            throw new AppException("Không có dữ liệu hợp lệ để cập nhật");

        var afterDocument = existing.DeepClone().AsBsonDocument;
        foreach (var element in updateDocument)
            afterDocument[element.Name] = element.Value;
        foreach (var field in fieldsToUnset)
            afterDocument.Remove(field);
        ValidateCompleteDocument(resource, afterDocument);

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                await EnsureSingleCurrentAcademicYearAsync(
                    session,
                    resource,
                    body.TryGetValue("isCurrent", out var rawCurrent)
                    && ToBoolean(rawCurrent),
                    id,
                    token);
                var update = new BsonDocument(
                    "$set",
                    updateDocument);
                if (fieldsToUnset.Count > 0)
                {
                    update["$unset"] = new BsonDocument(
                        fieldsToUnset.Select(field => new BsonElement(field, "")));
                }

                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid)
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true),
                    update,
                    cancellationToken: token);

                if (updateResult.MatchedCount == 0)
                    throw new NotFoundException();

                await SynchronizeAccountAndProfileAsync(
                    session,
                    resource,
                    afterDocument,
                    existing,
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
            .Find(
                Builders<BsonDocument>.Filter.Eq("_id", oid)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid)
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true),
                    Builders<BsonDocument>.Update
                        .Set("isDeleted", true)
                        .Set("updatedAt", DateTime.UtcNow),
                    cancellationToken: token);
                await SetLinkedAccountDeletedStateAsync(
                    session,
                    resource,
                    existing,
                    deleted: true,
                    token);
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
            .Find(
                Builders<BsonDocument>.Filter.Eq("_id", oid)
                & Builders<BsonDocument>.Filter.Eq("isDeleted", true))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        await EnsureRestoreConflictsAsync(resource, existing, ct);

        var result = await InTransactionAsync(
            async (session, token) =>
            {
                var restoringCurrentYear =
                    existing.GetValue("isCurrent", false) is
                    { IsBoolean: true } currentValue
                    && currentValue.AsBoolean;
                await EnsureSingleCurrentAcademicYearAsync(
                    session,
                    resource,
                    restoringCurrentYear,
                    id,
                    token);
                var updateResult = await Collection(resource).UpdateOneAsync(
                    session,
                    Builders<BsonDocument>.Filter.Eq("_id", oid)
                    & Builders<BsonDocument>.Filter.Eq("isDeleted", true),
                    Builders<BsonDocument>.Update
                        .Set("isDeleted", false)
                        .Set("updatedAt", DateTime.UtcNow),
                    cancellationToken: token);
                await SetLinkedAccountDeletedStateAsync(
                    session,
                    resource,
                    existing,
                    deleted: false,
                    token);
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
            body["academicYearId"] = year["_id"];
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
        else if (resource.Equals("courses", StringComparison.OrdinalIgnoreCase)
                 && body.TryGetValue("facultyId", out var courseFaculty)
                 && IsClearValue(courseFaculty))
        {
            body["faculty"] = null;
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
        else if (resource.Equals("programs", StringComparison.OrdinalIgnoreCase)
                 && body.TryGetValue("facultyId", out var programFaculty)
                 && IsClearValue(programFaculty))
        {
            body["faculty"] = null;
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
            else if (body.TryGetValue("facultyId", out var studentFaculty)
                     && IsClearValue(studentFaculty))
            {
                body["faculty"] = null;
                body.Remove("facultyId");
            }
            if (TryId(body, "programId", out var studentProgramId))
            {
                body["program"] = await BuildProgramSnapshotAsync(
                    studentProgramId,
                    ct);
                body.Remove("programId");
            }
            else if (body.TryGetValue("programId", out var studentProgram)
                     && IsClearValue(studentProgram))
            {
                body["program"] = null;
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
        else if (resource.Equals("lecturers", StringComparison.OrdinalIgnoreCase)
                 && body.TryGetValue("facultyId", out var lecturerFaculty)
                 && IsClearValue(lecturerFaculty))
        {
            body["faculty"] = null;
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
                body["courseId"] = course["_id"];
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
                body["lecturerId"] = lecturer["_id"];
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
                body["semesterId"] = semester["_id"];
                body["semesterCode"] = semester.GetValue(
                    "semesterCode",
                    "").AsString;
                body["semesterName"] = semester.GetValue(
                    "semesterName",
                    "").AsString;
                body["academicYearId"] = semester.GetValue(
                    "academicYearId",
                    BsonNull.Value);
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
        if (audienceType.Equals(
                "SpecificUsers",
                StringComparison.OrdinalIgnoreCase))
        {
            var recipientIds = StringList(
                    body.GetValueOrDefault("recipientIds"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (recipientIds.Length == 0)
                throw new AppException("Phải chọn ít nhất một người nhận thông báo");
            if (recipientIds.Any(id => !ObjectId.TryParse(id, out _)))
                throw new AppException("Danh sách người nhận chứa ID không hợp lệ");

            var objectIds = recipientIds.Select(ObjectId.Parse).ToArray();
            var users = db.Database.GetCollection<BsonDocument>("users");
            var existingIds = await users.Find(
                    Builders<BsonDocument>.Filter.In("_id", objectIds)
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
                .Project(Builders<BsonDocument>.Projection.Include("_id"))
                .ToListAsync(ct);
            if (existingIds.Count != recipientIds.Length)
                throw new AppException(
                    "Một hoặc nhiều tài khoản nhận thông báo không tồn tại");

            body["recipientIds"] = recipientIds;
            body["audienceId"] = null;
            body["audienceName"] = null;
            return;
        }

        if (!audienceType.Equals("Faculty", StringComparison.OrdinalIgnoreCase)
            && !audienceType.Equals(
                "ClassSection",
                StringComparison.OrdinalIgnoreCase))
        {
            body["recipientIds"] = Array.Empty<string>();
            body["audienceId"] = null;
            body["audienceName"] = null;
            return;
        }

        var audienceId = GetString(body, "audienceId");
        if (string.IsNullOrWhiteSpace(audienceId))
            throw new AppException("Phải chọn khoa hoặc lớp học phần nhận thông báo");

        body["audienceId"] = ParseId(audienceId);

        var studentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lecturerCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (audienceType.Equals("Faculty", StringComparison.OrdinalIgnoreCase))
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
        BsonDocument? previousDocument,
        CancellationToken ct)
    {
        if (resource.Equals("students", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureLinkedUserAsync(
                session,
                "Student",
                "studentCode",
                document.GetValue("studentCode", "").AsString,
                previousDocument?.GetValue("studentCode", "").AsString,
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
                previousDocument?.GetValue("lecturerCode", "").AsString,
                document.GetValue("fullName", "").AsString,
                document.GetValue("email", "").AsString,
                document.GetValue("status", "Active").AsString,
                ct);
            return;
        }

        if (!resource.Equals("users", StringComparison.OrdinalIgnoreCase))
            return;

        var role = document.GetValue("role", "").AsString;
        var previousRole = previousDocument?.GetValue("role", "").AsString;
        var roleChanged = previousRole is not null
            && !previousRole.Equals(role, StringComparison.OrdinalIgnoreCase);
        await DeactivatePreviousRoleProfileAsync(
            session,
            previousDocument,
            role,
            ct);
        if (role.Equals("Student", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureProfileAsync(
                session,
                "students",
                "studentCode",
                document.GetValue("studentCode", "").AsString,
                document,
                "Studying",
                roleChanged,
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
                roleChanged,
                ct);
        }
    }

    private async Task DeactivatePreviousRoleProfileAsync(
        IClientSessionHandle session,
        BsonDocument? previousUser,
        string currentRole,
        CancellationToken ct)
    {
        if (previousUser is null)
            return;

        var previousRole = previousUser.GetValue("role", "").AsString;
        if (previousRole.Equals(currentRole, StringComparison.OrdinalIgnoreCase))
            return;

        var (collectionName, codeField, inactiveStatus) =
            previousRole.ToLowerInvariant() switch
            {
                "student" => ("students", "studentCode", "Suspended"),
                "lecturer" => ("lecturers", "lecturerCode", "Inactive"),
                _ => (string.Empty, string.Empty, string.Empty)
            };
        if (collectionName.Length == 0)
            return;

        var code = previousUser.GetValue(codeField, "").AsString;
        if (string.IsNullOrWhiteSpace(code))
            return;

        var collection = db.Database.GetCollection<BsonDocument>(collectionName);
        var profile = await collection.Find(
                session,
                Builders<BsonDocument>.Filter.Eq(codeField, code)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct);
        if (profile is null)
            return;

        var previousStatus = profile.GetValue("status", inactiveStatus).AsString;
        await collection.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", profile["_id"]),
            Builders<BsonDocument>.Update
                .Set("status", inactiveStatus)
                .Set("accountLinked", false)
                .Set("accountUnlinkedAt", DateTime.UtcNow)
                .Set("accountUnlinkedPreviousStatus", previousStatus)
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);
    }

    private async Task EnsureLinkedUserAsync(
        IClientSessionHandle session,
        string role,
        string codeField,
        string code,
        string? previousCode,
        string fullName,
        string email,
        string profileStatus,
        CancellationToken ct)
    {
        var users = db.Database.GetCollection<BsonDocument>("users");
        var activeFilter = Builders<BsonDocument>.Filter.Ne(
            "isDeleted",
            true);
        BsonDocument? existing = null;
        if (!string.IsNullOrWhiteSpace(previousCode))
        {
            existing = await users.Find(
                    session,
                    Builders<BsonDocument>.Filter.Eq(
                        codeField,
                        previousCode)
                    & activeFilter)
                .FirstOrDefaultAsync(ct);
        }
        existing ??= await users.Find(
                session,
                Builders<BsonDocument>.Filter.Eq(codeField, code)
                & activeFilter)
            .FirstOrDefaultAsync(ct);

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

        var codeConflict = await users.Find(
                session,
                Builders<BsonDocument>.Filter.Eq(codeField, code)
                & Builders<BsonDocument>.Filter.Ne("_id", existing["_id"])
                & activeFilter)
            .FirstOrDefaultAsync(ct);
        if (codeConflict is not null)
            throw new ConflictException(
                $"Mã {code} đang được một tài khoản khác sử dụng");

        var update = Builders<BsonDocument>.Update
            .Set("fullName", fullName)
            .Set("email", email.ToLowerInvariant())
            .Set("role", role)
            .Set("status", active ? "Active" : "Inactive")
            .Set(codeField, code)
            .Set("updatedAt", DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(previousCode)
            && !previousCode.Equals(code, StringComparison.OrdinalIgnoreCase)
            && existing.GetValue("username", "").AsString.Equals(
                previousCode.ToLowerInvariant(),
                StringComparison.OrdinalIgnoreCase))
        {
            update = update.Set("username", code.ToLowerInvariant());
        }

        await users.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]),
            update,
            cancellationToken: ct);
    }

    private async Task EnsureProfileAsync(
        IClientSessionHandle session,
        string collectionName,
        string codeField,
        string code,
        BsonDocument user,
        string defaultStatus,
        bool reactivate,
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
                ["accountLinked"] = true,
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

        var update = Builders<BsonDocument>.Update
            .Set("fullName", user.GetValue("fullName", ""))
            .Set("email", user.GetValue("email", ""))
            .Set("accountLinked", true)
            .Set("updatedAt", DateTime.UtcNow);
        if (reactivate
            && existing.Contains("accountUnlinkedPreviousStatus"))
        {
            update = update
                .Set(
                    "status",
                    existing.GetValue(
                        "accountUnlinkedPreviousStatus",
                        defaultStatus))
                .Unset("accountUnlinkedAt")
                .Unset("accountUnlinkedPreviousStatus");
        }

        await collection.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]),
            update,
            cancellationToken: ct);
    }

    private async Task SetLinkedAccountDeletedStateAsync(
        IClientSessionHandle session,
        string resource,
        BsonDocument profile,
        bool deleted,
        CancellationToken ct)
    {
        var (codeField, role, defaultStatus) = resource.ToLowerInvariant() switch
        {
            "students" => ("studentCode", "Student", "Studying"),
            "lecturers" => ("lecturerCode", "Lecturer", "Active"),
            _ => (string.Empty, string.Empty, string.Empty)
        };
        if (codeField.Length == 0)
            return;

        var code = profile.GetValue(codeField, "").AsString;
        if (string.IsNullOrWhiteSpace(code))
            return;

        var users = db.Database.GetCollection<BsonDocument>("users");
        var codeFilter = Builders<BsonDocument>.Filter.Eq(codeField, code);

        if (deleted)
        {
            await users.UpdateManyAsync(
                session,
                codeFilter & Builders<BsonDocument>.Filter.Ne("isDeleted", true),
                Builders<BsonDocument>.Update
                    .Set("isDeleted", true)
                    .Set("status", "Inactive")
                    .Set("updatedAt", DateTime.UtcNow),
                cancellationToken: ct);
            return;
        }

        var account = await users.Find(session, codeFilter)
            .Sort(Builders<BsonDocument>.Sort.Descending("updatedAt"))
            .FirstOrDefaultAsync(ct);
        if (account is null)
        {
            await EnsureLinkedUserAsync(
                session,
                role,
                codeField,
                code,
                null,
                profile.GetValue("fullName", "").AsString,
                profile.GetValue("email", "").AsString,
                profile.GetValue("status", defaultStatus).AsString,
                ct);
            return;
        }

        var email = profile.GetValue("email", "").AsString.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(email))
        {
            var conflict = await users.Find(
                    session,
                    Builders<BsonDocument>.Filter.Eq("email", email)
                    & Builders<BsonDocument>.Filter.Ne("_id", account["_id"])
                    & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
                .FirstOrDefaultAsync(ct);
            if (conflict is not null)
                throw new ConflictException(
                    "Không thể khôi phục vì email đang được tài khoản khác sử dụng");
        }

        var profileStatus = profile.GetValue("status", defaultStatus).AsString;
        var active = profileStatus is not ("Inactive" or "Suspended" or "Graduated");
        await users.UpdateOneAsync(
            session,
            Builders<BsonDocument>.Filter.Eq("_id", account["_id"]),
            Builders<BsonDocument>.Update
                .Set("isDeleted", false)
                .Set("status", active ? "Active" : "Inactive")
                .Set("role", role)
                .Set(codeField, code)
                .Set("fullName", profile.GetValue("fullName", ""))
                .Set("email", email)
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

        var required = RequiredFields(resource);

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

    private static void ValidateCompleteDocument(
        string resource,
        BsonDocument document)
    {
        var missing = RequiredFields(resource)
            .Where(field =>
                !document.TryGetValue(field, out var value)
                || value.IsBsonNull
                || (value.IsString && string.IsNullOrWhiteSpace(value.AsString)))
            .ToList();
        if (missing.Count > 0)
            throw new AppException(
                $"Không được xóa trường bắt buộc: {string.Join(", ", missing)}");

        if (resource.Equals(
                "academic-years",
                StringComparison.OrdinalIgnoreCase))
        {
            ValidateDateOrder(
                document,
                "startDate",
                "endDate",
                "Thời gian năm học");
        }
        else if (resource.Equals(
                     "semesters",
                     StringComparison.OrdinalIgnoreCase))
        {
            ValidateDateOrder(
                document,
                "startDate",
                "endDate",
                "Thời gian học kỳ");
            ValidateDateOrder(
                document,
                "gradeEntryStart",
                "gradeEntryEnd",
                "Thời gian nhập điểm");
        }
        else if (resource.Equals(
                     "class-sections",
                     StringComparison.OrdinalIgnoreCase))
        {
            ValidateDateOrder(
                document,
                "startDate",
                "endDate",
                "Thời gian lớp học phần");
        }
        else if (resource.Equals(
                     "notifications",
                     StringComparison.OrdinalIgnoreCase))
        {
            ValidateDateOrder(
                document,
                "displayFrom",
                "expiresAt",
                "Thời gian hiển thị thông báo");
        }

        if (!resource.Equals("users", StringComparison.OrdinalIgnoreCase))
            return;

        var role = document.GetValue("role", "").AsString;
        if (role == "Student"
            && string.IsNullOrWhiteSpace(
                document.GetValue("studentCode", "").AsString))
            throw new AppException("Tài khoản sinh viên phải có mã sinh viên");
        if (role == "Lecturer"
            && string.IsNullOrWhiteSpace(
                document.GetValue("lecturerCode", "").AsString))
            throw new AppException("Tài khoản giảng viên phải có mã giảng viên");
    }

    private static string[] RequiredFields(string resource) => resource switch
    {
        "faculties" => ["facultyCode", "facultyName"],
        "programs" => ["programCode", "programName"],
        "academic-years" =>
            ["academicYearCode", "academicYearName", "startDate", "endDate"],
        "semesters" =>
            [
                "semesterCode", "semesterName", "academicYearId",
                "startDate", "endDate"
            ],
        "courses" => ["courseCode", "courseName", "credits"],
        "class-sections" => ["classSectionCode", "courseId", "lecturerId", "semesterId"],
        "notifications" => ["title", "content"],
        "system-settings" => ["key", "value"],
        "users" => ["username", "email", "fullName", "role"],
        "students" => ["studentCode", "fullName", "email"],
        "lecturers" => ["lecturerCode", "fullName", "email"],
        _ => []
    };

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

        if (resource is "users" or "students" or "lecturers")
            ValidateEmail(body);

        var status = GetString(body, "status");
        var allowedStatuses = resource switch
        {
            "users" => new[] { "Active", "Inactive" },
            "students" => new[] { "Studying", "Suspended", "Graduated" },
            "class-sections" => new[]
            {
                "Draft", "Submitted", "Published", "Locked", "Reopened"
            },
            "faculties" or "programs" or "academic-years" or "semesters"
                or "courses" or "lecturers" => new[] { "Active", "Inactive" },
            _ => Array.Empty<string>()
        };
        if (status is not null
            && allowedStatuses.Length > 0
            && !allowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw new AppException("Trạng thái dữ liệu không hợp lệ");

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
        {
            ValidateDateOrder(body, "startDate", "endDate", "Thời gian lớp học phần");
            ValidateIntegerField(body, "capacity", 1, 1000, "Sĩ số tối đa");
        }

        if (resource.Equals("courses", StringComparison.OrdinalIgnoreCase))
        {
            ValidateIntegerField(body, "credits", 1, 30, "Số tín chỉ");
            ValidateIntegerField(
                body,
                "theoryPeriods",
                0,
                1000,
                "Số tiết lý thuyết");
            ValidateIntegerField(
                body,
                "practicePeriods",
                0,
                1000,
                "Số tiết thực hành");
        }

        if (resource.Equals("programs", StringComparison.OrdinalIgnoreCase))
        {
            ValidateIntegerField(
                body,
                "requiredCredits",
                0,
                1000,
                "Tổng tín chỉ");
            ValidateIntegerField(
                body,
                "requiredCompulsoryCredits",
                0,
                1000,
                "Tín chỉ bắt buộc");
            ValidateIntegerField(
                body,
                "requiredElectiveCredits",
                0,
                1000,
                "Tín chỉ tự chọn");
            ValidateIntegerField(
                body,
                "durationYears",
                1,
                20,
                "Số năm đào tạo");
        }

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

            var notificationStatus = GetString(body, "status");
            if (notificationStatus is not null && notificationStatus is not ("Draft" or "Sent"))
                throw new AppException("Trạng thái thông báo không hợp lệ");
            var priority = GetString(body, "priority");
            if (priority is not null && priority is not ("Low" or "Normal" or "High"))
                throw new AppException("Mức ưu tiên thông báo không hợp lệ");
            var type = GetString(body, "type");
            if (type is not null
                && type is not ("General" or "Academic" or "Grade" or "Emergency"))
                throw new AppException("Loại thông báo không hợp lệ");
            ValidateDateOrder(
                body,
                "displayFrom",
                "expiresAt",
                "Thời gian hiển thị thông báo");
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

    private static void ValidateDateOrder(
        BsonDocument document,
        string startKey,
        string endKey,
        string label)
    {
        if (!TryDate(document, startKey, out var start)
            || !TryDate(document, endKey, out var end))
            return;
        if (start >= end)
            throw new AppException(
                $"{label}: ngày bắt đầu phải trước ngày kết thúc");
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

    private static void ValidateEmail(Dictionary<string, object?> body)
    {
        var email = GetString(body, "email")?.Trim();
        if (string.IsNullOrWhiteSpace(email))
            return;
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed)
            || !parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase))
            throw new AppException("Địa chỉ email không hợp lệ");
    }

    private static void NormalizeRolePermissions(
        string resource,
        Dictionary<string, object?> body,
        BsonDocument? existing = null)
    {
        if (!resource.Equals("users", StringComparison.OrdinalIgnoreCase))
            return;
        var existingRole = existing?.GetValue("role", "").AsString;
        var role = GetString(body, "role") ?? existingRole;
        if (AdminUserPolicy.RequiresExplicitAdminPermissions(
                existingRole,
                role,
                body.ContainsKey("permissions")))
            throw new AppException(
                existing is null
                    ? "Tài khoản Admin mới phải được cấu hình quyền cụ thể"
                    : "Khi chuyển sang vai trò Admin phải cấu hình quyền cụ thể");
        if (role is not null
            && !role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            body["permissions"] = Array.Empty<string>();

        foreach (var field in AdminUserPolicy.ObsoleteLinkFields(role))
        {
            if (existing is null)
                body.Remove(field);
            else
                body[field] = null;
        }
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
            || !double.IsFinite(value)
            || value < minimum
            || value > maximum)
            throw new AppException(
                $"{label} phải nằm trong khoảng {minimum:0.##}–{maximum:0.##}");
    }

    private static void ValidateIntegerField(
        Dictionary<string, object?> body,
        string key,
        long minimum,
        long maximum,
        string label)
    {
        if (!body.TryGetValue(key, out var raw) || IsClearValue(raw))
            return;
        if (!long.TryParse(ValueAsString(raw), out var value)
            || value < minimum
            || value > maximum)
        {
            throw new AppException(
                $"{label} phải là số nguyên từ {minimum} đến {maximum}");
        }
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

    private static bool TryDate(
        BsonDocument document,
        string key,
        out DateTime value)
    {
        value = default;
        if (!document.TryGetValue(key, out var raw) || raw.IsBsonNull)
            return false;
        if (raw.IsBsonDateTime)
        {
            value = raw.AsBsonDateTime.ToUniversalTime();
            return true;
        }
        return raw.IsString && DateTime.TryParse(raw.AsString, out value);
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
            var filter = Builders<BsonDocument>.Filter.Eq(field, ConvertValue(raw))
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true);
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
                         Builders<BsonDocument>.Filter.Eq("academicYearId", ConvertValue(academicYearId)) &
                         Builders<BsonDocument>.Filter.Ne("isDeleted", true);
            if (!string.IsNullOrWhiteSpace(currentId))
                filter &= Builders<BsonDocument>.Filter.Ne("_id", ParseId(currentId));
            if (await Collection(resource).Find(filter).FirstOrDefaultAsync(ct) is not null)
                throw new AppException("Học kỳ đã tồn tại trong năm học này");
        }
    }

    private async Task EnsureSingleCurrentAcademicYearAsync(
        IClientSessionHandle session,
        string resource,
        bool isCurrent,
        string? currentId,
        CancellationToken ct)
    {
        if (!resource.Equals("academic-years", StringComparison.OrdinalIgnoreCase)) return;
        if (!isCurrent) return;

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
            var filter = Builders<BsonDocument>.Filter.In(
                    field,
                    new BsonValue[]
                    {
                        new BsonObjectId(id),
                        new BsonString(id.ToString())
                    })
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true);
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
        "users" => ["username", "email", "studentCode", "lecturerCode"],
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
        foreach (var field in new[]
                 {
                     "academicYearId", "courseId", "lecturerId", "semesterId",
                     "audienceId"
                 })
        {
            if (document.TryGetValue(field, out var value)
                && !value.IsBsonNull)
                result[field] = IdAsString(value);
        }
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
        new(values.Where(pair => !IsClearValue(pair.Value))
            .Select(pair => new BsonElement(pair.Key, ConvertValue(pair.Value))));

    private static bool IsClearValue(object? value)
    {
        if (value is null)
            return true;
        if (value is string text)
            return string.IsNullOrWhiteSpace(text);
        if (value is System.Text.Json.JsonElement json)
        {
            return json.ValueKind is System.Text.Json.JsonValueKind.Null
                    or System.Text.Json.JsonValueKind.Undefined
                || (json.ValueKind == System.Text.Json.JsonValueKind.String
                    && string.IsNullOrWhiteSpace(json.GetString()));
        }
        return false;
    }

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
