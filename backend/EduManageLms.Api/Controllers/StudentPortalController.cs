using EduManageLms.Api.Application;
using EduManageLms.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduManageLms.Api.Controllers;

[ApiController]
[Route("api/v1/student")]
[Authorize(Roles = "Student")]
public sealed class StudentPortalController(IStudentPortalService service) : ControllerBase
{
    [HttpGet("current-courses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<StudentCourseDto>>>> CurrentCourses(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<StudentCourseDto>>.Ok(await service.GetCurrentCoursesAsync(User.StudentCode()!, ct)));

    [HttpGet("transcript")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TranscriptTermDto>>>> Transcript(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<TranscriptTermDto>>.Ok(await service.GetTranscriptAsync(User.StudentCode()!, ct)));


    [HttpGet("semester-options")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SemesterOptionDto>>>> SemesterOptions(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<SemesterOptionDto>>.Ok(await service.GetSemesterOptionsAsync(User.StudentCode()!, ct)));

    [HttpGet("semester-average-chart")]
    public async Task<ActionResult<ApiResponse<SemesterAverageChartDto>>> SemesterAverageChart([FromQuery] string semesterKey, CancellationToken ct) =>
        Ok(ApiResponse<SemesterAverageChartDto>.Ok(await service.GetSemesterAverageChartAsync(User.StudentCode()!, semesterKey, ct)));

    [HttpGet("curriculum")]
    public async Task<ActionResult<ApiResponse<StudentCurriculumDto>>> Curriculum(CancellationToken ct) =>
        Ok(ApiResponse<StudentCurriculumDto>.Ok(await service.GetCurriculumAsync(User.StudentCode()!, ct)));

    [HttpGet("schedule")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ScheduleItemDto>>>> Schedule(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<ScheduleItemDto>>.Ok(await service.GetScheduleAsync(User.StudentCode()!, ct)));

    [HttpGet("materials")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<MaterialDto>>>> Materials([FromQuery] string? classSectionId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<MaterialDto>>.Ok(await service.GetMaterialsAsync(User.StudentCode()!, classSectionId, ct)));

    [HttpGet("assignments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AssignmentDto>>>> Assignments([FromQuery] string? classSectionId, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyCollection<AssignmentDto>>.Ok(await service.GetAssignmentsAsync(User.StudentCode()!, classSectionId, ct)));

    [HttpPost("assignments/{id}/submit")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<SubmissionDto>>> Submit(
        string id,
        [FromForm] string textContent,
        [FromForm] List<IFormFile> files,
        CancellationToken ct) =>
        Ok(ApiResponse<SubmissionDto>.Ok(await service.SubmitAsync(User.StudentCode()!, id, new StudentSubmissionRequest(textContent), files, ct), "Nộp bài thành công"));

    [HttpGet("transcript/export")]
    public async Task<IActionResult> ExportTranscript(CancellationToken ct)
    {
        var bytes = await service.ExportTranscriptAsync(User.StudentCode()!, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"bang-diem-{User.StudentCode()}.xlsx");
    }
}
