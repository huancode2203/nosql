using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class ProfileController(IProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Get(CancellationToken ct) =>
        Ok(ApiResponse<UserProfileDto>.Ok(await service.GetAsync(User.UserId(), ct)));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Update(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(ApiResponse<UserProfileDto>.Ok(await service.UpdateAsync(User.UserId(), request, ct), "Cập nhật hồ sơ thành công"));
}
