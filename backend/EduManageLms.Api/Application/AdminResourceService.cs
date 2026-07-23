using EduManageLms.Api.Common;
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
        int page,
        int size,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);

        var collection = Collection(resource);
        var filter = Builders<BsonDocument>.Filter.Ne("isDeleted", true);
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
        CancellationToken ct)
    {
        SanitizeBody(resource, body);
        NormalizeBody(resource, body);
        ValidateResourceBody(resource, body, updating: false);
        await EnsureUniqueAsync(resource, body, currentId: null, ct);
        await EnsureSingleCurrentAcademicYearAsync(resource, body, currentId: null, ct);

        var document = ToBson(body);
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
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
        await Collection(resource).InsertOneAsync(document, cancellationToken: ct);
        return Map(document);
    }

    public async Task<Dictionary<string, object?>> UpdateAsync(
        string resource,
        string id,
        Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var oid = ParseId(id);
        var existing = await Collection(resource)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", oid))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

        SanitizeBody(resource, body);
        NormalizeBody(resource, body);
        ValidateResourceBody(resource, body, updating: true);
        await EnsureUniqueAsync(resource, body, id, ct);
        await EnsureSingleCurrentAcademicYearAsync(resource, body, id, ct);

        var updateDocument = ToBson(body);
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

        var result = await Collection(resource).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            new BsonDocument("$set", updateDocument),
            cancellationToken: ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
        _ = existing;
        return await GetAsync(resource, id, ct);
    }

    public async Task DeleteAsync(string resource, string id, CancellationToken ct)
    {
        var oid = ParseId(id);
        await EnsureCanDeleteAsync(resource, oid, ct);

        var result = await Collection(resource).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", oid),
            Builders<BsonDocument>.Update
                .Set("isDeleted", true)
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
    }

    public async Task RestoreAsync(string resource, string id, CancellationToken ct)
    {
        var result = await Collection(resource).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", ParseId(id)),
            Builders<BsonDocument>.Update
                .Set("isDeleted", false)
                .Set("updatedAt", DateTime.UtcNow),
            cancellationToken: ct);

        if (result.MatchedCount == 0) throw new NotFoundException();
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
            "class-sections" => ["classSectionCode", "courseId", "lecturerId"],
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
                         "passwordHash", "refreshTokens", "failedLoginCount", "lockedUntil", "lastLoginAt"
                     })
                body.Remove(protectedField);
        }
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
    }

    private async Task EnsureUniqueAsync(
        string resource,
        Dictionary<string, object?> body,
        string? currentId,
        CancellationToken ct)
    {
        string[] fields = resource switch
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

    private static Dictionary<string, object?> Map(BsonDocument document) =>
        document.Elements.ToDictionary(
            element => element.Name == "_id" ? "id" : element.Name,
            element => BsonTypeMapper.MapToDotNetValue(element.Value));

    private static BsonDocument ToBson(Dictionary<string, object?> values) =>
        new(values.Where(pair => pair.Value is not null)
            .Select(pair => new BsonElement(pair.Key, ConvertValue(pair.Value))));

    private static BsonValue ConvertValue(object? value)
    {
        if (value is null) return BsonNull.Value;
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
