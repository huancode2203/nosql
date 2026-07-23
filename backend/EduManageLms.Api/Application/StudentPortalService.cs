using ClosedXML.Excel;
using EduManageLms.Api.Common;
using EduManageLms.Api.Domain;
using EduManageLms.Api.Infrastructure;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Application;

public sealed class StudentPortalService(MongoContext db, IStudentAnalyticsService analytics) : IStudentPortalService
{
    private static readonly string[] AllowedExtensions = [".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".zip", ".txt", ".png", ".jpg", ".jpeg"];

    public async Task<IReadOnlyCollection<StudentCourseDto>> GetCurrentCoursesAsync(string studentCode, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var allRecords = student.AcademicRecords
            .SelectMany(y => y.Semesters.SelectMany(s => s.Courses.Select(c => new { Year = y, Semester = s, Course = c })))
            .OrderByDescending(x => x.Year.AcademicYearName)
            .ThenByDescending(x => ParseSemesterNumber(x.Semester.SemesterName))
            .ToList();
        var activeTerm = allRecords
            .GroupBy(x => new { x.Year.AcademicYearName, x.Semester.SemesterCode, x.Semester.SemesterName })
            .OrderByDescending(x => x.Key.AcademicYearName)
            .ThenByDescending(x => ParseSemesterNumber(x.Key.SemesterName))
            .FirstOrDefault(x => x.Any(item => item.Course.ScoreStatus != "Published"))
            ?? allRecords.GroupBy(x => new { x.Year.AcademicYearName, x.Semester.SemesterCode, x.Semester.SemesterName })
                .OrderByDescending(x => x.Key.AcademicYearName)
                .ThenByDescending(x => ParseSemesterNumber(x.Key.SemesterName))
                .FirstOrDefault();
        if (activeTerm is null) return [];
        var records = activeTerm.ToList();
        var sectionIds = records.Select(x => x.Course.ClassSectionId).Distinct().ToList();
        var sections = await db.ClassSections.Find(Builders<ClassSection>.Filter.In(x => x.Id, sectionIds)).ToListAsync(ct);
        var lookup = sections.ToDictionary(x => x.Id);
        return records.Select(x =>
        {
            lookup.TryGetValue(x.Course.ClassSectionId, out var section);
            return new StudentCourseDto(
                x.Course.ClassSectionId,
                x.Course.ClassSectionCode,
                x.Course.CourseCode,
                x.Course.CourseName,
                x.Course.Credits,
                x.Course.Lecturer.FullName,
                x.Year.AcademicYearName,
                x.Semester.SemesterCode,
                x.Semester.SemesterName,
                x.Course.ScoreStatus,
                section?.Schedule ?? []);
        }).ToList();
    }

    public Task<IReadOnlyCollection<TranscriptTermDto>> GetTranscriptAsync(string studentCode, CancellationToken ct) =>
        analytics.GetTranscriptAsync(studentCode, ct);

    public async Task<IReadOnlyCollection<SemesterOptionDto>> GetSemesterOptionsAsync(string studentCode, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        return student.AcademicRecords
            .SelectMany(year => year.Semesters.Select(semester => new
            {
                Year = year.AcademicYearName,
                Semester = semester,
                SemesterNumber = ParseSemesterNumber(semester.SemesterName)
            }))
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.SemesterNumber)
            .Select(x => new SemesterOptionDto(
                BuildSemesterKey(x.Year, x.Semester.SemesterCode, x.Semester.SemesterName),
                $"{x.Semester.SemesterCode} ({x.Year}) - {x.Semester.SemesterName}",
                IsSemesterComplete(x.Semester)))
            .ToList();
    }

