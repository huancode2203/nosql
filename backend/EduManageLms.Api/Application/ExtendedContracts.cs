using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using Microsoft.AspNetCore.Http;

namespace EduManageLms.Api.Application;

public sealed record LecturerClassDto(
    string Id,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    string AcademicYearName,
    string SemesterName,
    int StudentCount,
    string GradeStatus,
    IReadOnlyCollection<ScheduleSlot> Schedule,
    DateTime StartDate,
    DateTime EndDate);

public sealed record ClassStudentDto(string Id, string StudentCode, string FullName, string Email, string AdministrativeClass, string Status);

public sealed record ClassStatisticsDto(
    string ClassSectionId,
    string ClassSectionCode,
    string CourseName,
    int StudentCount,
    double Average,
    double Highest,
    double Lowest,
    double Median,
    double StandardDeviation,
    int Passed,
    int Failed,
    double PassRate,
    IReadOnlyCollection<ChartItemDto> Distribution,
    IReadOnlyCollection<GradebookStudentDto> TopStudents,
    IReadOnlyCollection<GradebookStudentDto> AtRiskStudents);

public sealed record ClassCloStatisticsDto(string CloCode, string Description, double AveragePercentage, double Threshold, int PassedStudents, int TotalStudents, double PassRate);

public sealed record MaterialDto(
    string Id,
    string ClassSectionId,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    string Title,
    string Description,
    string Category,
    string Chapter,
    string ResourceType,
    string ResourceUrl,
    DateTime VisibleFrom,
    DateTime? VisibleUntil,
    int ViewCount,
    int DownloadCount,
    string Status);

public sealed record MaterialUpsertRequest(
    string ClassSectionId,
    string Title,
    string Description,
    string Category,
    string Chapter,
    string ResourceType,
    string ResourceUrl,
    DateTime? VisibleFrom,
    DateTime? VisibleUntil,
    string Status);

public sealed record AssignmentDto(
    string Id,
    string ClassSectionId,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    string Title,
    string Content,
    string AttachmentUrl,
    double MaxScore,
    DateTime OpenAt,
    DateTime DueAt,
    bool AllowLate,
    double LatePenaltyPercent,
    IReadOnlyCollection<string> CloCodes,
    string? LinkedComponentId,
    string Status,
    int SubmissionCount,
    string? StudentSubmissionStatus,
    double? StudentScore,
    string? StudentFeedback);

public sealed record AssignmentUpsertRequest(
    string ClassSectionId,
    string Title,
    string Content,
    string AttachmentUrl,
    double MaxScore,
    DateTime OpenAt,
    DateTime DueAt,
    bool AllowLate,
    double LatePenaltyPercent,
    IReadOnlyCollection<string> CloCodes,
    string? LinkedComponentId,
    string Status);

public sealed record SubmissionDto(
    string Id,
    string AssignmentId,
    string StudentId,
    string StudentCode,
    string StudentName,
    string TextContent,
    IReadOnlyCollection<SubmissionFile> Files,
    DateTime SubmittedAt,
    bool IsLate,
    string Status,
    double? Score,
    string Feedback,
    bool ResubmissionAllowed);

public sealed record GradeSubmissionRequest(double Score, string Feedback, bool ResubmissionAllowed, string Status);
public sealed record StudentSubmissionRequest(string TextContent);

public sealed record StudentCourseDto(
    string ClassSectionId,
    string ClassSectionCode,
    string CourseCode,
    string CourseName,
    int Credits,
    string LecturerName,
    string AcademicYearName,
    string SemesterCode,
    string SemesterName,
    string ScoreStatus,
    IReadOnlyCollection<ScheduleSlot> Schedule);

public sealed record ScheduleItemDto(
    string Type,
    string CourseCode,
    string CourseName,
    string ClassSectionCode,
    DateTime? Date,
    string DayOfWeek,
    string StartTime,
    string EndTime,
    string Room,
    string LecturerName,
    string Note);

public sealed record TranscriptTermDto(
    string AcademicYear,
    string SemesterCode,
    string SemesterName,
    IReadOnlyCollection<CourseGradeDto> Courses,
    double Gpa,
    double Average10,
    int TotalCredits,
    int PassedCredits);

public sealed record UserProfileDto(
    string Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string Status,
    string? AvatarUrl,
    string? SecondaryEmail,
    string Phone,
    string Address,
    DateTime? DateOfBirth,
    string? StudentCode,
    string? LecturerCode,
    string FacultyName,
    string ProgramName,
    DateTime? LastLoginAt,
    string Gender,
    string Cohort,
    string AdministrativeClass,
    int RequiredCredits,
    string Degree,
    string JobTitle,
    string Department);

