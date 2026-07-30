using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

public sealed record ReviewGradeReopenRequest(
    bool Approve,
    string Note);

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminAcademicController(
    AdminAcademicService service,
    IImportExportService importExport) : ControllerBase
{
    [HttpGet("grading-courses")]
    [RequirePermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AdminCourseOptionDto>>>> GradingCourses(
        CancellationToken ct)
    {
        var result = await service.GetGradingCoursesAsync(ct);
        return Ok(
            ApiResponse<IReadOnlyCollection<AdminCourseOptionDto>>.Ok(
                result));
    }

    [HttpGet("report-options")]
    [RequirePermission(AppPermissions.ReportsRead)]
    public async Task<ActionResult<ApiResponse<AdminReportOptionsDto>>> ReportOptions(
        CancellationToken ct)
    {
        var result = await service.GetReportOptionsAsync(ct);
        return Ok(ApiResponse<AdminReportOptionsDto>.Ok(result));
    }

    [HttpGet("notification-options")]
    [RequirePermission(AppPermissions.NotificationsManage)]
    public async Task<ActionResult<ApiResponse<AdminNotificationOptionsDto>>> NotificationOptions(
        CancellationToken ct)
    {
        var result = await service.GetNotificationOptionsAsync(ct);
        return Ok(ApiResponse<AdminNotificationOptionsDto>.Ok(result));
    }

    [HttpGet("courses/{courseId}/design")]
    [RequirePermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<CourseDesignDto>>> GetCourseDesign(
        string courseId,
        CancellationToken ct)
    {
        var result = await service.GetCourseDesignAsync(
            courseId,
            ct);

        return Ok(
            ApiResponse<CourseDesignDto>.Ok(result));
    }

    [HttpPut("courses/{courseId}/design")]
    [RequirePermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<ApiResponse<CourseDesignDto>>> SaveCourseDesign(
        string courseId,
        SaveCourseDesignRequest request,
        CancellationToken ct)
    {
        var result = await service.SaveCourseDesignAsync(
            courseId,
            request,
            User.UserId(),
            ct);

        return Ok(
            ApiResponse<CourseDesignDto>.Ok(
                result,
                "Đã tạo phiên bản cấu trúc điểm mới."));
    }

    [HttpGet("audit-logs")]
    [RequirePermission(AppPermissions.AuditRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> AuditLogs(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? action,
        [FromQuery] string? result,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var data = await service.GetAuditLogsAsync(
            search,
            role,
            action,
            result,
            from,
            to,
            Math.Max(pageNumber, 1),
            Math.Clamp(pageSize, 1, 100),
            ct);

        return Ok(
            ApiResponse<PagedResult<Dictionary<string, object?>>>.Ok(
                data));
    }

    [HttpGet("reports")]
    [RequirePermission(AppPermissions.ReportsRead)]
    public async Task<ActionResult<ApiResponse<AdminReportDto>>> Reports(
        [FromQuery] string? academicYearId,
        [FromQuery] string? semesterId,
        [FromQuery] string? facultyId,
        [FromQuery] string? programId,
        CancellationToken ct)
    {
        var result = await service.GetReportsAsync(
            academicYearId,
            semesterId,
            facultyId,
            programId,
            ct);

        return Ok(
            ApiResponse<AdminReportDto>.Ok(result));
    }

    [HttpGet("reports/export")]
    [RequirePermission(AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportReport(
        [FromQuery] string? academicYearId,
        [FromQuery] string? semesterId,
        [FromQuery] string? facultyId,
        [FromQuery] string? programId,
        CancellationToken ct)
    {
        var bytes = await service.ExportReportAsync(
            academicYearId,
            semesterId,
            facultyId,
            programId,
            ct);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument."
            + "spreadsheetml.sheet",
            $"BaoCaoDaoTao-{DateTime.UtcNow:yyyyMMddHHmm}.xlsx");
    }

    [HttpGet("reports/export-pdf")]
    [RequirePermission(AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportReportPdf(
        [FromQuery] string? academicYearId,
        [FromQuery] string? semesterId,
        [FromQuery] string? facultyId,
        [FromQuery] string? programId,
        CancellationToken ct)
    {
        var bytes = await service.ExportReportPdfAsync(
            academicYearId,
            semesterId,
            facultyId,
            programId,
            ct);

        return File(
            bytes,
            "application/pdf",
            $"BaoCaoDaoTao-{DateTime.UtcNow:yyyyMMddHHmm}.pdf");
    }

    [HttpGet("grade-reopen-requests")]
    [RequirePermission(AppPermissions.GradesReopen)]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> ReopenRequests(
        [FromQuery] string? status = "Pending",
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await service.GetReopenRequestsAsync(
            status,
            search,
            pageNumber,
            pageSize,
            ct);

        return Ok(
            ApiResponse<PagedResult<Dictionary<string, object?>>>.Ok(
                result));
    }

    [HttpGet("grade-reopen-requests/{id}")]
    [RequirePermission(AppPermissions.GradesReopen)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> ReopenRequest(
        string id,
        CancellationToken ct)
    {
        var result = await service.GetReopenRequestAsync(
            id,
            ct);

        return Ok(
            ApiResponse<Dictionary<string, object?>>.Ok(
                result));
    }

    [HttpPut("grade-reopen-requests/{id}")]
    [RequirePermission(AppPermissions.GradesReopen)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> ReviewReopenRequest(
        string id,
        ReviewGradeReopenRequest request,
        CancellationToken ct)
    {
        var result = await service.ReviewReopenRequestAsync(
            id,
            request.Approve,
            request.Note,
            User.UserId(),
            ct);

        return Ok(
            ApiResponse<Dictionary<string, object?>>.Ok(
                result,
                request.Approve
                    ? "Đã duyệt yêu cầu mở điểm."
                    : "Đã từ chối yêu cầu mở điểm."));
    }

    [HttpGet("export/{resource}")]
    [RequirePermission(AppPermissions.ImportExport)]
    public async Task<IActionResult> Export(
        string resource,
        CancellationToken ct)
    {
        var bytes = await importExport.ExportResourceAsync(
            resource,
            ct);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument."
            + "spreadsheetml.sheet",
            $"{resource}-{DateTime.UtcNow:yyyyMMddHHmm}.xlsx");
    }

    [HttpPost("import/{resource}")]
    [RequirePermission(AppPermissions.ImportExport)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportPreviewDto>>> ImportResource(
        string resource,
        [FromForm] IFormFile file,
        [FromQuery] bool commit = false,
        CancellationToken ct = default)
    {
        var result = await importExport.ImportResourceAsync(
            resource,
            file,
            commit,
            Actor(),
            ct);

        return Ok(
            ApiResponse<ImportPreviewDto>.Ok(
                result,
                commit
                    ? $"Import {resource} thành công."
                    : "Xem trước dữ liệu import thành công."));
    }

    private AdminActor Actor() =>
        new(
            User.UserId(),
            User.Identity?.Name ?? string.Empty,
            User.RoleName(),
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());
}
