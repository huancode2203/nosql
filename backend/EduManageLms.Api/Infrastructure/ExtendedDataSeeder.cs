using EduManageLms.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Infrastructure;

public sealed class ExtendedDataSeeder(MongoContext db, ILogger<ExtendedDataSeeder> log)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var faculty = await db.Faculties.Find(x => !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (faculty is null)
        {
            var faculties = new List<Faculty>
            {
                new() { FacultyCode = "CNTT", FacultyName = "Công nghệ thông tin", Description = "Đào tạo và nghiên cứu lĩnh vực CNTT", DeanName = "PGS.TS Nguyễn Văn A" },
                new() { FacultyCode = "DTVT", FacultyName = "Điện tử - Viễn thông", Description = "Đào tạo điện tử, viễn thông và IoT", DeanName = "TS Trần Văn B" },
                new() { FacultyCode = "QTKD", FacultyName = "Quản trị kinh doanh", Description = "Đào tạo quản trị và kinh doanh số", DeanName = "TS Lê Thị C" }
            };
            await db.Faculties.InsertManyAsync(faculties, cancellationToken: ct);
            faculty = faculties[0];
        }

        var year = await db.AcademicYears.Find(x => x.IsCurrent && !x.IsDeleted).FirstOrDefaultAsync(ct);
        if (year is null)
        {
            var years = new List<AcademicYearEntity>
            {
                new() { AcademicYearCode = "2025-2026", AcademicYearName = "Năm học 2025-2026", StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc), IsCurrent = true },
                new() { AcademicYearCode = "2024-2025", AcademicYearName = "Năm học 2024-2025", StartDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc), IsCurrent = false }
            };
            await db.AcademicYears.InsertManyAsync(years, cancellationToken: ct);
            year = years[0];
        }

        if (await db.Semesters.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            var semesters = new List<SemesterEntity>
            {
                new() { SemesterCode = "HK1", SemesterName = "Học kỳ 1", AcademicYearId = year.Id, AcademicYearName = year.AcademicYearName, StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), GradeEntryStart = new DateTime(2025, 12, 20, 0, 0, 0, DateTimeKind.Utc), GradeEntryEnd = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc), PublishDate = new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc) },
                new() { SemesterCode = "HK2", SemesterName = "Học kỳ 2", AcademicYearId = year.Id, AcademicYearName = year.AcademicYearName, StartDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc), GradeEntryStart = new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), GradeEntryEnd = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc), PublishDate = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) }
            };
            await db.Semesters.InsertManyAsync(semesters, cancellationToken: ct);
        }

        if (await db.Programs.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            var courses = await db.Courses.Find(x => !x.IsDeleted).Limit(12).ToListAsync(ct);
            var programs = new List<TrainingProgram>
            {
                new()
                {
                    ProgramCode = "CNTT2024",
                    ProgramName = "Công nghệ thông tin",
                    Faculty = new FacultySnapshot { FacultyId = faculty.Id, FacultyCode = faculty.FacultyCode, FacultyName = faculty.FacultyName },
                    RequiredCredits = 130,
                    ApplicableCohort = "2024",
                    Courses = courses.Select((x, i) => new ProgramCourseItem { CourseId = x.Id, CourseCode = x.CourseCode, CourseName = x.CourseName, Credits = x.Credits, Group = i < 9 ? "Required" : "Elective", SuggestedSemester = i / 5 + 1, Prerequisites = x.PrerequisiteCourseCodes }).ToList(),
                    ProgramOutcomes = ["PLO1: Vận dụng kiến thức CNTT", "PLO2: Phân tích và thiết kế hệ thống", "PLO3: Làm việc nhóm và giao tiếp nghề nghiệp"]
                },
                new() { ProgramCode = "HTTT2024", ProgramName = "Hệ thống thông tin", Faculty = new FacultySnapshot { FacultyId = faculty.Id, FacultyCode = faculty.FacultyCode, FacultyName = faculty.FacultyName }, RequiredCredits = 128, ApplicableCohort = "2024" },
                new() { ProgramCode = "IOT2024", ProgramName = "Internet vạn vật", Faculty = new FacultySnapshot { FacultyId = faculty.Id, FacultyCode = faculty.FacultyCode, FacultyName = faculty.FacultyName }, RequiredCredits = 132, ApplicableCohort = "2024" },
                new() { ProgramCode = "ATTT2024", ProgramName = "An toàn thông tin", Faculty = new FacultySnapshot { FacultyId = faculty.Id, FacultyCode = faculty.FacultyCode, FacultyName = faculty.FacultyName }, RequiredCredits = 130, ApplicableCohort = "2024" }
            };
            await db.Programs.InsertManyAsync(programs, cancellationToken: ct);
        }

        var sections = await db.ClassSections.Find(x => !x.IsDeleted).ToListAsync(ct);
        if (sections.Count > 0 && await db.Materials.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            var materials = sections.Take(8).SelectMany(section => new[]
            {
                new LearningMaterial { ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName, LecturerCode = section.LecturerCode, Title = $"Đề cương {section.CourseName}", Description = "Đề cương chi tiết và kế hoạch học tập", Category = "Đề cương", Chapter = "Giới thiệu", ResourceType = "Link", ResourceUrl = "https://example.edu.vn/materials/syllabus", Status = "Published" },
                new LearningMaterial { ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName, LecturerCode = section.LecturerCode, Title = $"Bài giảng chương 1 - {section.CourseCode}", Description = "Tài liệu bài giảng chương đầu tiên", Category = "Bài giảng", Chapter = "Chương 1", ResourceType = "PDF", ResourceUrl = "https://example.edu.vn/materials/chapter-1.pdf", Status = "Published" }
            }).ToList();
            await db.Materials.InsertManyAsync(materials, cancellationToken: ct);
        }

        if (sections.Count > 0 && await db.Assignments.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            var assignments = sections.Take(8).SelectMany((section, index) => new[]
            {
                new Assignment { ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName, LecturerCode = section.LecturerCode, Title = $"Bài tập 1 - {section.CourseCode}", Content = "Hoàn thành bài tập theo yêu cầu trong tài liệu đính kèm.", MaxScore = 10, OpenAt = DateTime.UtcNow.AddDays(-14), DueAt = DateTime.UtcNow.AddDays(7 + index), AllowLate = true, LatePenaltyPercent = 10, CloCodes = ["CLO1", "CLO2"], LinkedComponentId = "BT", Status = "Open" },
                new Assignment { ClassSectionId = section.Id, ClassSectionCode = section.ClassSectionCode, CourseCode = section.CourseCode, CourseName = section.CourseName, LecturerCode = section.LecturerCode, Title = $"Đồ án nhỏ - {section.CourseCode}", Content = "Xây dựng sản phẩm nhỏ và nộp báo cáo.", MaxScore = 10, OpenAt = DateTime.UtcNow.AddDays(-7), DueAt = DateTime.UtcNow.AddDays(21 + index), AllowLate = false, CloCodes = ["CLO2", "CLO3"], LinkedComponentId = "BT", Status = "Open" }
            }).ToList();
            await db.Assignments.InsertManyAsync(assignments, cancellationToken: ct);

            var students = await db.Students.Find(x => !x.IsDeleted).Limit(30).ToListAsync(ct);
            var submissions = new List<Submission>();
            foreach (var assignment in assignments.Take(5))
            {
                foreach (var student in students.Where(s => sections.First(x => x.Id == assignment.ClassSectionId).Students.Any(e => e.StudentId == s.Id)).Take(8))
                {
                    submissions.Add(new Submission { AssignmentId = assignment.Id, ClassSectionId = assignment.ClassSectionId, StudentId = student.Id, StudentCode = student.StudentCode, StudentName = student.FullName, TextContent = "Bài nộp mẫu", SubmittedAt = DateTime.UtcNow.AddDays(-2), Status = "Submitted" });
                }
            }
            if (submissions.Count > 0) await db.Submissions.InsertManyAsync(submissions, cancellationToken: ct);
        }

        if (sections.Count > 0 && await db.ExamSchedules.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            await db.ExamSchedules.InsertManyAsync(sections.Take(8).Select((section, index) => new ExamSchedule
            {
                ClassSectionId = section.Id,
                ClassSectionCode = section.ClassSectionCode,
                CourseCode = section.CourseCode,
                CourseName = section.CourseName,
                ExamType = "Final",
                StartAt = DateTime.UtcNow.Date.AddDays(20 + index).AddHours(7),
                EndAt = DateTime.UtcNow.Date.AddDays(20 + index).AddHours(9),
                Room = $"B.{index + 1}01",
                Note = "Có mặt trước giờ thi 15 phút"
            }).ToList(), cancellationToken: ct);
        }

        if (await db.SystemSettings.CountDocumentsAsync(x => !x.IsDeleted, cancellationToken: ct) == 0)
        {
            await db.SystemSettings.InsertManyAsync(new[]
            {
                new SystemSetting { Key = "grade.repeatPolicy", Group = "Grading", Value = "Latest", Description = "Chính sách chọn kết quả môn học lại" },
                new SystemSetting { Key = "grade.enforceWindow", Group = "Grading", Value = false, Description = "Bắt buộc kiểm tra thời gian nhập điểm" },
                new SystemSetting { Key = "grade.defaultPassingScore", Group = "Grading", Value = 4.0, Description = "Ngưỡng đạt môn mặc định" },
                new SystemSetting { Key = "clo.defaultThreshold", Group = "CLO", Value = 50.0, Description = "Ngưỡng đạt CLO mặc định" },
                new SystemSetting { Key = "upload.maxFileSizeMb", Group = "Upload", Value = 20, Description = "Dung lượng file tối đa" },
                new SystemSetting { Key = "maintenance.enabled", Group = "System", Value = false, Description = "Chế độ bảo trì" }
            }, cancellationToken: ct);
        }

        log.LogInformation("Extended seed completed for faculties, programs, academic years, materials, assignments and schedules");
    }
}