public sealed record UpdateProfileRequest(
    string Phone,
    string Address,
    DateTime? DateOfBirth,
    string? Gender,
    string? SecondaryEmail);

public sealed record AdminAvatarDto(string UserId, string? AvatarUrl);

public interface IAdminAvatarService
{
    Task<AdminAvatarDto> UploadAsync(
        string userId,
        IFormFile file,
        AdminActor actor,
        CancellationToken ct);

    Task<AdminAvatarDto> DeleteAsync(
        string userId,
        AdminActor actor,
        CancellationToken ct);
}

public sealed record CourseDesignDto(string CourseId, string CourseCode, string CourseName, IReadOnlyCollection<CloDefinition> Clos, IReadOnlyCollection<GradingSchemeVersion> GradingSchemes);
public sealed record AdminCourseOptionDto(
    string Id,
    string CourseCode,
    string CourseName);
public sealed record AdminLookupOptionDto(
    string Id,
    string Code,
    string Name);
public sealed record AdminReportOptionsDto(
    IReadOnlyCollection<AdminLookupOptionDto> AcademicYears,
    IReadOnlyCollection<AdminLookupOptionDto> Semesters,
    IReadOnlyCollection<AdminLookupOptionDto> Faculties,
    IReadOnlyCollection<AdminLookupOptionDto> Programs);
public sealed record AdminNotificationOptionsDto(
    IReadOnlyCollection<AdminLookupOptionDto> Faculties,
    IReadOnlyCollection<AdminLookupOptionDto> ClassSections);
public sealed record SaveCourseDesignRequest(IReadOnlyCollection<CloDefinition> Clos, GradingSchemeVersion Scheme);

public sealed record AdminReportDto(
    IReadOnlyCollection<DashboardCardDto> Cards,
    IReadOnlyCollection<ChartItemDto> StudentsByFaculty,
    IReadOnlyCollection<ChartItemDto> GradeStatus,
    IReadOnlyCollection<ChartItemDto> LearningStatus,
    IReadOnlyCollection<ChartItemDto> CloAchievement,
    IReadOnlyCollection<ActivityDto> RecentActivities);

public sealed record ImportRowResult(int RowNumber, bool Valid, IReadOnlyCollection<string> Errors, Dictionary<string, object?> Data);
public sealed record ImportPreviewDto(int TotalRows, int ValidRows, int InvalidRows, IReadOnlyCollection<ImportRowResult> Rows);

public interface IAdminAcademicService
{
    Task<IReadOnlyCollection<AdminCourseOptionDto>> GetGradingCoursesAsync(
        CancellationToken ct);
    Task<AdminReportOptionsDto> GetReportOptionsAsync(
        CancellationToken ct);
    Task<AdminNotificationOptionsDto> GetNotificationOptionsAsync(
        CancellationToken ct);
    Task<CourseDesignDto> GetCourseDesignAsync(string courseId, CancellationToken ct);
    Task<CourseDesignDto> SaveCourseDesignAsync(string courseId, SaveCourseDesignRequest request, string userId, CancellationToken ct);
    Task<PagedResult<Dictionary<string, object?>>> GetAuditLogsAsync(
        string? search,
        string? role,
        string? action,
        string? result,
        DateTime? from,
        DateTime? to,
        int page,
        int size,
        CancellationToken ct);
    Task<AdminReportDto> GetReportsAsync(
        string? academicYearId,
        string? semesterId,
        string? facultyId,
        string? programId,
        CancellationToken ct);
    Task<byte[]> ExportReportPdfAsync(
        string? academicYearId,
        string? semesterId,
        string? facultyId,
        string? programId,
        CancellationToken ct);
    Task<Dictionary<string, object?>> ReviewReopenRequestAsync(string id, bool approve, string note, string userId, CancellationToken ct);
}

