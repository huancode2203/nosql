using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminController(
    IAdminResourceService resources,
    IBackupService backups) : ControllerBase
{
    private const string SupportedResourcePattern =
        "^(users|students|lecturers|faculties|programs|academic-years|semesters|courses|class-sections|notifications|system-settings|grade-reopen-requests)$";

    [HttpGet("{resource:regex(" + SupportedResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> List(
        string resource,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await resources.ListAsync(
            resource,
            search,
            Math.Max(1, pageNumber),
            Math.Clamp(pageSize, 1, 100),
            ct);

        return Ok(ApiResponse<PagedResult<Dictionary<string, object?>>>.Ok(result));
    }

    [HttpGet("{resource:regex(" + SupportedResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Get(
        string resource,
        string id,
        CancellationToken ct)
    {
        var result = await resources.GetAsync(resource, id, ct);
        return Ok(ApiResponse<Dictionary<string, object?>>.Ok(result));
    }

    [HttpPost("{resource:regex(" + SupportedResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Create(
        string resource,
        [FromBody] Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var result = await resources.CreateAsync(resource, body, ct);

        return Ok(
            ApiResponse<Dictionary<string, object?>>.Ok(
                result,
                "Tạo dữ liệu thành công"));
    }

    [HttpPut("{resource:regex(" + SupportedResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Update(
        string resource,
        string id,
        [FromBody] Dictionary<string, object?> body,
        CancellationToken ct)
    {
        var result = await resources.UpdateAsync(resource, id, body, ct);

        return Ok(
            ApiResponse<Dictionary<string, object?>>.Ok(
                result,
                "Cập nhật thành công"));
    }

    [HttpDelete("{resource:regex(" + SupportedResourcePattern + ")}/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string resource,
        string id,
        CancellationToken ct)
    {
        await resources.DeleteAsync(resource, id, ct);

        return Ok(
            ApiResponse<object>.Ok(
                new { id, resource },
                "Xóa mềm thành công"));
    }

    [HttpPost("{resource:regex(" + SupportedResourcePattern + ")}/{id}/restore")]
    public async Task<ActionResult<ApiResponse<object>>> RestoreResource(
        string resource,
        string id,
        CancellationToken ct)
    {
        await resources.RestoreAsync(resource, id, ct);

        return Ok(
            ApiResponse<object>.Ok(
                new { id, resource },
                "Khôi phục dữ liệu thành công"));
    }

    [HttpGet("backups")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>>> BackupList(
        CancellationToken ct)
    {
        var result = await backups.ListAsync(ct);

        return Ok(
            ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>.Ok(
                result));
    }

    [HttpPost("backups")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> Backup(
        CancellationToken ct)
    {
        var result = await backups.CreateAsync(User.UserId(), ct);

        return Ok(
            ApiResponse<Dictionary<string, object>>.Ok(
                result,
                "Sao lưu thành công"));
    }

    [HttpPost("backups/{id}/restore")]
    public async Task<ActionResult<ApiResponse<object>>> RestoreBackup(
        string id,
        [FromBody] Dictionary<string, string> body,
        CancellationToken ct)
    {
        await backups.RestoreAsync(
            id,
            User.UserId(),
            body.GetValueOrDefault("confirmation", string.Empty),
            ct);

        return Ok(
            ApiResponse<object>.Ok(
                new { id },
                "Phục hồi thành công"));
    }
}
