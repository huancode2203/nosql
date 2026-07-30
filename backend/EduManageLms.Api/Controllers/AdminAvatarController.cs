using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users/{userId}/avatar")]
[Authorize(Roles = "Admin")]
public sealed class AdminAvatarController(IAdminAvatarService service)
    : ControllerBase
{
    [HttpPost]
    [RequirePermission(AppPermissions.UsersManageAvatars)]
    [RequestSizeLimit(AvatarFileValidator.MaximumBytes)]
    public async Task<ActionResult<ApiResponse<AdminAvatarDto>>> Upload(
        string userId,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        var result = await service.UploadAsync(
            userId,
            file,
            Actor(),
            ct);
        return Ok(ApiResponse<AdminAvatarDto>.Ok(
            result,
            "Đã cập nhật ảnh đại diện."));
    }

    [HttpDelete]
    [RequirePermission(AppPermissions.UsersManageAvatars)]
    public async Task<ActionResult<ApiResponse<AdminAvatarDto>>> Delete(
        string userId,
        CancellationToken ct)
    {
        var result = await service.DeleteAsync(
            userId,
            Actor(),
            ct);
        return Ok(ApiResponse<AdminAvatarDto>.Ok(
            result,
            "Đã xóa ảnh đại diện."));
    }

    private AdminActor Actor() =>
        new(
            User.UserId(),
            User.Identity?.Name ?? string.Empty,
            User.RoleName(),
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            Request.Headers.UserAgent.ToString());
}
