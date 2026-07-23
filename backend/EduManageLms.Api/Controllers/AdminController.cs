using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(IAdminResourceService service, IBackupService backups) : ControllerBase
{
    private const string ResourcePattern = "^(users|students|lecturers|faculties|programs|academic-years|semesters|courses|class-sections|notifications|system-settings|grade-reopen-requests)$";

    [HttpGet("{resource:regex(" + ResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> List(
        string resource,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<Dictionary<string, object?>>>.Ok(await service.ListAsync(resource, search, Math.Max(1, pageNumber), Math.Clamp(pageSize, 1, 100), ct)));

    [HttpGet("{resource:regex(" + ResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Get(string resource, string id, CancellationToken ct) =>
        Ok(ApiResponse<Dictionary<string, object?>>.Ok(await service.GetAsync(resource, id, ct)));

    [HttpPost("{resource:regex(" + ResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Create(string resource, Dictionary<string, object?> body, CancellationToken ct) =>
        Ok(ApiResponse<Dictionary<string, object?>>.Ok(await service.CreateAsync(resource, body, ct), "Tạo dữ liệu thành công"));

    [HttpPut("{resource:regex(" + ResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Update(string resource, string id, Dictionary<string, object?> body, CancellationToken ct) =>
        Ok(ApiResponse<Dictionary<string, object?>>.Ok(await service.UpdateAsync(resource, id, body, ct), "Cập nhật thành công"));

    [HttpDelete("{resource:regex(" + ResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string resource, string id, CancellationToken ct)
    {
        await service.DeleteAsync(resource, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Xóa mềm thành công"));
    }

    [HttpPost("{resource:regex(" + ResourcePattern + ")}/{id}/restore")]
    public async Task<ActionResult<ApiResponse<object>>> RestoreResource(string resource, string id, CancellationToken ct)
    {
        await service.RestoreAsync(resource, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Khôi phục dữ liệu thành công"));
    }

    [HttpGet("backups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>>> BackupList(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>.Ok(await backups.ListAsync(ct)));

    [HttpPost("backups")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> Backup(CancellationToken ct) =>
        Ok(ApiResponse<Dictionary<string, object>>.Ok(await backups.CreateAsync(User.UserId(), ct), "Sao lưu thành công"));

    [HttpGet("backups/{id}/download")]
    public async Task<IActionResult> DownloadBackup(string id, CancellationToken ct)
    {
        var result = await backups.DownloadAsync(id, ct);
        return File(result.Content, "application/zip", result.FileName);
    }

    [HttpPost("backups/upload")]
    [RequestSizeLimit(500L * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> UploadBackup([FromForm] IFormFile file, CancellationToken ct) =>
        Ok(ApiResponse<Dictionary<string, object>>.Ok(await backups.UploadAsync(file, User.UserId(), ct), "Tải bản sao lưu lên thành công"));

    [HttpDelete("backups/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBackup(string id, CancellationToken ct)
    {
        await backups.DeleteAsync(id, User.UserId(), ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Xóa bản sao lưu thành công"));
    }

    [HttpPost("backups/{id}/restore")]
    public async Task<ActionResult<ApiResponse<object>>> RestoreBackup(string id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        await backups.RestoreAsync(id, User.UserId(), body.GetValueOrDefault("confirmation", string.Empty), ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Phục hồi thành công"));
    }
}
