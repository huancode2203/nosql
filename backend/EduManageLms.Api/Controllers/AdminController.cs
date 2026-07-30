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
        "^(users|students|lecturers|faculties|programs|academic-years|semesters|courses|class-sections|notifications|system-settings)$";

    [HttpGet("{resource:regex(" + SupportedResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<PagedResult<Dictionary<string, object?>>>>> List(
        string resource,
        [FromQuery] string? search,
        [FromQuery] bool deletedOnly = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        EnsureResourcePermission(resource, write: false);
        var result = await resources.ListAsync(
            resource,
            search,
            deletedOnly,
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
        EnsureResourcePermission(resource, write: false);
        var result = await resources.GetAsync(resource, id, ct);
        return Ok(ApiResponse<Dictionary<string, object?>>.Ok(result));
    }

    [HttpPost("{resource:regex(" + SupportedResourcePattern + ")}")]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object?>>>> Create(
        string resource,
        [FromBody] Dictionary<string, object?> body,
        CancellationToken ct)
    {
        EnsureResourcePermission(resource, write: true);
        EnsureCanChangeUserPermissions(resource, body);
        PrepareNotificationBody(resource, body, creating: true);
        var result = await resources.CreateAsync(resource, body, Actor(), ct);

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
        EnsureResourcePermission(resource, write: true);
        EnsureCanChangeUserPermissions(resource, body);
        PrepareNotificationBody(resource, body, creating: false);
        var result = await resources.UpdateAsync(resource, id, body, Actor(), ct);

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
        EnsureResourcePermission(resource, write: true, deleting: true);
        await resources.DeleteAsync(resource, id, Actor(), ct);

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
        EnsureResourcePermission(resource, write: true, deleting: true);
        await resources.RestoreAsync(resource, id, Actor(), ct);

        return Ok(
            ApiResponse<object>.Ok(
                new { id, resource },
                "Khôi phục dữ liệu thành công"));
    }

    [HttpGet("backups")]
    [RequirePermission(AppPermissions.BackupsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>>> BackupList(
        CancellationToken ct)
    {
        var result = await backups.ListAsync(ct);

        return Ok(
            ApiResponse<IReadOnlyCollection<Dictionary<string, object?>>>.Ok(
                result));
    }

    [HttpPost("backups")]
    [RequirePermission(AppPermissions.BackupsManage)]
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
    [RequirePermission(AppPermissions.BackupsManage)]
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

    [HttpPost("backups/upload")]
    [RequirePermission(AppPermissions.BackupsManage)]
    [RequestSizeLimit(500L * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<Dictionary<string, object>>>> UploadBackup(
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var result = await backups.UploadAsync(file, User.UserId(), ct);

        return Ok(
            ApiResponse<Dictionary<string, object>>.Ok(
                result,
                "Tải bản sao lưu lên thành công"));
    }

    [HttpGet("backups/{id}/download")]
    [RequirePermission(AppPermissions.BackupsRead)]
    public async Task<IActionResult> DownloadBackup(
        string id,
        CancellationToken ct)
    {
        var result = await backups.DownloadAsync(id, ct);
        return File(result.Content, "application/zip", result.FileName);
    }

    [HttpDelete("backups/{id}")]
    [RequirePermission(AppPermissions.BackupsManage)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBackup(
        string id,
        CancellationToken ct)
    {
        await backups.DeleteAsync(id, User.UserId(), ct);

        return Ok(
            ApiResponse<object>.Ok(
                new { id },
                "Xóa bản sao lưu thành công"));
    }

    private AdminActor Actor() =>
        new(
            User.UserId(),
            User.Identity?.Name ?? string.Empty,
            User.RoleName(),
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());

    private void EnsureResourcePermission(
        string resource,
        bool write,
        bool deleting = false)
    {
        var permission = resource.ToLowerInvariant() switch
        {
            "notifications" => AppPermissions.NotificationsManage,
            "system-settings" => AppPermissions.SettingsManage,
            _ when deleting => AppPermissions.ResourcesDelete,
            _ when write => AppPermissions.ResourcesWrite,
            _ => AppPermissions.ResourcesRead
        };

        if (!User.HasPermission(permission))
            throw new ForbiddenException();
    }

    private void EnsureCanChangeUserPermissions(
        string resource,
        IReadOnlyDictionary<string, object?> body)
    {
        if (resource.Equals("users", StringComparison.OrdinalIgnoreCase)
            && body.Keys.Any(
                key => key.Equals(
                    "permissions",
                    StringComparison.OrdinalIgnoreCase))
            && !User.HasPermission(AppPermissions.UsersManagePermissions))
        {
            throw new ForbiddenException(
                "Bạn không có quyền thay đổi quyền của tài khoản");
        }
    }

    private void PrepareNotificationBody(
        string resource,
        IDictionary<string, object?> body,
        bool creating)
    {
        if (!resource.Equals(
                "notifications",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (creating)
        {
            body["senderId"] = User.UserId();
        }
        else
        {
            body.Remove("senderId");
        }
    }
}
