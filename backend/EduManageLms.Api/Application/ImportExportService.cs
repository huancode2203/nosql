using ClosedXML.Excel;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class ImportExportService(MongoContext db) : IImportExportService
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

    private static string Read(IXLRow row, IReadOnlyDictionary<string, int> headers, string key) => headers.TryGetValue(key, out var col) ? row.Cell(col).GetString().Trim() : "";
}
