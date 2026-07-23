using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminAcademicController(IAdminAcademicService service, IImportExportService importExport) : ControllerBase
{
    [HttpGet("courses/{courseId}/design")]
    public async Task<ActionResult<ApiResponse<CourseDesignDto>>> GetCourseDesign(string courseId, CancellationToken ct) =>
        Ok(ApiResponse<CourseDesignDto>.Ok(await service.GetCourseDesignAsync(courseId, ct)));

    [HttpPut("courses/{courseId}/design")]
    public async Task<ActionResult<ApiResponse<CourseDesignDto>>> SaveCourseDesign(string courseId, SaveCourseDesignRequest request, CancellationToken ct) =>
        Ok(ApiResponse<CourseDesignDto>.Ok(await service.SaveCourseDesignAsync(courseId, request, User.UserId(), ct), "Đã tạo phiên bản cấu trúc điểm mới"));

    [HttpGet("audit-logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> AuditLogs(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<Dictionary<string, object?>>>.Ok(await service.GetAuditLogsAsync(search, Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100), ct)));

    [HttpGet("reports")]
    public async Task<ActionResult<ApiResponse<AdminReportDto>>> Reports(CancellationToken ct) =>
        Ok(ApiResponse<AdminReportDto>.Ok(await service.GetReportsAsync(ct)));

    [HttpPut("grade-reopen-requests/{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> ReviewReopenRequest(
        string id,
        [FromBody] Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var approve = body.TryGetValue("approve", out var value) && value is System.Text.Json.JsonElement json && json.ValueKind == System.Text.Json.JsonValueKind.True;
        var note = body.TryGetValue("note", out var noteValue) ? noteValue?.ToString() ?? "" : "";
        return Ok(ApiResponse<Dictionary<string, object?>>.Ok(await service.ReviewReopenRequestAsync(id, approve, note, User.UserId(), ct), "Đã xử lý yêu cầu mở điểm"));
    }

    [HttpGet("export/{resource}")]
    public async Task<IActionResult> Export(string resource, CancellationToken ct)
    {
        var bytes = await importExport.ExportResourceAsync(resource, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{resource}-{DateTime.UtcNow:yyyyMMddHHmm}.xlsx");
    }

    [HttpPost("import/students")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportPreviewDto>>> ImportStudents([FromForm] IFormFile file, [FromQuery] bool commit = false, CancellationToken ct = default) =>
        Ok(ApiResponse<ImportPreviewDto>.Ok(await importExport.ImportStudentsAsync(file, commit, ct), commit ? "Import sinh viên thành công" : "Xem trước dữ liệu import thành công"));
}
