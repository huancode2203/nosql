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
        user.AvatarUrl = request.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await db.Users.ReplaceOneAsync(x => x.Id == user.Id, user, cancellationToken: ct);

        if (user.Role == "Student" && !string.IsNullOrWhiteSpace(user.StudentCode))
        {
            var student = await db.Students.Find(x => x.StudentCode == user.StudentCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("Không tìm thấy hồ sơ sinh viên");
            student.Phone = request.Phone.Trim();
            student.Address = request.Address.Trim();
            student.DateOfBirth = request.DateOfBirth;
            student.UpdatedAt = DateTime.UtcNow;
            await db.Students.ReplaceOneAsync(x => x.Id == student.Id, student, cancellationToken: ct);
        }
        else if (user.Role == "Lecturer" && !string.IsNullOrWhiteSpace(user.LecturerCode))
        {
            var lecturer = await db.Lecturers.Find(x => x.LecturerCode == user.LecturerCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
                           ?? throw new NotFoundException("Không tìm thấy hồ sơ giảng viên");
            lecturer.Phone = request.Phone.Trim();
            lecturer.UpdatedAt = DateTime.UtcNow;
            await db.Lecturers.ReplaceOneAsync(x => x.Id == lecturer.Id, lecturer, cancellationToken: ct);
        }

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
