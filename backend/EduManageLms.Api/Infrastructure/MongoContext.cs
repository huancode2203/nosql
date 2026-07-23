using EduManageLms.Api.Domain;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EduManageLms.Api.Infrastructure;

public sealed class MongoContext
{
    public IMongoDatabase Database { get; }
    public MongoOptions Options { get; }

    public MongoContext(IOptions<MongoOptions> options)
    {
        Options = options.Value;
        var settings = MongoClientSettings.FromConnectionString(Options.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
        Database = new MongoClient(settings).GetDatabase(Options.DatabaseName);
    }

    public IMongoCollection<User> Users => Database.GetCollection<User>("users");
    public IMongoCollection<Student> Students => Database.GetCollection<Student>("students");
    public IMongoCollection<Lecturer> Lecturers => Database.GetCollection<Lecturer>("lecturers");
    public IMongoCollection<Course> Courses => Database.GetCollection<Course>("courses");
    public IMongoCollection<ClassSection> ClassSections => Database.GetCollection<ClassSection>("classSections");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("auditLogs");
    public IMongoCollection<BackupHistory> BackupHistories => Database.GetCollection<BackupHistory>("backupHistories");
    public IMongoCollection<LoginHistory> LoginHistories => Database.GetCollection<LoginHistory>("loginHistories");
    public IMongoCollection<PasswordResetToken> PasswordResetTokens => Database.GetCollection<PasswordResetToken>("passwordResetTokens");

    public IMongoCollection<Faculty> Faculties => Database.GetCollection<Faculty>("faculties");
    public IMongoCollection<TrainingProgram> Programs => Database.GetCollection<TrainingProgram>("programs");
    public IMongoCollection<AcademicYearEntity> AcademicYears => Database.GetCollection<AcademicYearEntity>("academicYears");
    public IMongoCollection<SemesterEntity> Semesters => Database.GetCollection<SemesterEntity>("semesters");
    public IMongoCollection<LearningMaterial> Materials => Database.GetCollection<LearningMaterial>("materials");
    public IMongoCollection<Assignment> Assignments => Database.GetCollection<Assignment>("assignments");
    public IMongoCollection<Submission> Submissions => Database.GetCollection<Submission>("submissions");
    public IMongoCollection<ExamSchedule> ExamSchedules => Database.GetCollection<ExamSchedule>("examSchedules");
    public IMongoCollection<SystemSetting> SystemSettings => Database.GetCollection<SystemSetting>("systemSettings");
    public IMongoCollection<GradeReopenRequest> GradeReopenRequests => Database.GetCollection<GradeReopenRequest>("gradeReopenRequests");
}