public interface ILecturerPortalService
{
    Task<IReadOnlyCollection<LecturerClassDto>> GetClassesAsync(string lecturerCode, CancellationToken ct);
    Task<IReadOnlyCollection<ClassStudentDto>> GetStudentsAsync(string lecturerCode, string classSectionId, CancellationToken ct);
    Task<ClassStatisticsDto> GetStatisticsAsync(string lecturerCode, string classSectionId, CancellationToken ct);
    Task<IReadOnlyCollection<ClassCloStatisticsDto>> GetCloStatisticsAsync(string lecturerCode, string classSectionId, CancellationToken ct);
    Task<IReadOnlyCollection<MaterialDto>> GetMaterialsAsync(string lecturerCode, string? classSectionId, CancellationToken ct);
    Task<MaterialDto> SaveMaterialAsync(string lecturerCode, string? id, MaterialUpsertRequest request, CancellationToken ct);
    Task DeleteMaterialAsync(string lecturerCode, string id, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentDto>> GetAssignmentsAsync(string lecturerCode, string? classSectionId, CancellationToken ct);
    Task<AssignmentDto> SaveAssignmentAsync(string lecturerCode, string? id, AssignmentUpsertRequest request, CancellationToken ct);
    Task DeleteAssignmentAsync(string lecturerCode, string id, CancellationToken ct);
    Task<IReadOnlyCollection<SubmissionDto>> GetSubmissionsAsync(string lecturerCode, string assignmentId, CancellationToken ct);
    Task GradeSubmissionAsync(string lecturerCode, string submissionId, GradeSubmissionRequest request, CancellationToken ct);
    Task RequestReopenAsync(string lecturerCode, string classSectionId, string reason, CancellationToken ct);
    Task<byte[]> ExportGradebookAsync(string lecturerCode, string classSectionId, CancellationToken ct);
    Task<ImportPreviewDto> ImportGradesAsync(string lecturerCode, string classSectionId, IFormFile file, bool commit, string userId, CancellationToken ct);
}


public sealed record SemesterOptionDto(string Key, string Label, bool AllCoursesGraded);
public sealed record SemesterCourseAverageDto(string CourseCode, string CourseName, int Credits, double FinalScore10, bool ExcludeFromGpa);
public sealed record SemesterAverageChartDto(
    string SemesterKey,
    string SemesterLabel,
    bool AllCoursesGraded,
    double? Average10,
    IReadOnlyCollection<SemesterCourseAverageDto> Courses);

public sealed record CurriculumCourseDto(
    int Order,
    string CourseCode,
    string CourseName,
    int Credits,
    int TheoryPeriods,
    int PracticePeriods,
    string Group,
    int ElectiveGroup,
    int RequiredCreditsInGroup,
    bool ExcludeFromGpa,
    bool IsCoreCourse,
    bool IsDefaultSelection,
    bool IsSelected,
    string Status,
    double? FinalScore);

public sealed record CurriculumSemesterDto(
    int SemesterNumber,
    int RequiredCredits,
    int ElectiveCredits,
    IReadOnlyCollection<CurriculumCourseDto> Courses);

public sealed record StudentCurriculumDto(
    string ProgramCode,
    string ProgramName,
    string FacultyName,
    string EducationLevel,
    string ApplicableCohort,
    string CurriculumVersion,
    int RequiredCredits,
    int RequiredCompulsoryCredits,
    int RequiredElectiveCredits,
    int CompletedCredits,
    double ProgressPercent,
    IReadOnlyCollection<CurriculumSemesterDto> Semesters);

public interface IStudentPortalService
{
    Task<IReadOnlyCollection<StudentCourseDto>> GetCurrentCoursesAsync(string studentCode, CancellationToken ct);
    Task<IReadOnlyCollection<TranscriptTermDto>> GetTranscriptAsync(string studentCode, CancellationToken ct);
    Task<IReadOnlyCollection<SemesterOptionDto>> GetSemesterOptionsAsync(string studentCode, CancellationToken ct);
    Task<SemesterAverageChartDto> GetSemesterAverageChartAsync(string studentCode, string semesterKey, CancellationToken ct);
    Task<StudentCurriculumDto> GetCurriculumAsync(string studentCode, CancellationToken ct);
    Task<IReadOnlyCollection<ScheduleItemDto>> GetScheduleAsync(string studentCode, CancellationToken ct);
    Task<IReadOnlyCollection<MaterialDto>> GetMaterialsAsync(string studentCode, string? classSectionId, CancellationToken ct);
    Task<IReadOnlyCollection<AssignmentDto>> GetAssignmentsAsync(string studentCode, string? classSectionId, CancellationToken ct);
    Task<SubmissionDto> SubmitAsync(string studentCode, string assignmentId, StudentSubmissionRequest request, IReadOnlyCollection<IFormFile> files, CancellationToken ct);
    Task<byte[]> ExportTranscriptAsync(string studentCode, CancellationToken ct);
}

public interface IProfileService
{
    Task<UserProfileDto> GetAsync(string userId, CancellationToken ct);
    Task<UserProfileDto> UpdateAsync(string userId, UpdateProfileRequest request, CancellationToken ct);
}

public interface IImportExportService
{
    Task<byte[]> ExportResourceAsync(string resource, CancellationToken ct);
    Task<ImportPreviewDto> ImportStudentsAsync(IFormFile file, bool commit, CancellationToken ct);
    Task<ImportPreviewDto> ImportResourceAsync(
        string resource,
        IFormFile file,
        bool commit,
        AdminActor actor,
        CancellationToken ct);
}
