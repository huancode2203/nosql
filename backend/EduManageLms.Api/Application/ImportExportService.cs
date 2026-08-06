using ClosedXML.Excel;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class ImportExportService(
    MongoContext db,
    IAdminResourceService adminResources) : IImportExportService
{
    public async Task<byte[]> ExportResourceAsync(string resource, CancellationToken ct)
    {
        var collectionName = resource switch
        {
            "users" => "users",
            "students" => "students",
            "lecturers" => "lecturers",
            "faculties" => "faculties",
            "programs" => "programs",
            "academic-years" => "academicYears",
            "semesters" => "semesters",
            "courses" => "courses",
            "class-sections" => "classSections",
            "notifications" => "notifications",
            "system-settings" => "systemSettings",
            "audit-logs" => "auditLogs",
            _ => throw new NotFoundException("Tài nguyên không hỗ trợ export")
        };
        var documents = await db.Database.GetCollection<BsonDocument>(collectionName)
            .Find(Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .Limit(10000)
            .ToListAsync(ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        if (documents.Count == 0)
        {
            sheet.Cell(1, 1).Value = "Không có dữ liệu";
        }
        else
        {
            var keys = documents.SelectMany(x => x.Names).Where(x => x is not "passwordHash" and not "refreshTokens").Distinct().ToList();
            for (var i = 0; i < keys.Count; i++) sheet.Cell(1, i + 1).Value = keys[i] == "_id" ? "id" : keys[i];
            sheet.Row(1).Style.Font.SetBold();
            for (var row = 0; row < documents.Count; row++)
            {
                for (var col = 0; col < keys.Count; col++)
                {
                    var value = documents[row].GetValue(keys[col], BsonNull.Value);
                    sheet.Cell(row + 2, col + 1).Value = value.IsBsonNull ? "" : value.IsString ? value.AsString : value.IsNumeric ? value.ToDouble() : value.IsBoolean ? value.AsBoolean : value.IsValidDateTime ? value.ToUniversalTime() : value.ToJson();
                }
            }
            sheet.Columns().AdjustToContents();
            sheet.SheetView.FreezeRows(1);
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ImportPreviewDto> ImportStudentsAsync(IFormFile file, bool commit, CancellationToken ct)
    {
        if (file.Length == 0) throw new AppException("File import rỗng");
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var header = sheet.RowsUsed().FirstOrDefault(x => string.Equals(x.Cell(1).GetString().Trim(), "studentCode", StringComparison.OrdinalIgnoreCase));
        if (header is null) throw new AppException("Không tìm thấy dòng tiêu đề studentCode");
        var headers = header.CellsUsed().ToDictionary(x => x.GetString().Trim(), x => x.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "studentCode", "fullName", "email" }) if (!headers.ContainsKey(required)) throw new AppException($"Thiếu cột {required}");
        var existingCodes = (await db.Students.Find(x => !x.IsDeleted).Project(x => x.StudentCode).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingEmails = (await db.Users.Find(x => !x.IsDeleted).Project(x => x.Email).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportRowResult>();
        var newStudents = new List<Student>();
        var newUsers = new List<User>();
        foreach (var row in sheet.RowsUsed().Where(x => x.RowNumber() > header.RowNumber()))
        {
            var code = Read(row, headers, "studentCode").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code)) continue;
            var fullName = Read(row, headers, "fullName");
            var email = Read(row, headers, "email").ToLowerInvariant();
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(fullName)) errors.Add("Họ tên không được trống");
            if (!email.Contains('@')) errors.Add("Email không hợp lệ");
            if (existingCodes.Contains(code) || newStudents.Any(x => x.StudentCode.Equals(code, StringComparison.OrdinalIgnoreCase))) errors.Add("Mã sinh viên bị trùng");
            if (existingEmails.Contains(email) || newUsers.Any(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase))) errors.Add("Email bị trùng");
            var data = new Dictionary<string, object?>
            {
                ["studentCode"] = code,
                ["fullName"] = fullName,
                ["email"] = email,
                ["phone"] = Read(row, headers, "phone"),
                ["administrativeClass"] = Read(row, headers, "administrativeClass"),
                ["cohort"] = Read(row, headers, "cohort"),
                ["status"] = Read(row, headers, "academicStatus")
            };
            results.Add(new ImportRowResult(row.RowNumber(), errors.Count == 0, errors, data));
            if (errors.Count > 0) continue;
            var facultyCode = Read(row, headers, "facultyCode");
            var programCode = Read(row, headers, "programCode");
            var student = new Student
            {
                StudentCode = code,
                FullName = fullName,
                Email = email,
                Phone = Read(row, headers, "phone"),
                Address = Read(row, headers, "address"),
                Gender = Read(row, headers, "gender"),
                Cohort = Read(row, headers, "cohort"),
                AdministrativeClass = Read(row, headers, "administrativeClass"),
                Status = string.IsNullOrWhiteSpace(Read(row, headers, "academicStatus")) ? "Studying" : Read(row, headers, "academicStatus"),
                Faculty = new FacultySnapshot { FacultyCode = facultyCode, FacultyName = facultyCode },
                Program = new ProgramSnapshot { ProgramCode = programCode, ProgramName = programCode }
            };
            if (DateTime.TryParse(Read(row, headers, "dateOfBirth"), out var dob)) student.DateOfBirth = dob;
            newStudents.Add(student);
            newUsers.Add(new User { Username = code.ToLowerInvariant(), Email = email, FullName = fullName, PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lms@123456"), Role = "Student", StudentCode = code });
        }
        if (commit)
        {
            if (results.Any(x => !x.Valid)) throw new AppException("File còn dòng không hợp lệ; hãy sửa trước khi import");
            if (newStudents.Count > 0) await db.Students.InsertManyAsync(newStudents, cancellationToken: ct);
            if (newUsers.Count > 0) await db.Users.InsertManyAsync(newUsers, cancellationToken: ct);
        }
        return new ImportPreviewDto(results.Count, results.Count(x => x.Valid), results.Count(x => !x.Valid), results);
    }

    public async Task<ImportPreviewDto> ImportResourceAsync(
        string resource,
        IFormFile file,
        bool commit,
        AdminActor actor,
        CancellationToken ct)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "students",
            "lecturers",
            "faculties",
            "programs",
            "academic-years",
            "semesters",
            "courses",
            "class-sections"
        };
        if (!supported.Contains(resource))
            throw new NotFoundException("Tài nguyên không hỗ trợ import");
        if (file.Length == 0)
            throw new AppException("File import rỗng");
        if (file.Length > 20L * 1024 * 1024)
            throw new AppException("File import vượt quá 20 MB");

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var header = sheet.RowsUsed().FirstOrDefault()
            ?? throw new AppException("File không có dòng tiêu đề");
        var headers = header.CellsUsed()
            .Select(cell => new
            {
                Name = cell.GetString().Trim(),
                Column = cell.Address.ColumnNumber
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(
                item => item.Name,
                item => item.Column,
                StringComparer.OrdinalIgnoreCase);

        var required = RequiredImportFields(resource);
        foreach (var alternatives in required)
            if (!alternatives.Any(headers.ContainsKey))
                throw new AppException(
                    $"Thiếu cột {string.Join(" hoặc ", alternatives)}");

        var rows = new List<ImportRowResult>();
        var preparedRows = new List<Dictionary<string, object?>>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueKey = UniqueImportField(resource);

        foreach (var row in sheet.RowsUsed()
                     .Where(item => item.RowNumber() > header.RowNumber()))
        {
            var data = headers.ToDictionary(
                pair => pair.Key,
                pair => ConvertCellValue(row.Cell(pair.Value), pair.Key),
                StringComparer.OrdinalIgnoreCase);
            NormalizeImportValues(resource, data);
            var errors = new List<string>();
            await ResolveReferenceCodesAsync(
                resource,
                data,
                errors,
                ct);
            RemoveImportOnlyReferenceCodes(resource, data);

            var key = data.GetValueOrDefault(uniqueKey)?.ToString()?.Trim()
                      ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)
                && data.Values.All(value => string.IsNullOrWhiteSpace(value?.ToString())))
                continue;

            foreach (var alternatives in required)
                if (!alternatives.Any(field =>
                        data.TryGetValue(field, out var value)
                        && !string.IsNullOrWhiteSpace(value?.ToString())))
                    errors.Add($"Thiếu {string.Join(" hoặc ", alternatives)}");

            if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key))
                errors.Add($"{uniqueKey} bị trùng trong file");
            if (!string.IsNullOrWhiteSpace(key)
                && await ExistingKeyAsync(
                    resource,
                    uniqueKey,
                    key,
                    data,
                    ct))
                errors.Add($"{uniqueKey} đã tồn tại trong hệ thống");
            if (data.TryGetValue("email", out var email)
                && !string.IsNullOrWhiteSpace(email?.ToString())
                && !email.ToString()!.Contains('@'))
                errors.Add("Email không hợp lệ");
            if (resource is "students" or "lecturers"
                && data.TryGetValue("email", out var accountEmail)
                && !string.IsNullOrWhiteSpace(accountEmail?.ToString())
                && await db.Users.Find(
                        user => user.Email == accountEmail.ToString()
                                && !user.IsDeleted)
                    .AnyAsync(ct))
                errors.Add("Email đăng nhập đã được tài khoản khác sử dụng");

            Dictionary<string, object?>? prepared = null;
            if (errors.Count == 0)
            {
                try
                {
                    // Xem trước và ghi thật dùng chung toàn bộ pipeline chuẩn bị dữ liệu.
                    prepared = await adminResources.PrepareCreateAsync(
                        resource,
                        data,
                        ct);
                }
                catch (AppException exception)
                {
                    errors.Add(exception.Message);
                }
            }

            rows.Add(
                new ImportRowResult(
                    row.RowNumber(),
                    errors.Count == 0,
                    errors,
                    data));
            if (prepared is not null && errors.Count == 0)
                preparedRows.Add(prepared);
        }

        if (commit)
        {
            if (rows.Any(row => !row.Valid))
                throw new AppException(
                    "File còn dòng không hợp lệ; hãy sửa trước khi import");
            foreach (var data in preparedRows)
                await adminResources.CreateAsync(
                    resource,
                    data,
                    actor,
                    ct);
        }

        return new ImportPreviewDto(
            rows.Count,
            rows.Count(row => row.Valid),
            rows.Count(row => !row.Valid),
            rows);
    }

    private static string[][] RequiredImportFields(string resource) =>
        resource switch
        {
            "students" =>
            [
                ["studentCode"], ["fullName"], ["email"],
                ["facultyId", "facultyCode"],
                ["programId", "programCode"]
            ],
            "lecturers" =>
            [
                ["lecturerCode"], ["fullName"], ["email"]
            ],
            "faculties" =>
            [
                ["facultyCode"], ["facultyName"]
            ],
            "programs" =>
            [
                ["programCode"], ["programName"]
            ],
            "academic-years" =>
            [
                ["academicYearCode"], ["academicYearName"],
                ["startDate"], ["endDate"]
            ],
            "semesters" =>
            [
                ["semesterCode"], ["semesterName"],
                ["academicYearId", "academicYearCode"],
                ["startDate"], ["endDate"]
            ],
            "courses" =>
            [
                ["courseCode"], ["courseName"], ["credits"]
            ],
            "class-sections" =>
            [
                ["classSectionCode"],
                ["courseId", "courseCode"],
                ["lecturerId", "lecturerCode"],
                ["semesterId", "semesterCode"]
            ],
            _ => []
        };

    private static string UniqueImportField(string resource) =>
        resource switch
        {
            "students" => "studentCode",
            "lecturers" => "lecturerCode",
            "faculties" => "facultyCode",
            "programs" => "programCode",
            "academic-years" => "academicYearCode",
            "semesters" => "semesterCode",
            "courses" => "courseCode",
            "class-sections" => "classSectionCode",
            _ => "id"
        };

    private async Task ResolveReferenceCodesAsync(
        string resource,
        Dictionary<string, object?> data,
        List<string> errors,
        CancellationToken ct)
    {
        if (resource is "students" or "lecturers" or "programs" or "courses")
            await ResolveReferenceAsync(
                data,
                "facultyId",
                "facultyCode",
                "faculties",
                "facultyCode",
                "khoa",
                required: resource == "students",
                errors,
                ct);

        if (resource == "students")
            await ResolveReferenceAsync(
                data,
                "programId",
                "programCode",
                "programs",
                "programCode",
                "chương trình đào tạo",
                required: true,
                errors,
                ct);

        if (resource == "semesters")
            await ResolveReferenceAsync(
                data,
                "academicYearId",
                "academicYearCode",
                "academicYears",
                "academicYearCode",
                "năm học",
                required: true,
                errors,
                ct);

        if (resource == "class-sections")
        {
            await ResolveReferenceAsync(
                data,
                "courseId",
                "courseCode",
                "courses",
                "courseCode",
                "môn học",
                required: true,
                errors,
                ct);
            await ResolveReferenceAsync(
                data,
                "lecturerId",
                "lecturerCode",
                "lecturers",
                "lecturerCode",
                "giảng viên",
                required: true,
                errors,
                ct);
            await ResolveReferenceAsync(
                data,
                "semesterId",
                "semesterCode",
                "semesters",
                "semesterCode",
                "học kỳ",
                required: true,
                errors,
                ct);
        }
    }

    private async Task ResolveReferenceAsync(
        Dictionary<string, object?> data,
        string idField,
        string codeField,
        string collectionName,
        string collectionCodeField,
        string label,
        bool required,
        List<string> errors,
        CancellationToken ct)
    {
        var id = data.GetValueOrDefault(idField)?.ToString()?.Trim();
        var code = data.GetValueOrDefault(codeField)?.ToString()?.Trim();
        var collection = db.Database.GetCollection<BsonDocument>(
            collectionName);

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!ObjectId.TryParse(id, out var objectId)
                || !await collection.Find(
                        Builders<BsonDocument>.Filter.Eq("_id", objectId)
                        & Builders<BsonDocument>.Filter.Ne(
                            "isDeleted",
                            true))
                    .AnyAsync(ct))
                errors.Add($"{label} có ID không hợp lệ hoặc đã bị xóa");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            if (required)
                errors.Add($"Thiếu ID hoặc mã {label}");
            return;
        }

        var regex = new BsonRegularExpression(
            $"^{System.Text.RegularExpressions.Regex.Escape(code)}$",
            "i");
        var document = await collection.Find(
                Builders<BsonDocument>.Filter.Regex(
                    collectionCodeField,
                    regex)
                & Builders<BsonDocument>.Filter.Ne("isDeleted", true))
            .FirstOrDefaultAsync(ct);
        if (document is null)
        {
            errors.Add($"Không tìm thấy {label} có mã {code}");
            return;
        }

        data[idField] = document["_id"].AsObjectId.ToString();
    }

    private async Task<bool> ExistingKeyAsync(
        string resource,
        string uniqueKey,
        string key,
        Dictionary<string, object?> data,
        CancellationToken ct)
    {
        var collectionName = resource switch
        {
            "academic-years" => "academicYears",
            "class-sections" => "classSections",
            _ => resource
        };
        var collection = db.Database.GetCollection<BsonDocument>(
            collectionName);
        var regex = new BsonRegularExpression(
            $"^{System.Text.RegularExpressions.Regex.Escape(key)}$",
            "i");
        var filter = Builders<BsonDocument>.Filter.Regex(uniqueKey, regex)
                     & Builders<BsonDocument>.Filter.Ne("isDeleted", true);
        if (resource == "semesters"
            && data.TryGetValue("academicYearId", out var academicYearId)
            && !string.IsNullOrWhiteSpace(academicYearId?.ToString()))
            filter &= Builders<BsonDocument>.Filter.Eq(
                "academicYearId",
                academicYearId.ToString());

        return await collection.Find(filter).AnyAsync(ct);
    }

    private static void NormalizeImportValues(
        string resource,
        Dictionary<string, object?> data)
    {
        if (data.TryGetValue("email", out var email)
            && email is not null)
            data["email"] = email.ToString()!.Trim().ToLowerInvariant();

        var fields = resource switch
        {
            "students" => new[] { "studentCode" },
            "lecturers" => new[] { "lecturerCode" },
            "faculties" => new[] { "facultyCode" },
            "programs" => new[] { "programCode" },
            "academic-years" => new[] { "academicYearCode" },
            "semesters" => new[] { "semesterCode", "academicYearCode" },
            "courses" => new[] { "courseCode", "facultyCode" },
            "class-sections" => new[]
            {
                "classSectionCode", "courseCode", "lecturerCode",
                "semesterCode"
            },
            _ => []
        };
        foreach (var field in fields)
            if (data.TryGetValue(field, out var value)
                && value is not null)
                data[field] = value.ToString()!.Trim().ToUpperInvariant();
    }

    private static void RemoveImportOnlyReferenceCodes(
        string resource,
        Dictionary<string, object?> data)
    {
        if ((resource is "students" or "lecturers" or "programs" or "courses")
            && HasResolvedId(data, "facultyId"))
            data.Remove("facultyCode");
        if (resource == "students" && HasResolvedId(data, "programId"))
            data.Remove("programCode");
        if (resource == "semesters" && HasResolvedId(data, "academicYearId"))
            data.Remove("academicYearCode");
    }

    private static bool HasResolvedId(
        IReadOnlyDictionary<string, object?> data,
        string field) =>
        data.TryGetValue(field, out var value)
        && ObjectId.TryParse(value?.ToString(), out _);

    private static object? ConvertCellValue(IXLCell cell, string field)
    {
        if (cell.IsEmpty())
            return null;
        if (field.EndsWith("Date", StringComparison.OrdinalIgnoreCase)
            || field.EndsWith("At", StringComparison.OrdinalIgnoreCase)
            || field.EndsWith("Start", StringComparison.OrdinalIgnoreCase)
            || field.EndsWith("End", StringComparison.OrdinalIgnoreCase))
        {
            if (cell.TryGetValue<DateTime>(out var date))
                return date.ToUniversalTime();
        }
        if (field.StartsWith("is", StringComparison.OrdinalIgnoreCase)
            || field.StartsWith("exclude", StringComparison.OrdinalIgnoreCase))
        {
            var rawBoolean = cell.GetString().Trim();
            if (bool.TryParse(rawBoolean, out var boolean))
                return boolean;
            if (rawBoolean == "1")
                return true;
            if (rawBoolean == "0")
                return false;
        }
        if (new[]
            {
                "credits", "capacity", "requiredCredits",
                "durationYears", "theoryPeriods", "practicePeriods"
            }.Contains(field, StringComparer.OrdinalIgnoreCase)
            && cell.TryGetValue<double>(out var number))
            return number;

        return cell.GetString().Trim();
    }

    private static string Read(IXLRow row, IReadOnlyDictionary<string, int> headers, string key) => headers.TryGetValue(key, out var col) ? row.Cell(col).GetString().Trim() : "";
}
