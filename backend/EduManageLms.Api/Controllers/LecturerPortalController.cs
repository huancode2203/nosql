using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/lecturer")]
[Authorize(Roles = "Lecturer")]
public sealed class LecturerPortalController(ILecturerPortalService service) : ControllerBase
{
    [HttpGet("classes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LecturerClassDto>>>> Classes(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<LecturerClassDto>>.Ok(await service.GetClassesAsync(User.LecturerCode()!, ct)));

    [HttpGet("classes/{id}/students")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ClassStudentDto>>>> Students(string id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<ClassStudentDto>>.Ok(await service.GetStudentsAsync(User.LecturerCode()!, id, ct)));

    [HttpGet("classes/{id}/statistics")]
    public async Task<ActionResult<ApiResponse<ClassStatisticsDto>>> Statistics(string id, CancellationToken ct) =>
        Ok(ApiResponse<ClassStatisticsDto>.Ok(await service.GetStatisticsAsync(User.LecturerCode()!, id, ct)));

    [HttpGet("classes/{id}/clo")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ClassCloStatisticsDto>>>> Clo(string id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<ClassCloStatisticsDto>>.Ok(await service.GetCloStatisticsAsync(User.LecturerCode()!, id, ct)));

    [HttpGet("materials")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MaterialDto>>>> Materials([FromQuery] string? classSectionId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<MaterialDto>>.Ok(await service.GetMaterialsAsync(User.LecturerCode()!, classSectionId, ct)));

    [HttpPost("materials")]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> CreateMaterial(MaterialUpsertRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MaterialDto>.Ok(await service.SaveMaterialAsync(User.LecturerCode()!, null, request, ct), "Tạo tài liệu thành công"));

    [HttpPut("materials/{id}")]
    public async Task<ActionResult<ApiResponse<MaterialDto>>> UpdateMaterial(string id, MaterialUpsertRequest request, CancellationToken ct) =>
        Ok(ApiResponse<MaterialDto>.Ok(await service.SaveMaterialAsync(User.LecturerCode()!, id, request, ct), "Cập nhật tài liệu thành công"));

    [HttpDelete("materials/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMaterial(string id, CancellationToken ct)
    {
        await service.DeleteMaterialAsync(User.LecturerCode()!, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Xóa tài liệu thành công"));
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AssignmentDto>>>> Assignments([FromQuery] string? classSectionId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<AssignmentDto>>.Ok(await service.GetAssignmentsAsync(User.LecturerCode()!, classSectionId, ct)));

    [HttpPost("assignments")]
    public async Task<ActionResult<ApiResponse<AssignmentDto>>> CreateAssignment(AssignmentUpsertRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AssignmentDto>.Ok(await service.SaveAssignmentAsync(User.LecturerCode()!, null, request, ct), "Tạo bài tập thành công"));

    [HttpPut("assignments/{id}")]
    public async Task<ActionResult<ApiResponse<AssignmentDto>>> UpdateAssignment(string id, AssignmentUpsertRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AssignmentDto>.Ok(await service.SaveAssignmentAsync(User.LecturerCode()!, id, request, ct), "Cập nhật bài tập thành công"));

    [HttpDelete("assignments/{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAssignment(string id, CancellationToken ct)
    {
        await service.DeleteAssignmentAsync(User.LecturerCode()!, id, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Xóa bài tập thành công"));
    }

    [HttpGet("assignments/{id}/submissions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SubmissionDto>>>> Submissions(string id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<SubmissionDto>>.Ok(await service.GetSubmissionsAsync(User.LecturerCode()!, id, ct)));

    [HttpPut("submissions/{id}/grade")]
    public async Task<ActionResult<ApiResponse<object>>> GradeSubmission(string id, GradeSubmissionRequest request, CancellationToken ct)
    {
        await service.GradeSubmissionAsync(User.LecturerCode()!, id, request, ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Chấm bài thành công"));
    }

    [HttpPost("classes/{id}/request-reopen")]
    public async Task<ActionResult<ApiResponse<object>>> RequestReopen(string id, [FromBody] Dictionary<string, string> body, CancellationToken ct)
    {
        await service.RequestReopenAsync(User.LecturerCode()!, id, body.GetValueOrDefault("reason", ""), ct);
        return Ok(ApiResponse<object>.Ok(new { }, "Đã gửi yêu cầu mở lại bảng điểm"));
    }

    [HttpGet("classes/{id}/gradebook/export")]
    public async Task<IActionResult> ExportGradebook(string id, CancellationToken ct)
    {
        var bytes = await service.ExportGradebookAsync(User.LecturerCode()!, id, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bang-diem-{id}.xlsx");
    }

    [HttpPost("classes/{id}/grades/import")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportPreviewDto>>> ImportGrades(string id, [FromForm] IFormFile file, [FromQuery] bool commit = false, CancellationToken ct = default) =>
        Ok(ApiResponse<ImportPreviewDto>.Ok(await service.ImportGradesAsync(User.LecturerCode()!, id, file, commit, User.UserId(), ct), commit ? "Import điểm thành công" : "Xem trước dữ liệu điểm thành công"));
}
