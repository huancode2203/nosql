using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class DashboardService(MongoContext db, IStudentAnalyticsService analytics) : IDashboardService
{
    public async Task<DashboardDto> AdminAsync(CancellationToken ct)
    {
        var studentCount = await db.Students.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct);
        var lecturerCount = await db.Lecturers.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct);
        var courseCount = await db.Courses.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct);
        var sections = await db.ClassSections.Find(x => !x.IsDeleted).ToListAsync(ct);
        var lockedUsers = await db.Users.CountDocumentsAsync(x => !x.IsDeleted && (x.Status == "Locked" || x.LockedUntil > DateTime.UtcNow), cancellationToken: ct);
        var unpublished = sections.Count(x => x.GradeStatus is not ("Published" or "Locked"));

        var cards = new List<DashboardCardDto>
        {
            new("Sinh viên", studentCount, "school", "Hồ sơ đang quản lý", "primary"),
            new("Giảng viên", lecturerCount, "badge", "Nhân sự giảng dạy", "success"),
            new("Môn học", courseCount, "menu_book", "Có cấu trúc điểm", "warning"),
            new("Lớp học phần", sections.Count, "class", $"{unpublished} bảng điểm chưa hoàn tất", unpublished > 0 ? "warning" : "success")
        };
        var distribution = sections.GroupBy(x => x.GradeStatus)
            .OrderByDescending(x => x.Count())
            .Select(x => new ChartItemDto(TranslateGradeStatus(x.Key), x.Count()))
            .ToList();
        var alerts = new List<AlertDto>();
        if (unpublished > 0) alerts.Add(new("Bảng điểm chưa hoàn tất", $"Còn {unpublished} lớp cần nhập hoặc công bố điểm", "warning"));
        if (lockedUsers > 0) alerts.Add(new("Tài khoản bị khóa", $"Có {lockedUsers} tài khoản cần được kiểm tra", "danger"));
        var lastBackup = await db.BackupHistories.Find(x => x.Status == "Success" && !x.IsDeleted).SortByDescending(x => x.CompletedAt).FirstOrDefaultAsync(ct);
        alerts.Add(lastBackup is null
            ? new AlertDto("Chưa có bản sao lưu", "Hãy tạo bản sao lưu dữ liệu đầu tiên", "warning")
            : new AlertDto("Sao lưu gần nhất", $"{lastBackup.FileName} đã hoàn tất", "success"));

        return new DashboardDto(cards, distribution, await RecentActivitiesAsync(null, ct), alerts);
    }

    public async Task<DashboardDto> LecturerAsync(string lecturerCode, CancellationToken ct)
    {
        var sections = await db.ClassSections.Find(x => x.LecturerCode == lecturerCode && !x.IsDeleted).ToListAsync(ct);
        var sectionIds = sections.Select(x => x.Id).ToList();
        var pendingSubmissions = sectionIds.Count == 0
            ? 0
            : await db.Submissions.CountDocumentsAsync(
                Builders<Submission>.Filter.In(x => x.ClassSectionId, sectionIds) &
                Builders<Submission>.Filter.Eq(x => x.IsDeleted, false) &
                Builders<Submission>.Filter.In(x => x.Status, new[] { "Submitted", "Late" }),
                cancellationToken: ct);
        var unpublished = sections.Count(x => x.GradeStatus is not ("Published" or "Locked"));
        var students = sections.SelectMany(x => x.Students).Select(x => x.StudentId).Distinct().Count();

        var cards = new List<DashboardCardDto>
        {
            new("Lớp phụ trách", sections.Count, "class", "Theo phân công hiện tại", "primary"),
            new("Sinh viên", students, "groups", "Không tính trùng sinh viên", "success"),
            new("Chưa công bố", unpublished, "pending_actions", "Bảng điểm cần hoàn tất", unpublished > 0 ? "warning" : "success"),
            new("Bài chưa chấm", pendingSubmissions, "assignment_late", "Bài nộp đang chờ", pendingSubmissions > 0 ? "danger" : "success")
        };
        var distribution = sections.GroupBy(x => x.GradeStatus)
            .Select(x => new ChartItemDto(TranslateGradeStatus(x.Key), x.Count()))
            .ToList();
        var alerts = new List<AlertDto>();
        if (unpublished > 0) alerts.Add(new("Tiến độ nhập điểm", $"{unpublished} lớp chưa công bố bảng điểm", "warning"));
        if (pendingSubmissions > 0) alerts.Add(new("Bài nộp chờ chấm", $"Có {pendingSubmissions} bài nộp cần xử lý", "danger"));
        if (alerts.Count == 0) alerts.Add(new("Công việc đã cập nhật", "Không có tác vụ quá hạn", "success"));

        return new DashboardDto(cards, distribution, await RecentActivitiesAsync(lecturerCode, ct), alerts);
    }

    public async Task<DashboardDto> StudentAsync(string studentCode, CancellationToken ct)
    {
        var student = await db.Students.Find(x => x.StudentCode == studentCode && !x.IsDeleted).FirstOrDefaultAsync(ct);
        var gpa = await analytics.GetCumulativeGpaAsync(studentCode, ct);
        var grades = await analytics.GetGradesAsync(studentCode, null, null, ct);
        var requiredCredits = student?.Program.RequiredCredits > 0 ? student.Program.RequiredCredits : 130;
        var progress = requiredCredits == 0 ? 0 : Math.Round(gpa.PassedCredits * 100d / requiredCredits, 1);

        var cards = new List<DashboardCardDto>
        {
            new("GPA tích lũy", gpa.Gpa, "insights", gpa.Classification, "primary"),
            new("Tín chỉ tích lũy", gpa.PassedCredits, "workspace_premium", $"{progress}% / {requiredCredits} tín chỉ", "success"),
            new("Môn đã đạt", grades.Count(x => x.Passed), "task_alt", "Chỉ tính điểm đã công bố", "success"),
            new("Môn chưa đạt", grades.Count(x => !x.Passed), "warning", "Có thể cần học lại", grades.Any(x => !x.Passed) ? "danger" : "success")
        };
        var distribution = grades.GroupBy(x => x.LetterGrade)
            .OrderBy(x => GradeOrder(x.Key))
            .Select(x => new ChartItemDto(x.Key, x.Count()))
            .ToList();

        var alerts = new List<AlertDto>();
        var activeClassIds = student?.AcademicRecords.SelectMany(x => x.Semesters).SelectMany(x => x.Courses)
            .Where(x => x.ScoreStatus is "Draft" or "InProgress")
            .Select(x => x.ClassSectionId).Distinct().ToList() ?? [];
        var upcomingAssignments = activeClassIds.Count == 0
            ? 0
            : await db.Assignments.CountDocumentsAsync(
                Builders<Assignment>.Filter.In(x => x.ClassSectionId, activeClassIds) &
                Builders<Assignment>.Filter.Gte(x => x.DueAt, DateTime.UtcNow) &
                Builders<Assignment>.Filter.Lte(x => x.DueAt, DateTime.UtcNow.AddDays(7)) &
                Builders<Assignment>.Filter.Eq(x => x.IsDeleted, false),
                cancellationToken: ct);
        if (upcomingAssignments > 0) alerts.Add(new("Bài tập sắp hết hạn", $"Có {upcomingAssignments} bài tập đến hạn trong 7 ngày", "warning"));
        if (grades.Any(x => !x.Passed)) alerts.Add(new("Môn học cần cải thiện", "Có kết quả chưa đạt trong bảng điểm đã công bố", "danger"));
        if (alerts.Count == 0) alerts.Add(new("Tiến độ ổn định", "Không có cảnh báo học tập mới", "success"));

        return new DashboardDto(cards, distribution, await RecentActivitiesAsync(studentCode, ct), alerts);
    }

    private async Task<IReadOnlyCollection<ActivityDto>> RecentActivitiesAsync(string? actor, CancellationToken ct)
    {
        var filter = Builders<AuditLog>.Filter.Eq(x => x.IsDeleted, false);
        if (!string.IsNullOrWhiteSpace(actor))
        {
            filter &= Builders<AuditLog>.Filter.Or(
                Builders<AuditLog>.Filter.Eq(x => x.UserName, actor),
                Builders<AuditLog>.Filter.Eq(x => x.UserId, actor));
        }

        var items = await db.AuditLogs.Find(filter).SortByDescending(x => x.CreatedAt).Limit(6).ToListAsync(ct);
        return items.Select(x => new ActivityDto(
            ActivityTitle(x.Action),
            string.IsNullOrWhiteSpace(x.Entity) ? x.Note ?? "Hoạt động hệ thống" : $"{x.Entity}{(string.IsNullOrWhiteSpace(x.EntityId) ? "" : $" · {x.EntityId}")}",
            RelativeTime(x.CreatedAt),
            ActivityIcon(x.Action))).ToList();
    }

    private static string TranslateGradeStatus(string value) => value switch
    {
        "Draft" => "Nháp",
        "InProgress" => "Đang nhập",
        "Submitted" => "Đã gửi",
        "Published" => "Đã công bố",
        "Locked" => "Đã khóa",
        "Reopened" => "Đã mở lại",
        _ => value
    };

    private static int GradeOrder(string value) => value switch
    {
        "A" => 1, "B+" => 2, "B" => 3, "C+" => 4, "C" => 5, "D+" => 6, "D" => 7, _ => 8
    };

    private static string ActivityTitle(string action) => action switch
    {
        "PublishGrades" => "Công bố bảng điểm",
        "SaveGrades" => "Cập nhật bảng điểm",
        "Login" => "Đăng nhập hệ thống",
        "CreateBackup" => "Tạo bản sao lưu",
        "RestoreBackup" => "Phục hồi dữ liệu",
        _ => string.IsNullOrWhiteSpace(action) ? "Hoạt động hệ thống" : action
    };

    private static string ActivityIcon(string action) => action switch
    {
        "PublishGrades" => "publish",
        "SaveGrades" => "edit_note",
        "Login" => "login",
        "CreateBackup" => "backup",
        "RestoreBackup" => "restore",
        _ => "history"
    };

    private static string RelativeTime(DateTime value)
    {
        var diff = DateTime.UtcNow - value.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalHours < 1) return $"{Math.Max(1, (int)diff.TotalMinutes)} phút trước";
        if (diff.TotalDays < 1) return $"{Math.Max(1, (int)diff.TotalHours)} giờ trước";
        if (diff.TotalDays < 7) return $"{Math.Max(1, (int)diff.TotalDays)} ngày trước";
        return value.ToLocalTime().ToString("dd/MM/yyyy");
    }
}
