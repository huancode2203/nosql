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
    [HttpGet("courses/{courseId}/design")]
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
    public async Task<ActionResult<ApiResponse<AdminReportDto>>> Reports(
        CancellationToken ct)
    {
        var result = await service.GetReportsAsync(ct);

        return Ok(
            ApiResponse<AdminReportDto>.Ok(result));
    }

    [HttpGet("grade-reopen-requests")]
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

    [HttpPost("import/students")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportPreviewDto>>> ImportStudents(
        [FromForm] IFormFile file,
        [FromQuery] bool commit = false,
        CancellationToken ct = default)
    {
        var result = await importExport.ImportStudentsAsync(
            file,
            commit,
            ct);

        return Ok(
            ApiResponse<ImportPreviewDto>.Ok(
                result,
                commit
                    ? "Import sinh viên thành công."
                    : "Xem trước dữ liệu import thành công."));
    }
}
