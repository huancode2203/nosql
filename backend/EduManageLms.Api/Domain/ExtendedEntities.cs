using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EduManageLms.Api.Domain;

public sealed class Faculty : Document
{
    public string FacultyCode { get; set; } = "";
    public string FacultyName { get; set; } = "";
    public string Description { get; set; } = "";
    public string DeanName { get; set; } = "";
    public string Status { get; set; } = "Active";
}

[BsonIgnoreExtraElements]
public sealed class ProgramCourseItem
{
    [BsonRepresentation(BsonType.ObjectId)] public string? CourseId { get; set; }
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public int Credits { get; set; }
    public string Group { get; set; } = "Required";
    public int SuggestedSemester { get; set; }
    public int TheoryPeriods { get; set; }
    public int PracticePeriods { get; set; }
    public int ElectiveGroup { get; set; }
    public int RequiredCreditsInGroup { get; set; }
    public bool ExcludeFromGpa { get; set; }
    public bool CountsTowardProgramCredits { get; set; } = true;
    public bool IsDefaultSelection { get; set; }
    public bool IsCoreCourse { get; set; }
    public List<string> Prerequisites { get; set; } = [];
}

public sealed class TrainingProgram : Document
{
    public string ProgramCode { get; set; } = "";
    public string ProgramName { get; set; } = "";
    public FacultySnapshot Faculty { get; set; } = new();
    public string EducationLevel { get; set; } = "Đại học";
    public int RequiredCredits { get; set; } = 151;
    public int RequiredCompulsoryCredits { get; set; } = 128;
    public int RequiredElectiveCredits { get; set; } = 23;
    public string CurriculumVersion { get; set; } = "2023";
    public string ApplicableCohort { get; set; } = "2024";
    public int DurationYears { get; set; } = 4;
    public string Status { get; set; } = "Active";
    public List<ProgramCourseItem> Courses { get; set; } = [];
    public List<string> ProgramOutcomes { get; set; } = [];
}

public sealed class AcademicYearEntity : Document
{
    public string AcademicYearCode { get; set; } = "";
    public string AcademicYearName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class SemesterEntity : Document
{
    public string SemesterCode { get; set; } = "";
    public string SemesterName { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public string AcademicYearId { get; set; } = "";
    public string AcademicYearName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GradeEntryStart { get; set; }
    public DateTime GradeEntryEnd { get; set; }
    public DateTime PublishDate { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class LearningMaterial : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    public string ClassSectionCode { get; set; } = "";
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string LecturerCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Tài liệu";
    public string Chapter { get; set; } = "";
    public string ResourceType { get; set; } = "Link";
    public string ResourceUrl { get; set; } = "";
    public DateTime VisibleFrom { get; set; } = DateTime.UtcNow;
    public DateTime? VisibleUntil { get; set; }
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
    public string Status { get; set; } = "Published";
}

public sealed class Assignment : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    public string ClassSectionCode { get; set; } = "";
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string LecturerCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string AttachmentUrl { get; set; } = "";
    public double MaxScore { get; set; } = 10;
    public DateTime OpenAt { get; set; } = DateTime.UtcNow;
    public DateTime DueAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public bool AllowLate { get; set; }
    public double LatePenaltyPercent { get; set; }
    public List<string> CloCodes { get; set; } = [];
    public string? LinkedComponentId { get; set; }
    public string Status { get; set; } = "Open";
}

[BsonIgnoreExtraElements]
public sealed class SubmissionFile
{
    public string OriginalName { get; set; } = "";
    public string StoredName { get; set; } = "";
    public string Url { get; set; } = "";
    public long SizeBytes { get; set; }
    public string MimeType { get; set; } = "";
}

public sealed class Submission : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string AssignmentId { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    [BsonRepresentation(BsonType.ObjectId)] public string StudentId { get; set; } = "";
    public string StudentCode { get; set; } = "";
    public string StudentName { get; set; } = "";
    public string TextContent { get; set; } = "";
    public List<SubmissionFile> Files { get; set; } = [];
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public bool IsLate { get; set; }
    public string Status { get; set; } = "Submitted";
    public double? Score { get; set; }
    public string Feedback { get; set; } = "";
    public DateTime? GradedAt { get; set; }
    public string GradedBy { get; set; } = "";
    public bool ResubmissionAllowed { get; set; }
}

public sealed class ExamSchedule : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    public string ClassSectionCode { get; set; } = "";
    public string CourseCode { get; set; } = "";
    public string CourseName { get; set; } = "";
    public string ExamType { get; set; } = "Final";
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Room { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class SystemSetting : Document
{
    public string Key { get; set; } = "";
    public string Group { get; set; } = "General";
    public object? Value { get; set; }
    public string Description { get; set; } = "";
    public bool Editable { get; set; } = true;
}

public sealed class GradeReopenRequest : Document
{
    [BsonRepresentation(BsonType.ObjectId)] public string ClassSectionId { get; set; } = "";
    public string ClassSectionCode { get; set; } = "";
    public string LecturerCode { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public string ReviewedBy { get; set; } = "";
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNote { get; set; } = "";
}
