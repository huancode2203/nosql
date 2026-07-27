using MongoDB.Bson; using MongoDB.Bson.Serialization.Attributes;
namespace EduManageLms.Api.Domain;
public abstract class Document { [BsonId][BsonRepresentation(BsonType.ObjectId)] public string Id {get;set;}=ObjectId.GenerateNewId().ToString(); public DateTime CreatedAt{get;set;}=DateTime.UtcNow; public DateTime UpdatedAt{get;set;}=DateTime.UtcNow; public bool IsDeleted{get;set;} }
public sealed class User:Document { public string Username{get;set;}=""; public string Email{get;set;}=""; public string FullName{get;set;}=""; public string PasswordHash{get;set;}=""; public string Role{get;set;}="Student"; public string Status{get;set;}="Active"; public string? StudentCode{get;set;} public string? LecturerCode{get;set;} public string? AvatarUrl{get;set;} public int FailedLoginCount{get;set;} public DateTime? LockedUntil{get;set;} public DateTime? LastLoginAt{get;set;} public List<RefreshToken> RefreshTokens{get;set;}=[]; }
public sealed class RefreshToken { public string TokenHash{get;set;}=""; public DateTime ExpiresAt{get;set;} public DateTime CreatedAt{get;set;}=DateTime.UtcNow; public DateTime? RevokedAt{get;set;} public string Device{get;set;}="Unknown"; public bool IsActive=>RevokedAt is null&&ExpiresAt>DateTime.UtcNow; }
public sealed class FacultySnapshot { [BsonRepresentation(BsonType.ObjectId)] public string? FacultyId{get;set;} public string FacultyCode{get;set;}=""; public string FacultyName{get;set;}=""; }
public sealed class ProgramSnapshot { [BsonRepresentation(BsonType.ObjectId)] public string? ProgramId{get;set;} public string ProgramCode{get;set;}=""; public string ProgramName{get;set;}=""; public int RequiredCredits{get;set;} }
public sealed class LecturerSnapshot { [BsonRepresentation(BsonType.ObjectId)] public string? LecturerId{get;set;} public string LecturerCode{get;set;}=""; public string FullName{get;set;}=""; }
public sealed class ScoreComponent
{
    public string ComponentId { get; set; } = "";
    public string ComponentName { get; set; } = "";
    public string Type { get; set; } = "";
    public double Weight { get; set; }
    public double MaxScore { get; set; } = 10;
    public double? Score { get; set; }

    // Dữ liệu gốc và thông tin chuẩn hóa phục vụ kiểm tra/audit.
    public string RawInput { get; set; } = "";
    public string NormalizationType { get; set; } = "None";
    public bool RequiresConfirmation { get; set; }
    public string EnteredBy { get; set; } = "";
    public DateTime? EnteredAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string Status { get; set; } = "NotGraded";
    public bool IsRequired { get; set; }
    public double? MinimumScore { get; set; }
    public List<CloMapping> CloMappings { get; set; } = [];
}
public sealed class CloMapping { public string CloCode{get;set;}=""; public double MappingWeight{get;set;} }
public sealed class StudentCourseRecord
{
    [BsonRepresentation(BsonType.ObjectId)] public string CourseId { get; set; } = "";
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int Credits { get; set; }
    public bool ExcludeFromGpa { get; set; }
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    public string ClassSectionCode { get; set; } = "";
    public LecturerSnapshot Lecturer { get; set; } = new();
    public int GradingSchemeVersion { get; set; } = 1;
    public List<ScoreComponent> Scores { get; set; } = [];
    public int AttemptNumber { get; set; } = 1;
    public string ScoreStatus { get; set; } = "Draft";
    public DateTime? PublishedAt { get; set; }

