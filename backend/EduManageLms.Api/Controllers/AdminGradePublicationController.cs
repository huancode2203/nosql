using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

public sealed record PublishGradesRequest(string Reason);
public sealed record ReturnGradebookRequest(string Reason);

[ApiController]
[Route("api/v1/admin/gradebooks")]
[Authorize(Roles = "Admin")]
public sealed class AdminGradePublicationController(
    AdminGradePublicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AdminGradebookSummaryDto>>>> List(
        [FromQuery] string? status = "Submitted",
        [FromQuery] string? search = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await service.ListAsync(
            status,
            search,
            pageNumber,
            pageSize,
            ct);

        return Ok(
            ApiResponse<PagedResult<AdminGradebookSummaryDto>>.Ok(result));
    }

    [HttpGet("{sectionId}")]
    public async Task<ActionResult<ApiResponse<AdminGradebookDetailDto>>> Get(
        string sectionId,
        CancellationToken ct)
    {
        var result = await service.GetAsync(sectionId, ct);

        return Ok(
            ApiResponse<AdminGradebookDetailDto>.Ok(result));
    }

    [HttpPost("{sectionId}/return")]
    public async Task<ActionResult<ApiResponse<object>>> ReturnToLecturer(
        string sectionId,
        ReturnGradebookRequest request,
        CancellationToken ct)
    {
        await service.ReturnToDraftAsync(
            sectionId,
            User.UserId(),
            request.Reason,
            ct);

        return Ok(
            ApiResponse<object>.Ok(
                new
                {
                    sectionId,
                    status = "Draft"
                },
                "Đã trả bảng điểm lại cho giảng viên."));
    }

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
