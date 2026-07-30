using System.Globalization;
using EduManageLms.Api.Common;

namespace EduManageLms.Api.Application;

public sealed record LoginRequest(string Identifier, string Password, bool RememberMe);
public sealed record RefreshRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Code, string NewPassword);
public sealed record LoginUserDto(string Id, string Username, string Email, string FullName, string Role, string? AvatarUrl);
public sealed record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, LoginUserDto User);
public sealed record DashboardCardDto(string Label, object Value, string Icon, string? Trend, string Tone);
public sealed record ChartItemDto(string Label, double Value);
public sealed record ActivityDto(string Title, string Description, string Time, string Icon);
public sealed record AlertDto(string Title, string Detail, string Tone);
public sealed record DashboardDto(IReadOnlyCollection<DashboardCardDto> Cards, IReadOnlyCollection<ChartItemDto> GradeDistribution, IReadOnlyCollection<ActivityDto> RecentActivities, IReadOnlyCollection<AlertDto> Alerts);
public sealed record GradeComponentDto(string ComponentId, string ComponentName, double Weight, double MaxScore, double? Score, string Status);
public sealed record CourseGradeDto(string CourseId, string CourseCode, string CourseName, int Credits, string ClassSectionCode, string LecturerName, IReadOnlyCollection<GradeComponentDto> Scores, double FinalScore, string LetterGrade, double GradePoint, string Classification, bool Passed, DateTime? PublishedAt);
public sealed record StudentGpaDto(double Gpa, double Average10, int TotalCredits, int PassedCredits, string Classification);
public sealed record CloResultDto(string CourseCode, string CourseName, string CloCode, string Description, double Percentage, double Threshold, bool Passed, IReadOnlyCollection<string> ContributingComponents);
public sealed record GradebookComponentDto(
    string ComponentId,
    string ComponentName,
    double Weight,
    double MaxScore);

public sealed record GradebookStudentDto(
    string StudentId,
    string StudentCode,
    string FullName,
    Dictionary<string, string?> Scores,
    double FinalScore,
    string LetterGrade,
    bool Passed,
    int Version = 0)
{
    // Tương thích các service cũ đang tạo DTO từ Dictionary<string, double?>.
    public GradebookStudentDto(
        string studentId,
        string studentCode,
        string fullName,
        Dictionary<string, double?> scores,
        double finalScore,
        string letterGrade,
        bool passed)
        : this(
            studentId,
            studentCode,
            fullName,
            scores.ToDictionary(
                item => item.Key,
                item => item.Value?.ToString("0.################", CultureInfo.InvariantCulture)),
            finalScore,
            letterGrade,
            passed,
            0)
    {
    }
}

public sealed record GradebookDto(
    string ClassSectionId,
    string ClassSectionCode,
    string CourseName,
    string Status,
    IReadOnlyCollection<GradebookComponentDto> Components,
    IReadOnlyCollection<GradebookStudentDto> Students);

public sealed class GradeUpdateStudent
{
    public string StudentId { get; set; } = "";
    public Dictionary<string, string?> Scores { get; set; } = new();
    public IReadOnlyCollection<string>? ConfirmedComponents { get; set; }
    public int? Version { get; set; }

    // Constructor rỗng để System.Text.Json deserialize request ổn định.
    public GradeUpdateStudent()
    {
    }

    public GradeUpdateStudent(
        string studentId,
        Dictionary<string, string?> scores,
        IReadOnlyCollection<string>? confirmedComponents = null,
        int? version = null)
    {
        StudentId = studentId;
        Scores = scores;
        ConfirmedComponents = confirmedComponents;
        Version = version;
    }

    // Giữ tương thích với luồng import Excel cũ.
    public GradeUpdateStudent(
        string studentId,
        Dictionary<string, double?> scores)
        : this(
            studentId,
            scores.ToDictionary(
                item => item.Key,
                item => item.Value?.ToString(
                    "0.################",
                    CultureInfo.InvariantCulture)),
            null,
            null)
    {
    }
}

public sealed record GradeUpdateRequest(
    IReadOnlyCollection<GradeUpdateStudent> Students,
    bool Publish);

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string ip, string userAgent, CancellationToken ct);
    Task<LoginResponse> RefreshAsync(string token, string userAgent, CancellationToken ct);
    Task RevokeAsync(string token, CancellationToken ct);
    Task RevokeAllAsync(string userId, CancellationToken ct);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<string?> ForgotPasswordAsync(string email, string ipAddress, CancellationToken ct);
    Task ResetPasswordAsync(string email, string code, string newPassword, CancellationToken ct);
}

public interface IDashboardService
{
    Task<DashboardDto> AdminAsync(CancellationToken ct);
    Task<DashboardDto> LecturerAsync(string lecturerCode, CancellationToken ct);
    Task<DashboardDto> StudentAsync(string studentCode, CancellationToken ct);
}

public interface IStudentAnalyticsService
{
    Task<IReadOnlyCollection<CourseGradeDto>> GetGradesAsync(string studentCode, string? year, string? semester, CancellationToken ct);
    Task<IReadOnlyCollection<TranscriptTermDto>> GetTranscriptAsync(string studentCode, CancellationToken ct);
    Task<StudentGpaDto> GetCumulativeGpaAsync(string studentCode, CancellationToken ct);
    Task<IReadOnlyCollection<CloResultDto>> GetCloAsync(string studentCode, CancellationToken ct);
}

public interface IGradebookService
{
    Task<GradebookDto> GetAsync(string lecturerCode, string? sectionId, CancellationToken ct);
    Task UpdateAsync(string lecturerCode, string sectionId, GradeUpdateRequest request, string userId, CancellationToken ct);
}

public interface IAdminResourceService
{
    Task<PagedResult<Dictionary<string, object?>>> ListAsync(string resource, string? search, int page, int size, CancellationToken ct);
    Task<Dictionary<string, object?>> GetAsync(string resource, string id, CancellationToken ct);
    Task<Dictionary<string, object?>> CreateAsync(string resource, Dictionary<string, object?> body, CancellationToken ct);
    Task<Dictionary<string, object?>> UpdateAsync(string resource, string id, Dictionary<string, object?> body, CancellationToken ct);
    Task DeleteAsync(string resource, string id, CancellationToken ct);
    Task RestoreAsync(string resource, string id, CancellationToken ct);
}

public interface IBackupService
{
    Task<Dictionary<string, object>> CreateAsync(string userId, CancellationToken ct);
    Task<IReadOnlyCollection<Dictionary<string, object?>>> ListAsync(CancellationToken ct);
    Task<(byte[] Content, string FileName)> DownloadAsync(string id, CancellationToken ct);
    Task<Dictionary<string, object>> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file, string userId, CancellationToken ct);
    Task DeleteAsync(string id, string userId, CancellationToken ct);
    Task RestoreAsync(string id, string userId, string confirmation, CancellationToken ct);
}
