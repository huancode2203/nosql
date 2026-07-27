using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/lecturer")]
[Authorize(Roles = "Lecturer")]
public sealed class LecturerController(IGradebookService service) : ControllerBase
{
    [HttpGet("classes/{id}/gradebook")]
    public async Task<ActionResult<ApiResponse<GradebookDto>>> Gradebook(
        string id,
        CancellationToken ct)
    {
        var data = await service.GetAsync(
            User.LecturerCode()!,
            id,
            ct);

        return Ok(ApiResponse<GradebookDto>.Ok(data));
    }

    [HttpPut("classes/{id}/grades")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string id,
        GradeUpdateRequest request,
        CancellationToken ct)
    {
        await service.UpdateAsync(
            User.LecturerCode()!,
            id,
            request,
            User.UserId(),
            ct);

        var status = request.Publish ? "Submitted" : "Draft";
        var message = request.Publish
            ? "Đã gửi bảng điểm để quản trị viên kiểm tra."
            : "Lưu bản nháp thành công.";

        return Ok(
            ApiResponse<object>.Ok(
                new { status },
                message));
    }
}