    public async Task<SemesterAverageChartDto> GetSemesterAverageChartAsync(string studentCode, string semesterKey, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var term = student.AcademicRecords
            .SelectMany(year => year.Semesters.Select(semester => new { Year = year.AcademicYearName, Semester = semester }))
            .FirstOrDefault(x => BuildSemesterKey(x.Year, x.Semester.SemesterCode, x.Semester.SemesterName) == semesterKey)
            ?? throw new NotFoundException("Không tìm thấy học kỳ đã chọn");

        var complete = IsSemesterComplete(term.Semester);
        var label = $"{term.Semester.SemesterCode} ({term.Year}) - {term.Semester.SemesterName}";
        if (!complete)
            return new SemesterAverageChartDto(semesterKey, label, false, null, []);

        var courses = term.Semester.Courses
            .Select(course => new SemesterCourseAverageDto(
                course.CourseCode,
                course.CourseName,
                course.Credits,
                CalculateFinalScore(course),
                course.ExcludeFromGpa))
            .OrderBy(x => x.CourseCode)
            .ToList();
        var gpaCourses = courses.Where(x => !x.ExcludeFromGpa).ToList();
        var denominator = gpaCourses.Sum(x => x.Credits);
        var average = denominator == 0
            ? 0
            : Math.Round(gpaCourses.Sum(x => x.FinalScore10 * x.Credits) / denominator, 2, MidpointRounding.AwayFromZero);
        return new SemesterAverageChartDto(semesterKey, label, true, average, courses);
    }

    public async Task<StudentCurriculumDto> GetCurriculumAsync(string studentCode, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var program = await db.Programs.Find(x => x.ProgramCode == student.Program.ProgramCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Không tìm thấy chương trình khung của sinh viên");
        var latestRecords = student.AcademicRecords
            .SelectMany(year => year.Semesters.SelectMany(semester => semester.Courses))
            .GroupBy(x => x.CourseCode)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.AttemptNumber).ThenByDescending(y => y.PublishedAt).First());

        var semesters = program.Courses
            .GroupBy(x => x.SuggestedSemester)
            .OrderBy(x => x.Key)
            .Select(group =>
            {
                var order = 0;
                var items = group.OrderBy(x => x.Group == "Required" ? 0 : 1).ThenBy(x => x.CourseCode).Select(item =>
                {
                    latestRecords.TryGetValue(item.CourseCode, out var record);
                    var finalScore = record is not null && record.ScoreStatus == "Published" ? CalculateFinalScore(record) : (double?)null;
                    var status = record is null ? "NotRegistered" : record.ScoreStatus == "Published" ? (finalScore >= 4 ? "Passed" : "Failed") : "InProgress";
                    return new CurriculumCourseDto(
                        ++order,
                        item.CourseCode,
                        item.CourseName,
                        item.Credits,
                        item.TheoryPeriods,
                        item.PracticePeriods,
                        item.Group,
                        item.ElectiveGroup,
                        item.RequiredCreditsInGroup,
                        item.ExcludeFromGpa,
                        item.IsCoreCourse,
                        item.IsDefaultSelection,
                        record is not null,
                        status,
                        finalScore);
                }).ToList();
                return new CurriculumSemesterDto(
                    group.Key,
                    group.Where(x => x.Group == "Required" && x.CountsTowardProgramCredits).Sum(x => x.Credits),
                    group.Where(x => x.Group == "Elective" && x.IsDefaultSelection && x.CountsTowardProgramCredits).Sum(x => x.Credits),
                    items);
            }).ToList();

