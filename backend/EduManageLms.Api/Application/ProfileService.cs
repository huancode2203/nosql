using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class ProfileService(MongoContext db) : IProfileService
{
    public async Task<UserProfileDto> GetAsync(string userId, CancellationToken ct)
    {
        var user = await db.Users.Find(x => x.Id == userId && !x.IsDeleted).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy tài khoản");
        return await MapAsync(user, ct);
    }

    public async Task<UserProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.Find(x => x.Id == userId && !x.IsDeleted).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Không tìm thấy tài khoản");

        var secondaryEmail = request.SecondaryEmail?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(secondaryEmail)
            && (!secondaryEmail.Contains('@')
                || secondaryEmail.Equals(
                    user.Email,
                    StringComparison.OrdinalIgnoreCase)))
            throw new AppException(
                "Email phụ phải hợp lệ và khác email đăng nhập.");
        if (request.DateOfBirth > DateTime.UtcNow.Date)
            throw new AppException("Ngày sinh không được nằm trong tương lai.");

        var gender = request.Gender?.Trim();
        if (!string.IsNullOrWhiteSpace(gender)
            && !new[] { "Nam", "Nữ", "Khác" }.Contains(
                gender,
                StringComparer.OrdinalIgnoreCase))
            throw new AppException("Giới tính không hợp lệ.");

        using var session = await db.Client.StartSessionAsync(
            cancellationToken: ct);
        await session.WithTransactionAsync(
            async (_, token) =>
            {
                await db.Users.UpdateOneAsync(
                    session,
                    x => x.Id == user.Id && !x.IsDeleted,
                    Builders<User>.Update
                        .Set(x => x.SecondaryEmail, secondaryEmail)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: token);

                if (user.Role == "Student"
                    && !string.IsNullOrWhiteSpace(user.StudentCode))
                {
                    var update = Builders<Student>.Update
                        .Set(x => x.Phone, request.Phone?.Trim() ?? string.Empty)
                        .Set(x => x.Address, request.Address?.Trim() ?? string.Empty)
                        .Set(x => x.DateOfBirth, request.DateOfBirth)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow);
                    if (!string.IsNullOrWhiteSpace(gender))
                        update = update.Set(x => x.Gender, gender);

                    var result = await db.Students.UpdateOneAsync(
                        session,
                        x => x.StudentCode == user.StudentCode && !x.IsDeleted,
                        update,
                        cancellationToken: token);
                    if (result.MatchedCount == 0)
                        throw new NotFoundException(
                            "Không tìm thấy hồ sơ sinh viên");
                }
                else if (user.Role == "Lecturer"
                         && !string.IsNullOrWhiteSpace(user.LecturerCode))
                {
                    var result = await db.Lecturers.UpdateOneAsync(
                        session,
                        x => x.LecturerCode == user.LecturerCode && !x.IsDeleted,
                        Builders<Lecturer>.Update
                            .Set(
                                x => x.Phone,
                                request.Phone?.Trim() ?? string.Empty)
                            .Set(x => x.UpdatedAt, DateTime.UtcNow),
                        cancellationToken: token);
                    if (result.MatchedCount == 0)
                        throw new NotFoundException(
                            "Không tìm thấy hồ sơ giảng viên");
                }

                return true;
            },
            new TransactionOptions(
                readPreference: ReadPreference.Primary,
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority),
            ct);

        user.SecondaryEmail = secondaryEmail;

        return await MapAsync(user, ct);
    }

    private async Task<UserProfileDto> MapAsync(User user, CancellationToken ct)
    {
        string phone = "", address = "", faculty = "", program = "", gender = "";
        string cohort = "", administrativeClass = "", degree = "", title = "", department = "";
        var requiredCredits = 0;
        DateTime? dateOfBirth = null;

        if (user.Role == "Student" && !string.IsNullOrWhiteSpace(user.StudentCode))
        {
            var student = await db.Students.Find(x => x.StudentCode == user.StudentCode && !x.IsDeleted).FirstOrDefaultAsync(ct);
            if (student is not null)
            {
                phone = student.Phone;
                address = student.Address;
                dateOfBirth = student.DateOfBirth;
                faculty = student.Faculty.FacultyName;
                program = student.Program.ProgramName;
                gender = student.Gender;
                cohort = student.Cohort;
                administrativeClass = student.AdministrativeClass;
                requiredCredits = student.Program.RequiredCredits;
            }
        }
        else if (user.Role == "Lecturer" && !string.IsNullOrWhiteSpace(user.LecturerCode))
        {
            var lecturer = await db.Lecturers.Find(x => x.LecturerCode == user.LecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct);
            if (lecturer is not null)
            {
                phone = lecturer.Phone;
                faculty = lecturer.Faculty.FacultyName;
                degree = lecturer.Degree;
                title = lecturer.Title;
                department = lecturer.Department;
            }
        }

        return new UserProfileDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role,
            user.Status,
            user.AvatarUrl,
            user.SecondaryEmail,
            phone,
            address,
            dateOfBirth,
            user.StudentCode,
            user.LecturerCode,
            faculty,
            program,
            user.LastLoginAt,
            gender,
            cohort,
            administrativeClass,
            requiredCredits,
            degree,
            title,
            department);
    }
}
