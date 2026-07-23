using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService auth, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken ct)
    {
        var data = await auth.LoginAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString(),
            ct);
        return Ok(ApiResponse<LoginResponse>.Ok(data, "Đăng nhập thành công"));
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(ApiResponse<LoginResponse>.Ok(
            await auth.RefreshAsync(request.RefreshToken, Request.Headers.UserAgent.ToString(), ct),
            "Làm mới token thành công"));

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var code = await auth.ForgotPasswordAsync(
            request.Email,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            ct);
        object data = environment.IsDevelopment() && code is not null
            ? new { demoCode = code, expiresInMinutes = 10 }
            : new { expiresInMinutes = 10 };
        return Ok(ApiResponse<object>.Ok(data, "Nếu email tồn tại, mã xác nhận đã được tạo"));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request.Email, request.Code, request.NewPassword, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đặt lại mật khẩu thành công"));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.RevokeAsync(request.RefreshToken, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đăng xuất thành công"));
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> LogoutAll(CancellationToken ct)
    {
        await auth.RevokeAllAsync(User.UserId(), ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã đăng xuất khỏi tất cả thiết bị"));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(
        [FromBody] Dictionary<string, string> body,
        CancellationToken ct)
    {
        await auth.ChangePasswordAsync(
            User.UserId(),
            body.GetValueOrDefault("currentPassword", string.Empty),
            body.GetValueOrDefault("newPassword", string.Empty),
            ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đổi mật khẩu thành công"));
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<object>> Me() =>
        Ok(ApiResponse<object>.Ok(new
        {
            id = User.UserId(),
            username = User.Identity?.Name,
            email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            fullName = User.FindFirst("fullName")?.Value,
            role = User.RoleName()
        }));
}