        var completedCredits = latestRecords.Values
            .Where(x => x.ScoreStatus == "Published" && !x.ExcludeFromGpa && CalculateFinalScore(x) >= 4)
            .GroupBy(x => x.CourseCode)
            .Sum(x => x.First().Credits);
        return new StudentCurriculumDto(
            program.ProgramCode,
            program.ProgramName,
            program.Faculty.FacultyName,
            program.EducationLevel,
            program.ApplicableCohort,
            program.CurriculumVersion,
            program.RequiredCredits,
            program.RequiredCompulsoryCredits,
            program.RequiredElectiveCredits,
            completedCredits,
            Math.Round(completedCredits * 100.0 / Math.Max(1, program.RequiredCredits), 1),
            semesters);
    }

    public async Task<IReadOnlyCollection<ScheduleItemDto>> GetScheduleAsync(string studentCode, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var latestTerm = student.AcademicRecords
            .SelectMany(year => year.Semesters.Select(semester => new { Year = year.AcademicYearName, Semester = semester }))
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => ParseSemesterNumber(x.Semester.SemesterName))
            .FirstOrDefault(x => x.Semester.Courses.Any(course => course.ScoreStatus != "Published"))
            ?? student.AcademicRecords
                .SelectMany(year => year.Semesters.Select(semester => new { Year = year.AcademicYearName, Semester = semester }))
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => ParseSemesterNumber(x.Semester.SemesterName))
                .FirstOrDefault();
        var activeSectionIds = latestTerm?.Semester.Courses.Select(x => x.ClassSectionId).Distinct().ToList() ?? [];
        var sections = activeSectionIds.Count == 0
            ? new List<ClassSection>()
            : await db.ClassSections.Find(Builders<ClassSection>.Filter.In(x => x.Id, activeSectionIds) & Builders<ClassSection>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);
        var items = sections.SelectMany(section => section.Schedule.Select(slot => new ScheduleItemDto(
            "Class",
            section.CourseCode,
            section.CourseName,
            section.ClassSectionCode,
            null,
            slot.DayOfWeek,
            slot.StartTime,
            slot.EndTime,
            slot.Room,
            section.LecturerName,
            "Lịch học"))).ToList();
        var sectionIds = sections.Select(x => x.Id).ToList();
        var exams = await db.ExamSchedules.Find(Builders<ExamSchedule>.Filter.In(x => x.ClassSectionId, sectionIds) & Builders<ExamSchedule>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);
        items.AddRange(exams.Select(x => new ScheduleItemDto("Exam", x.CourseCode, x.CourseName, x.ClassSectionCode, x.StartAt, x.StartAt.DayOfWeek.ToString(), x.StartAt.ToString("HH:mm"), x.EndAt.ToString("HH:mm"), x.Room, "", x.Note)));
        return items.OrderBy(x => x.Type).ThenBy(x => x.CourseCode).ToList();
    }

    public async Task<IReadOnlyCollection<MaterialDto>> GetMaterialsAsync(string studentCode, string? classSectionId, CancellationToken ct)
    {
        var sectionIds = await GetStudentSectionIdsAsync(studentCode, ct);
        if (!string.IsNullOrWhiteSpace(classSectionId) && !sectionIds.Contains(classSectionId)) throw new NotFoundException("Không tìm thấy lớp học phần của sinh viên");
        var filter = Builders<LearningMaterial>.Filter.In(x => x.ClassSectionId, string.IsNullOrWhiteSpace(classSectionId) ? sectionIds : [classSectionId]) &
                     Builders<LearningMaterial>.Filter.Eq(x => x.IsDeleted, false) &
                     Builders<LearningMaterial>.Filter.Eq(x => x.Status, "Published") &
                     Builders<LearningMaterial>.Filter.Lte(x => x.VisibleFrom, DateTime.UtcNow) &
                     (Builders<LearningMaterial>.Filter.Eq(x => x.VisibleUntil, null) | Builders<LearningMaterial>.Filter.Gte(x => x.VisibleUntil, DateTime.UtcNow));
        var items = await db.Materials.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
        return items.Select(x => new MaterialDto(x.Id, x.ClassSectionId, x.ClassSectionCode, x.CourseCode, x.CourseName, x.Title, x.Description, x.Category, x.Chapter, x.ResourceType, x.ResourceUrl, x.VisibleFrom, x.VisibleUntil, x.ViewCount, x.DownloadCount, x.Status)).ToList();
    }

    public async Task<IReadOnlyCollection<AssignmentDto>> GetAssignmentsAsync(string studentCode, string? classSectionId, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var sectionIds = await GetStudentSectionIdsAsync(studentCode, ct);
        if (!string.IsNullOrWhiteSpace(classSectionId) && !sectionIds.Contains(classSectionId)) throw new NotFoundException("Không tìm thấy lớp học phần của sinh viên");
        var filter = Builders<Assignment>.Filter.In(x => x.ClassSectionId, string.IsNullOrWhiteSpace(classSectionId) ? sectionIds : [classSectionId]) & Builders<Assignment>.Filter.Eq(x => x.IsDeleted, false);
        var assignments = await db.Assignments.Find(filter).SortBy(x => x.DueAt).ToListAsync(ct);
        var assignmentIds = assignments.Select(x => x.Id).ToList();
        var submissions = await db.Submissions.Find(Builders<Submission>.Filter.In(x => x.AssignmentId, assignmentIds) & Builders<Submission>.Filter.Eq(x => x.StudentId, student.Id) & Builders<Submission>.Filter.Eq(x => x.IsDeleted, false)).ToListAsync(ct);
        var lookup = submissions.GroupBy(x => x.AssignmentId).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.SubmittedAt).First());
        return assignments.Select(x =>
        {
            lookup.TryGetValue(x.Id, out var submission);
            return new AssignmentDto(x.Id, x.ClassSectionId, x.ClassSectionCode, x.CourseCode, x.CourseName, x.Title, x.Content, x.AttachmentUrl, x.MaxScore, x.OpenAt, x.DueAt, x.AllowLate, x.LatePenaltyPercent, x.CloCodes, x.LinkedComponentId, x.Status, 0, submission?.Status, submission?.Score, submission?.Feedback);
        }).ToList();
    }

    public async Task<SubmissionDto> SubmitAsync(string studentCode, string assignmentId, StudentSubmissionRequest request, IReadOnlyCollection<Microsoft.AspNetCore.Http.IFormFile> files, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var assignment = await db.Assignments.Find(x => x.Id == assignmentId && !x.IsDeleted).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Không tìm thấy bài tập");
        var sectionIds = await GetStudentSectionIdsAsync(studentCode, ct);
        if (!sectionIds.Contains(assignment.ClassSectionId)) throw new NotFoundException("Bài tập không thuộc lớp của sinh viên");
        var now = DateTime.UtcNow;
        if (now < assignment.OpenAt) throw new AppException("Bài tập chưa mở");
        if (now > assignment.DueAt && !assignment.AllowLate) throw new AppException("Đã quá hạn nộp bài");
        var current = await db.Submissions.Find(x => x.AssignmentId == assignmentId && x.StudentId == student.Id && !x.IsDeleted).SortByDescending(x => x.SubmittedAt).FirstOrDefaultAsync(ct);
        if (current is not null && now > assignment.DueAt && !current.ResubmissionAllowed) throw new AppException("Không được phép thay thế bài nộp sau hạn");
        var savedFiles = new List<SubmissionFile>();
        foreach (var file in files)
        {
            if (file.Length > 20 * 1024 * 1024) throw new AppException($"File {file.FileName} vượt quá 20 MB");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension)) throw new AppException($"Định dạng {extension} không được hỗ trợ");
            var storedName = $"{Guid.NewGuid():N}{extension}";
            var root = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "submissions", assignment.Id, student.StudentCode);
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, storedName);
            await using var output = File.Create(path);
            await file.CopyToAsync(output, ct);
            savedFiles.Add(new SubmissionFile { OriginalName = Path.GetFileName(file.FileName), StoredName = storedName, Url = $"/uploads/submissions/{assignment.Id}/{student.StudentCode}/{storedName}", SizeBytes = file.Length, MimeType = file.ContentType });
        }
        var submission = current ?? new Submission { AssignmentId = assignment.Id, ClassSectionId = assignment.ClassSectionId, StudentId = student.Id, StudentCode = student.StudentCode, StudentName = student.FullName };
        submission.TextContent = request.TextContent;
        if (savedFiles.Count > 0) submission.Files = savedFiles;
        submission.SubmittedAt = now;
        submission.IsLate = now > assignment.DueAt;
        submission.Status = submission.IsLate ? "Late" : "Submitted";
        submission.Score = null;
        submission.Feedback = "";
        submission.GradedAt = null;
        submission.UpdatedAt = now;
        if (current is null) await db.Submissions.InsertOneAsync(submission, cancellationToken: ct);
        else await db.Submissions.ReplaceOneAsync(x => x.Id == submission.Id, submission, cancellationToken: ct);
        return MapSubmission(submission);
    }

    public async Task<byte[]> ExportTranscriptAsync(string studentCode, CancellationToken ct)
    {
        var student = await RequireStudentAsync(studentCode, ct);
        var terms = await GetTranscriptAsync(studentCode, ct);
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bang diem");
        sheet.Cell(1, 1).Value = "BẢNG ĐIỂM TOÀN KHÓA";
        sheet.Range(1, 1, 1, 8).Merge().Style.Font.SetBold().Font.SetFontSize(16);
        sheet.Cell(2, 1).Value = "Mã sinh viên";
        sheet.Cell(2, 2).Value = student.StudentCode;
        sheet.Cell(3, 1).Value = "Họ tên";
        sheet.Cell(3, 2).Value = student.FullName;
        var row = 5;
        foreach (var term in terms)
        {
            sheet.Cell(row, 1).Value = $"{term.AcademicYear} - {term.SemesterName} | GPA: {term.Gpa}";
            sheet.Range(row, 1, row, 8).Merge().Style.Font.SetBold();
            row++;
            var headers = new[] { "Mã môn", "Tên môn", "Tín chỉ", "Lớp HP", "Điểm 10", "Điểm chữ", "Điểm hệ 4", "Kết quả" };
            for (var i = 0; i < headers.Length; i++) sheet.Cell(row, i + 1).Value = headers[i];
            sheet.Row(row).Style.Font.SetBold();
            row++;
            foreach (var course in term.Courses)
            {
                sheet.Cell(row, 1).Value = course.CourseCode;
                sheet.Cell(row, 2).Value = course.CourseName;
                sheet.Cell(row, 3).Value = course.Credits;
                sheet.Cell(row, 4).Value = course.ClassSectionCode;
                sheet.Cell(row, 5).Value = course.FinalScore;
                sheet.Cell(row, 6).Value = course.LetterGrade;
                sheet.Cell(row, 7).Value = course.GradePoint;
                sheet.Cell(row, 8).Value = course.Passed ? "Đạt" : "Không đạt";
                row++;
            }
            row++;
        }
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }


    private static bool IsSemesterComplete(SemesterRecord semester) =>
        semester.Courses.Count > 0 && semester.Courses.All(course =>
            course.ScoreStatus == "Published" &&
            course.Scores.Count > 0 &&
            course.Scores.All(score => score.Status == "Graded" && score.Score.HasValue));

    private static double CalculateFinalScore(StudentCourseRecord course)
    {
        var value = course.Scores.Sum(score =>
            score.MaxScore <= 0 || !score.Score.HasValue
                ? 0
                : score.Score.Value / score.MaxScore * 10 * score.Weight / 100);
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string BuildSemesterKey(string academicYear, string semesterCode, string semesterName) =>
        $"{academicYear}|{semesterCode}|{semesterName}";

    private static int ParseSemesterNumber(string semesterName)
    {
        var digits = new string(semesterName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? value : 0;
    }

    private async Task<Student> RequireStudentAsync(string studentCode, CancellationToken ct) =>
        await db.Students.Find(x => x.StudentCode == studentCode && !x.IsDeleted).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Không tìm thấy sinh viên");

    private async Task<List<string>> GetStudentSectionIdsAsync(string studentCode, CancellationToken ct) =>
        (await db.ClassSections.Find(x => x.Students.Any(s => s.StudentCode == studentCode) && !x.IsDeleted).Project(x => x.Id).ToListAsync(ct)).Distinct().ToList();

    private static SubmissionDto MapSubmission(Submission x) => new(x.Id, x.AssignmentId, x.StudentId, x.StudentCode, x.StudentName, x.TextContent, x.Files, x.SubmittedAt, x.IsLate, x.Status, x.Score, x.Feedback, x.ResubmissionAllowed);
    private static BsonDocument Stage(string json) => BsonDocument.Parse(json);
}
