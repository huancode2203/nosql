using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

public sealed record PublishGradesRequest(string Reason);

[ApiController]
[Route("api/v1/admin/gradebooks")]
[Authorize(Roles = "Admin")]
public sealed class AdminGradePublicationController(
    AdminGradePublicationService service) : ControllerBase
{
    [HttpPost("{sectionId}/publish")]
    public async Task<ActionResult<ApiResponse<object>>> Publish(
        string sectionId,
        PublishGradesRequest request,
        CancellationToken ct)
    {
        await service.PublishAsync(
            sectionId,
            User.UserId(),
            request.Reason,
            ct);

        return Ok(
            ApiResponse<object>.Ok(
                new
                {
                    sectionId,
                    status = "Published"
                },
                "Công bố điểm thành công."));
    }
}