    // Optimistic concurrency cho thao tác nhập điểm.
    public int Version { get; set; }
}
public sealed class SemesterRecord { [BsonRepresentation(BsonType.ObjectId)] public string SemesterId{get;set;}=""; public string SemesterCode{get;set;}=""; public string SemesterName{get;set;}=""; public List<StudentCourseRecord> Courses{get;set;}=[]; }
public sealed class AcademicRecord { [BsonRepresentation(BsonType.ObjectId)] public string AcademicYearId{get;set;}=""; public string AcademicYearName{get;set;}=""; public List<SemesterRecord> Semesters{get;set;}=[]; }
public sealed class Student:Document { public string StudentCode{get;set;}=""; public string FullName{get;set;}=""; public string Email{get;set;}=""; public string Phone{get;set;}=""; public string Address{get;set;}=""; public string Gender{get;set;}=""; public DateTime? DateOfBirth{get;set;} public FacultySnapshot Faculty{get;set;}=new(); public ProgramSnapshot Program{get;set;}=new(); public string Cohort{get;set;}=""; public string AdministrativeClass{get;set;}=""; public string Status{get;set;}="Studying"; public List<AcademicRecord> AcademicRecords{get;set;}=[]; }
public sealed class Lecturer:Document { public string LecturerCode{get;set;}=""; public string FullName{get;set;}=""; public string Email{get;set;}=""; public string Phone{get;set;}=""; public string Degree{get;set;}="Thạc sĩ"; public string Title{get;set;}="Giảng viên"; public FacultySnapshot Faculty{get;set;}=new(); public string Department{get;set;}=""; public List<string> Specializations{get;set;}=[]; public string Status{get;set;}="Active"; }
public sealed class CloDefinition { public string CloCode{get;set;}=""; public string Name{get;set;}=""; public string Description{get;set;}=""; public string BloomLevel{get;set;}="Apply"; public double Threshold{get;set;}=50; public double Weight{get;set;} public bool Active{get;set;}=true; }
public sealed class GradeScaleItem { public double Min{get;set;} public double Max{get;set;} public string Letter{get;set;}=""; public double GradePoint{get;set;} public string Classification{get;set;}=""; }
public sealed class GradingComponentDefinition { public string ComponentId{get;set;}=""; public string Name{get;set;}=""; public string Type{get;set;}=""; public double Weight{get;set;} public double MaxScore{get;set;}=10; public bool IsRequired{get;set;} public double? MinimumScore{get;set;} public bool IsFinalCondition{get;set;} public List<CloMapping> CloMappings{get;set;}=[]; }
public sealed class GradingSchemeVersion { public int Version{get;set;} public string AcademicYear{get;set;}=""; public List<GradingComponentDefinition> Components{get;set;}=[]; public double PassingScore{get;set;}=4; public string RoundingMode{get;set;}="Normal"; public int DecimalPlaces{get;set;}=2; public DateTime EffectiveFrom{get;set;}=DateTime.UtcNow; public bool Active{get;set;}=true; }
public sealed class Course:Document { public string CourseCode{get;set;}=""; public string CourseName{get;set;}=""; public string? EnglishName{get;set;} public int Credits{get;set;} public int TheoryPeriods{get;set;} public int PracticePeriods{get;set;} public bool ExcludeFromGpa{get;set;} public bool IsCoreCourse{get;set;} public FacultySnapshot Faculty{get;set;}=new(); public string Description{get;set;}=""; public string Status{get;set;}="Active"; public List<string> PrerequisiteCourseCodes{get;set;}=[]; public List<CloDefinition> Clos{get;set;}=[]; public List<GradingSchemeVersion> GradingSchemes{get;set;}=[]; public List<GradeScaleItem> GradeScale{get;set;}=[]; }
public sealed class StudentEnrollmentSnapshot { [BsonRepresentation(BsonType.ObjectId)] public string StudentId{get;set;}=""; public string StudentCode{get;set;}=""; public string FullName{get;set;}=""; public string Status{get;set;}="Enrolled"; }
public sealed class ScheduleSlot { public string DayOfWeek{get;set;}="Monday"; public string StartTime{get;set;}="07:00"; public string EndTime{get;set;}="09:30"; public string Room{get;set;}=""; }
public sealed class ClassSection:Document { public string ClassSectionCode{get;set;}=""; [BsonRepresentation(BsonType.ObjectId)] public string CourseId{get;set;}=""; public string CourseCode{get;set;}=""; public string CourseName{get;set;}=""; [BsonRepresentation(BsonType.ObjectId)] public string AcademicYearId{get;set;}=""; public string AcademicYearName{get;set;}=""; [BsonRepresentation(BsonType.ObjectId)] public string SemesterId{get;set;}=""; public string SemesterCode{get;set;}=""; public string SemesterName{get;set;}=""; [BsonRepresentation(BsonType.ObjectId)] public string LecturerId{get;set;}=""; public string LecturerCode{get;set;}=""; public string LecturerName{get;set;}=""; public int Capacity{get;set;}=40; public List<StudentEnrollmentSnapshot> Students{get;set;}=[]; public GradingSchemeVersion GradingSchemeSnapshot{get;set;}=new(); public string GradeStatus{get;set;}="Draft"; public List<ScheduleSlot> Schedule{get;set;}=[]; public DateTime StartDate{get;set;} public DateTime EndDate{get;set;} }
public sealed class Notification:Document { public string Title{get;set;}=""; public string Content{get;set;}=""; public string Type{get;set;}="General"; public string Priority{get;set;}="Normal"; public string SenderId{get;set;}=""; public List<string> RecipientIds{get;set;}=[]; public string AudienceType{get;set;}="All"; public DateTime DisplayFrom{get;set;}=DateTime.UtcNow; public DateTime? ExpiresAt{get;set;} public List<string> ReadBy{get;set;}=[]; public string Status{get;set;}="Sent"; }
public sealed class AuditLog:Document { public string UserId{get;set;}=""; public string UserName{get;set;}=""; public string Role{get;set;}=""; public string Action{get;set;}=""; public string Entity{get;set;}=""; public string? EntityId{get;set;} public object? Before{get;set;} public object? After{get;set;} public string IpAddress{get;set;}=""; public string UserAgent{get;set;}=""; public string Result{get;set;}="Success"; public string? Note{get;set;} }
public sealed class BackupHistory:Document { public string FileName{get;set;}=""; public long SizeBytes{get;set;} public string Status{get;set;}="Pending"; public string PerformedBy{get;set;}=""; public string Type{get;set;}="Manual"; public string? Error{get;set;} public DateTime? CompletedAt{get;set;} }
public sealed class LoginHistory:Document { public string Identifier{get;set;}=""; public string? UserId{get;set;} public bool Success{get;set;} public string IpAddress{get;set;}=""; public string UserAgent{get;set;}=""; public string? FailureReason{get;set;} }
public sealed class PasswordResetToken : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string RequestedFromIp { get; set; } = "";
    public bool IsActive => UsedAt is null && ExpiresAt > DateTime.UtcNow;
}
