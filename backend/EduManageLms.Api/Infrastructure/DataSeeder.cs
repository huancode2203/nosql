using EduManageLms.Api.Application;
using EduManageLms.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EduManageLms.Api.Infrastructure;

public sealed class DataSeeder(MongoContext db, ILogger<DataSeeder> log)
{
    private sealed record CurriculumSeed(
        int Semester,
        string Group,
        int ElectiveGroup,
        int RequiredCreditsInGroup,
        bool IsDefaultSelection,
        bool ExcludeFromGpa,
        string Code,
        string Name,
        int Credits,
        int TheoryPeriods,
        int PracticePeriods);

    private sealed record CohortSeed(string Cohort, int StudentCount, int CurrentSemester, IReadOnlyCollection<string> Classes);

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await db.Users.EstimatedDocumentCountAsync(cancellationToken: ct) > 0) return;

        var curriculum = Curriculum();
        ValidateCurriculum(curriculum);

        var facultyEntity = new Faculty
        {
            FacultyCode = "CNTT",
            FacultyName = "Khoa Công nghệ Thông tin",
            Description = "Đào tạo cử nhân và kỹ sư Công nghệ thông tin",
            DeanName = "PGS. TS. Nguyễn Minh Tuấn",
            Status = "Active"
        };
        await db.Faculties.InsertOneAsync(facultyEntity, cancellationToken: ct);
        var faculty = new FacultySnapshot
        {
            FacultyId = facultyEntity.Id,
            FacultyCode = facultyEntity.FacultyCode,
            FacultyName = facultyEntity.FacultyName
        };

        var gradeScale = DefaultGradeScale();
        var courses = curriculum.Select(seed =>
        {
            var scheme = BuildScheme(seed);
            return new Course
            {
                CourseCode = seed.Code,
                CourseName = seed.Name,
                Credits = seed.Credits,
                TheoryPeriods = seed.TheoryPeriods,
                PracticePeriods = seed.PracticePeriods,
                ExcludeFromGpa = seed.ExcludeFromGpa,
                IsCoreCourse = seed.Group == "Required" && !seed.ExcludeFromGpa,
                Faculty = faculty,
                Description = $"Học phần thuộc chương trình Công nghệ thông tin - học kỳ {seed.Semester}",
                Clos = BuildClos(seed),
                GradingSchemes = [CloneScheme(scheme)],
                GradeScale = gradeScale.Select(CloneScale).ToList()
            };
        }).ToList();
        await db.Courses.InsertManyAsync(courses, cancellationToken: ct);
        var courseLookup = courses.ToDictionary(x => x.CourseCode);

        var academicYears = BuildAcademicYears();
        await db.AcademicYears.InsertManyAsync(academicYears, cancellationToken: ct);
        var yearLookup = academicYears.ToDictionary(x => x.AcademicYearCode);

        var semesters = BuildSemesters(academicYears);
        await db.Semesters.InsertManyAsync(semesters, cancellationToken: ct);
        var semesterLookup = semesters.ToDictionary(x => (x.AcademicYearName.Replace("Năm học ", ""), x.SemesterCode));

        var programs = new List<TrainingProgram>();
        foreach (var cohort in new[] { "2023", "2024", "2025" })
        {
            programs.Add(new TrainingProgram
            {
                ProgramCode = $"CNTT{cohort}",
                ProgramName = "Công nghệ thông tin",
                Faculty = faculty,
                EducationLevel = "Đại học",
                RequiredCredits = 151,
                RequiredCompulsoryCredits = 128,
                RequiredElectiveCredits = 23,
                CurriculumVersion = "2023",
                ApplicableCohort = cohort,
                DurationYears = 4,
                Courses = curriculum.Select(seed => new ProgramCourseItem
                {
                    CourseId = courseLookup[seed.Code].Id,
                    CourseCode = seed.Code,
                    CourseName = seed.Name,
                    Credits = seed.Credits,
                    Group = seed.Group,
                    SuggestedSemester = seed.Semester,
                    TheoryPeriods = seed.TheoryPeriods,
                    PracticePeriods = seed.PracticePeriods,
                    ElectiveGroup = seed.ElectiveGroup,
                    RequiredCreditsInGroup = seed.RequiredCreditsInGroup,
                    ExcludeFromGpa = seed.ExcludeFromGpa,
                    CountsTowardProgramCredits = !seed.ExcludeFromGpa,
                    IsDefaultSelection = seed.IsDefaultSelection,
                    IsCoreCourse = seed.Group == "Required" && !seed.ExcludeFromGpa
                }).ToList(),
                ProgramOutcomes =
                [
                    "PLO1: Vận dụng kiến thức toán học, khoa học và Công nghệ thông tin",
                    "PLO2: Phân tích, thiết kế và phát triển hệ thống phần mềm",
                    "PLO3: Quản trị dữ liệu, hạ tầng mạng và bảo mật",
                    "PLO4: Giao tiếp, làm việc nhóm và tuân thủ đạo đức nghề nghiệp",
                    "PLO5: Nghiên cứu, đổi mới sáng tạo và học tập suốt đời"
                ]
            });
        }
        await db.Programs.InsertManyAsync(programs, cancellationToken: ct);
        var programLookup = programs.ToDictionary(x => x.ApplicableCohort);

        var lecturers = BuildLecturers(faculty);
        await db.Lecturers.InsertManyAsync(lecturers, cancellationToken: ct);

        var cohortSeeds = new[]
        {
            new CohortSeed("2023", 60, 7, new[] { "14DHTH13", "14DHTH14" }),
            new CohortSeed("2024", 40, 5, new[] { "15DHTH11", "15DHTH12" }),
            new CohortSeed("2025", 20, 3, new[] { "16DHTH01" })
        };

        var students = BuildStudents(cohortSeeds, faculty, programLookup);
        var selectedCurriculum = curriculum.Where(x => x.IsDefaultSelection).ToList();
        var classSections = BuildClassSections(
            cohortSeeds,
            selectedCurriculum,
            courseLookup,
            lecturers,
            students,
            yearLookup,
            semesterLookup);

        PopulateAcademicRecords(
            students,
            cohortSeeds,
            selectedCurriculum,
            courseLookup,
            classSections,
            semesterLookup);

        await db.Students.InsertManyAsync(students, cancellationToken: ct);
        await db.ClassSections.InsertManyAsync(classSections, cancellationToken: ct);

        var users = new List<User>
        {
            new()
            {
                Username = "admin",
                Email = "admin@lms.edu.vn",
                FullName = "Quản trị hệ thống",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lms@123456"),
                Role = "Admin"
            }
        };
        users.AddRange(lecturers.Select(x => new User
        {
            Username = x.LecturerCode.ToLowerInvariant(),
            Email = x.Email,
            FullName = x.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lms@123456"),
            Role = "Lecturer",
            LecturerCode = x.LecturerCode
        }));
        users.AddRange(students.Select(x => new User
        {
            Username = x.StudentCode,
            Email = x.Email,
            FullName = x.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lms@123456"),
            Role = "Student",
            StudentCode = x.StudentCode
        }));
        await db.Users.InsertManyAsync(users, cancellationToken: ct);

        var admin = users[0];
        var notifications = Enumerable.Range(1, 40).Select(i => new Notification
        {
            Title = (i % 4) switch
            {
                0 => "Thông báo lịch thi cuối kỳ",
                1 => "Cập nhật kết quả học tập",
                2 => "Nhắc thời hạn nộp bài",
                _ => "Thông báo đăng ký học phần"
            },
            Content = $"Thông báo học vụ số {i} dành cho sinh viên Khoa Công nghệ Thông tin.",
            Type = i % 4 == 0 ? "Exam" : i % 3 == 0 ? "Grade" : "Academic",
            Priority = i % 7 == 0 ? "High" : "Normal",
            AudienceType = i % 5 == 0 ? "All" : "Student",
            SenderId = admin.Id,
            DisplayFrom = DateTime.UtcNow.AddDays(-i)
        }).ToList();
        await db.Notifications.InsertManyAsync(notifications, cancellationToken: ct);

        await db.AuditLogs.InsertManyAsync(Enumerable.Range(1, 30).Select(i => new AuditLog
        {
            UserId = admin.Id,
            UserName = admin.FullName,
            Role = "Admin",
            Action = i % 3 == 0 ? "PublishGrades" : i % 2 == 0 ? "Create" : "Update",
            Entity = i % 3 == 0 ? "ClassSection" : i % 2 == 0 ? "Course" : "Student",
            IpAddress = "127.0.0.1",
            UserAgent = "CurriculumSeeder",
            Note = "Dữ liệu mẫu chương trình khung Công nghệ thông tin"
        }).ToList(), cancellationToken: ct);

        log.LogInformation(
            "Seeded IT curriculum: {Students} students, {Lecturers} lecturers, {Courses} courses, {Sections} class sections and 151 required credits",
            students.Count,
            lecturers.Count,
            courses.Count,
            classSections.Count);
    }

    private static List<CurriculumSeed> Curriculum() =>
    [
            new(1, "Required", 0, 0, true, true, "0101001657", "Giáo dục quốc phòng - an ninh 1", 3, 45, 0),
            new(1, "Required", 0, 0, true, false, "0101002298", "Kinh tế chính trị Mác - Lênin", 2, 30, 0),
            new(1, "Required", 0, 0, true, false, "0101007641", "Xác suất và thống kê trong sản xuất, công nghệ, kỹ thuật", 2, 30, 0),
            new(1, "Required", 0, 0, true, false, "0101100651", "Triết học Mác - Lênin", 3, 45, 0),
            new(1, "Required", 0, 0, true, false, "0101100984", "Đại số tuyến tính", 2, 30, 0),
            new(1, "Required", 0, 0, true, false, "0101101922", "Kỹ năng ứng dụng công nghệ thông tin", 3, 0, 90),
            new(1, "Required", 0, 0, true, false, "0101101923", "Nguyên lý ngôn ngữ lập trình", 2, 30, 0),
            new(1, "Required", 0, 0, true, false, "0101101924", "Thực hành Nguyên lý ngôn ngữ lập trình", 2, 0, 60),
            new(2, "Required", 0, 0, true, true, "0101001662", "Giáo dục quốc phòng - an ninh 2", 2, 30, 0),
            new(2, "Required", 0, 0, true, false, "0101003158", "Mạng máy tính", 3, 45, 0),
            new(2, "Required", 0, 0, true, false, "0101005322", "Thực hành mạng máy tính", 1, 0, 30),
            new(2, "Required", 0, 0, true, false, "0101006322", "Tư tưởng Hồ Chí Minh", 2, 30, 0),
            new(2, "Required", 0, 0, true, false, "0101100822", "Anh văn 1", 3, 45, 0),
            new(2, "Required", 0, 0, true, false, "0101101943", "Cấu trúc dữ liệu và Giải thuật", 2, 30, 0),
            new(2, "Required", 0, 0, true, false, "0101101961", "Thực hành cấu trúc dữ liệu và giải thuật", 1, 0, 30),
            new(2, "Elective", 1, 2, true, true, "0101001697", "Giáo dục thể chất 1 (Thể dục Thể hình)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101001703", "Giáo dục thể chất 1 (Võ thuật)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101001704", "Giáo dục thể chất 1 (Bóng đá)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101001705", "Giáo dục thể chất 1 (Bóng chuyền)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101001706", "Giáo dục thể chất 1 (Bơi lội)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101001707", "Giáo dục thể chất 1 (Cầu lông)", 2, 0, 60),
            new(2, "Elective", 2, 4, true, false, "0101003015", "Logic học", 2, 30, 0),
            new(2, "Elective", 2, 4, true, false, "0101003731", "Phương pháp nghiên cứu khoa học", 2, 30, 0),
            new(2, "Elective", 2, 4, false, false, "0101004030", "Quy hoạch thực nghiệm", 2, 30, 0),
            new(2, "Elective", 2, 4, false, false, "0101100933", "Giải tích", 3, 45, 0),
            new(2, "Elective", 2, 4, false, false, "0101100936", "Đổi mới sáng tạo và khởi nghiệp", 2, 30, 0),
            new(2, "Elective", 1, 2, false, true, "0101103085", "Giáo dục thể chất 1 (Bóng rổ)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101103086", "Giáo dục thể chất 1 (Pickleball)", 2, 0, 60),
            new(2, "Elective", 1, 2, false, true, "0101103087", "Giáo dục thể chất 1 (Cờ vua)", 2, 0, 60),
            new(3, "Required", 0, 0, true, false, "0101000476", "Chủ nghĩa xã hội khoa học", 2, 30, 0),
            new(3, "Required", 0, 0, true, true, "0101001669", "Giáo dục quốc phòng - an ninh 3", 1, 0, 30),
            new(3, "Required", 0, 0, true, true, "0101001677", "Giáo dục quốc phòng - an ninh 4", 2, 0, 60),
            new(3, "Required", 0, 0, true, false, "0101001742", "Hệ điều hành", 3, 45, 0),
            new(3, "Required", 0, 0, true, false, "0101002289", "Kiến trúc máy tính", 3, 45, 0),
            new(3, "Required", 0, 0, true, false, "0101100823", "Anh văn 2", 3, 45, 0),
            new(3, "Required", 0, 0, true, false, "0101100986", "Cấu trúc rời rạc", 3, 45, 0),
            new(3, "Elective", 2, 3, false, false, "0101001565", "Đồ họa ứng dụng", 3, 15, 60),
            new(3, "Elective", 1, 2, true, true, "0101001693", "Giáo dục thể chất 2 (Bóng chuyền)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101001694", "Giáo dục thể chất 2 (Bóng đá)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101001695", "Giáo dục thể chất 2 (Cầu lông)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101001696", "Giáo dục thể chất 2 (Thể dục Thể hình)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101001701", "Giáo dục thể chất 2 (Võ thuật)", 2, 0, 60),
            new(3, "Elective", 2, 3, true, false, "0101005177", "Thực hành kỹ thuật lập trình", 1, 0, 30),
            new(3, "Elective", 2, 3, true, false, "0101007064", "Kỹ thuật lập trình", 2, 30, 0),
            new(3, "Elective", 1, 2, false, true, "0101101334", "Giáo dục thể chất 2 (Bơi lội)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101103088", "Giáo dục thể chất 2 (Bóng rổ)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101103089", "Giáo dục thể chất 2 (Pickleball)", 2, 0, 60),
            new(3, "Elective", 1, 2, false, true, "0101103090", "Giáo dục thể chất 2 (Cờ vua)", 2, 0, 60),
            new(4, "Required", 0, 0, true, false, "0101001625", "Lịch sử Đảng Cộng sản Việt Nam", 2, 30, 0),
            new(4, "Required", 0, 0, true, false, "0101005281", "Thực hành lập trình hướng đối tượng", 1, 0, 30),
            new(4, "Required", 0, 0, true, false, "0101100824", "Anh văn 3", 3, 45, 0),
            new(4, "Required", 0, 0, true, false, "0101101954", "Bảo mật máy tính", 2, 30, 0),
            new(4, "Required", 0, 0, true, false, "0101101958", "Hệ cơ sở dữ liệu", 3, 45, 0),
            new(4, "Required", 0, 0, true, false, "0101101959", "Thực hành Hệ cơ sở dữ liệu", 1, 0, 30),
            new(4, "Required", 0, 0, true, false, "0101101962", "Lập trình hướng đối tượng", 2, 30, 0),
            new(4, "Elective", 1, 1, false, true, "0101001702", "Giáo dục thể chất 3 (Bóng đá)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101001718", "Giáo dục thể chất 3 (Bóng chuyền)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101001719", "Giáo dục thể chất 3 (Cầu lông)", 1, 0, 30),
            new(4, "Elective", 2, 3, true, false, "0101004725", "Thiết kế web", 3, 15, 60),
            new(4, "Elective", 1, 1, true, true, "0101100929", "Giáo dục thể chất 3 (Bơi lội)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101100930", "Giáo dục thể chất 3 (Thể dục Thể hình)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101100931", "Giáo dục thể chất 3 (Võ thuật)", 1, 0, 30),
            new(4, "Elective", 2, 3, false, false, "0101101955", "Lập trình Python", 3, 15, 60),
            new(4, "Elective", 2, 3, false, false, "0101101967", "Mã hóa và ứng dụng", 3, 15, 60),
            new(4, "Elective", 1, 1, false, true, "0101103091", "Giáo dục thể chất 3 (Bóng rổ)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101103092", "Giáo dục thể chất 3 (Pickleball)", 1, 0, 30),
            new(4, "Elective", 1, 1, false, true, "0101103093", "Giáo dục thể chất 3 (Cờ vua)", 1, 0, 30),
            new(5, "Required", 0, 0, true, false, "0101002921", "Lập trình web", 3, 15, 60),
            new(5, "Required", 0, 0, true, false, "0101006237", "Trí tuệ nhân tạo", 3, 45, 0),
            new(5, "Required", 0, 0, true, false, "0101101040", "Thực hành Trí tuệ nhân tạo", 1, 0, 30),
            new(5, "Required", 0, 0, true, false, "0101101963", "Công nghệ phần mềm", 3, 45, 0),
            new(5, "Required", 0, 0, true, false, "0101101968", "Hệ quản trị cơ sở dữ liệu", 3, 15, 60),
            new(5, "Elective", 2, 3, false, false, "0101007881", "Công nghệ .NET", 3, 15, 60),
            new(5, "Elective", 1, 3, true, false, "0101101964", "Phân tích thiết kế thuật toán", 3, 45, 0),
            new(5, "Elective", 1, 3, false, false, "0101101965", "Lập trình mạng", 3, 15, 60),
            new(5, "Elective", 1, 3, false, false, "0101101966", "Ảo hóa và điện toán đám mây", 3, 15, 60),
            new(5, "Elective", 2, 3, false, false, "0101101979", "Xử lý ảnh", 3, 15, 60),
            new(5, "Elective", 2, 3, true, false, "0101101983", "Bảo mật cơ sở dữ liệu", 3, 15, 60),
            new(6, "Required", 0, 0, true, false, "0101101956", "Deep learning", 3, 45, 0),
            new(6, "Required", 0, 0, true, false, "0101101957", "Thực hành deep learning", 1, 0, 30),
            new(6, "Required", 0, 0, true, false, "0101101969", "Lập trình di động", 3, 15, 60),
            new(6, "Required", 0, 0, true, false, "0101101970", "Khai phá dữ liệu", 3, 45, 0),
            new(6, "Required", 0, 0, true, false, "0101101973", "Quản trị hệ thống mạng", 3, 45, 0),
            new(6, "Required", 0, 0, true, false, "0101101974", "Thực hành quản trị hệ thống mạng", 1, 0, 30),
            new(6, "Required", 0, 0, true, false, "0101101976", "Phân tích thiết kế hệ thống", 2, 30, 0),
            new(6, "Required", 0, 0, true, false, "0101101977", "Thực hành phân tích thiết kế hệ thống", 1, 0, 30),
            new(6, "Elective", 1, 3, false, false, "0101000002", "Công nghệ Java", 3, 15, 60),
            new(6, "Elective", 1, 3, false, false, "0101000609", "Cơ sở dữ liệu nâng cao", 2, 30, 0),
            new(6, "Elective", 1, 3, false, false, "0101101980", "Công nghệ phần mềm nâng cao", 3, 15, 60),
            new(6, "Elective", 1, 3, false, false, "0101101982", "Thương mại điện tử", 3, 15, 60),
            new(6, "Elective", 1, 3, true, false, "0101101984", "Kiểm định phần mềm", 3, 15, 60),
            new(6, "Elective", 1, 3, false, false, "0101102539", "Kiểm thử phần mềm", 2, 15, 30),
            new(7, "Required", 0, 0, true, false, "0101101971", "Nhập môn Big Data", 2, 30, 0),
            new(7, "Required", 0, 0, true, false, "0101101972", "Thực hành nhập môn Big Data", 1, 0, 30),
            new(7, "Required", 0, 0, true, false, "0101101975", "Internet of Things", 3, 45, 0),
            new(7, "Required", 0, 0, true, false, "0101102007", "Thực tập nghề nghiệp", 2, 0, 60),
            new(7, "Required", 0, 0, true, false, "0101102008", "Khóa luận cử nhân", 4, 0, 0),
            new(7, "Elective", 1, 4, true, false, "0101101978", "Lập trình mã nguồn mở", 2, 0, 60),
            new(7, "Elective", 1, 4, true, false, "0101101981", "Dữ liệu NoSQL", 2, 0, 60),
            new(7, "Elective", 1, 4, false, false, "0101101985", "An toàn mạng máy tính", 2, 30, 0),
            new(7, "Elective", 1, 4, false, false, "0101101986", "Thực hành an toàn mạng máy tính", 2, 0, 60),
            new(8, "Required", 0, 0, true, false, "0101101015", "Thực tập kỹ sư", 8, 0, 0),
            new(8, "Required", 0, 0, true, false, "0101102009", "Công tác kỹ sư", 2, 15, 30),
            new(8, "Required", 0, 0, true, false, "0101102010", "Chuyên đề công nghệ mới và chuyển đổi số", 3, 45, 0),
            new(8, "Required", 0, 0, true, false, "0101102011", "Học máy nâng cao", 3, 45, 0),
            new(8, "Required", 0, 0, true, false, "0101102012", "Khóa luận kỹ sư", 14, 0, 0),
    ];

    private static void ValidateCurriculum(IReadOnlyCollection<CurriculumSeed> curriculum)
    {
        var selected = curriculum.Where(x => x.IsDefaultSelection && !x.ExcludeFromGpa).ToList();
        var compulsory = selected.Where(x => x.Group == "Required").Sum(x => x.Credits);
        var elective = selected.Where(x => x.Group == "Elective").Sum(x => x.Credits);
        if (compulsory != 128 || elective != 23 || compulsory + elective != 151)
            throw new InvalidOperationException($"Invalid curriculum totals: compulsory={compulsory}, elective={elective}");
        if (curriculum.Any(x => x.Code.Length == 0 || !x.Code.All(char.IsDigit)))
            throw new InvalidOperationException("Every course code must be a numeric string");
    }

    private static List<AcademicYearEntity> BuildAcademicYears()
    {
        var years = new List<AcademicYearEntity>();
        for (var start = 2023; start <= 2028; start++)
        {
            years.Add(new AcademicYearEntity
            {
                AcademicYearCode = $"{start}-{start + 1}",
                AcademicYearName = $"Năm học {start}-{start + 1}",
                StartDate = new DateTime(start, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(start + 1, 8, 31, 0, 0, 0, DateTimeKind.Utc),
                IsCurrent = start == 2025
            });
        }
        return years;
    }

    private static List<SemesterEntity> BuildSemesters(IReadOnlyCollection<AcademicYearEntity> years)
    {
        var result = new List<SemesterEntity>();
        foreach (var year in years)
        {
            var start = int.Parse(year.AcademicYearCode[..4]);
            result.Add(new SemesterEntity
            {
                SemesterCode = "HK1",
                SemesterName = "Học kỳ 1",
                AcademicYearId = year.Id,
                AcademicYearName = year.AcademicYearName,
                StartDate = new DateTime(start, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(start + 1, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                GradeEntryStart = new DateTime(start + 1, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                GradeEntryEnd = new DateTime(start + 1, 1, 25, 0, 0, 0, DateTimeKind.Utc),
                PublishDate = new DateTime(start + 1, 1, 30, 0, 0, 0, DateTimeKind.Utc)
            });
            result.Add(new SemesterEntity
            {
                SemesterCode = "HK2",
                SemesterName = "Học kỳ 2",
                AcademicYearId = year.Id,
                AcademicYearName = year.AcademicYearName,
                StartDate = new DateTime(start + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(start + 1, 6, 20, 0, 0, 0, DateTimeKind.Utc),
                GradeEntryStart = new DateTime(start + 1, 6, 5, 0, 0, 0, DateTimeKind.Utc),
                GradeEntryEnd = new DateTime(start + 1, 6, 25, 0, 0, 0, DateTimeKind.Utc),
                PublishDate = new DateTime(start + 1, 6, 30, 0, 0, 0, DateTimeKind.Utc)
            });
        }
        return result;
    }

    private static List<Lecturer> BuildLecturers(FacultySnapshot faculty)
    {
        var source = new[]
        {
            ("GV001", "TS. Nguyễn Minh Tuấn", "Khoa học máy tính", "Trí tuệ nhân tạo"),
            ("GV002", "ThS. Trần Thị Thu Hà", "Hệ thống thông tin", "Cơ sở dữ liệu"),
            ("GV003", "TS. Lê Hoàng Nam", "Kỹ thuật phần mềm", "Công nghệ phần mềm"),
            ("GV004", "ThS. Phạm Ngọc Anh", "Mạng máy tính", "Quản trị hệ thống mạng"),
            ("GV005", "TS. Võ Quốc Bảo", "An toàn thông tin", "Bảo mật"),
            ("GV006", "ThS. Nguyễn Thị Minh Châu", "Khoa học máy tính", "Cấu trúc dữ liệu"),
            ("GV007", "ThS. Đặng Văn Khánh", "Kỹ thuật phần mềm", "Lập trình web"),
            ("GV008", "TS. Bùi Thanh Tùng", "Dữ liệu và AI", "Khai phá dữ liệu"),
            ("GV009", "ThS. Hồ Thị Mỹ Linh", "Hệ thống thông tin", "Phân tích thiết kế hệ thống"),
            ("GV010", "TS. Trương Công Thành", "Điện toán", "Điện toán đám mây"),
            ("GV011", "ThS. Đỗ Quang Huy", "Internet vạn vật", "IoT"),
            ("GV012", "ThS. Mai Thị Ngọc Lan", "Kỹ thuật phần mềm", "Kiểm thử phần mềm")
        };
        return source.Select((x, index) => new Lecturer
        {
            LecturerCode = x.Item1,
            FullName = x.Item2,
            Email = $"{x.Item1.ToLowerInvariant()}@lms.edu.vn",
            Phone = $"0902{index + 1:000000}",
            Faculty = faculty,
            Department = x.Item3,
            Specializations = [x.Item4],
            Degree = x.Item2.StartsWith("TS.") ? "Tiến sĩ" : "Thạc sĩ",
            Title = index % 4 == 0 ? "Giảng viên chính" : "Giảng viên"
        }).ToList();
    }

    private static List<Student> BuildStudents(
        IReadOnlyCollection<CohortSeed> cohorts,
        FacultySnapshot faculty,
        IReadOnlyDictionary<string, TrainingProgram> programs)
    {
        var students = new List<Student>();
        var globalIndex = 0;
        foreach (var cohort in cohorts)
        {
            for (var i = 0; i < cohort.StudentCount; i++)
            {
                var special = cohort.Cohort == "2023" && i == 0;
                var studentCode = special ? "2001230282" : $"2001{cohort.Cohort[^2..]}{i + 1:0000}";
                var fullName = special ? "Phạm Đăng Huấn" : BuildStudentName(globalIndex + 1);
                var classCode = special
                    ? "14DHTH14"
                    : cohort.Classes.ElementAt(Math.Min(cohort.Classes.Count - 1, i * cohort.Classes.Count / cohort.StudentCount));
                var program = programs[cohort.Cohort];
                students.Add(new Student
                {
                    StudentCode = studentCode,
                    FullName = fullName,
                    Email = $"{studentCode}@lms.edu.vn",
                    Phone = $"09{(globalIndex % 8) + 1}{globalIndex + 1000000:0000000}",
                    Address = AddressFor(globalIndex),
                    Gender = globalIndex % 3 == 0 ? "Nữ" : "Nam",
                    DateOfBirth = new DateTime(2004 + (globalIndex % 3), globalIndex % 12 + 1, globalIndex % 27 + 1, 0, 0, 0, DateTimeKind.Utc),
                    Faculty = faculty,
                    Program = new ProgramSnapshot
                    {
                        ProgramId = program.Id,
                        ProgramCode = program.ProgramCode,
                        ProgramName = program.ProgramName,
                        RequiredCredits = 151
                    },
                    Cohort = cohort.Cohort,
                    AdministrativeClass = classCode,
                    Status = globalIndex % 47 == 0 && !special ? "Suspended" : "Studying"
                });
                globalIndex++;
            }
        }
        return students;
    }

    private static List<ClassSection> BuildClassSections(
        IReadOnlyCollection<CohortSeed> cohorts,
        IReadOnlyCollection<CurriculumSeed> selectedCurriculum,
        IReadOnlyDictionary<string, Course> courses,
        IReadOnlyCollection<Lecturer> lecturers,
        IReadOnlyCollection<Student> students,
        IReadOnlyDictionary<string, AcademicYearEntity> academicYears,
        IReadOnlyDictionary<(string Year, string Semester), SemesterEntity> semesters)
    {
        var result = new List<ClassSection>();
        var lecturerList = lecturers.ToList();
        var courseIndex = 0;
        foreach (var cohort in cohorts)
        {
            foreach (var classCode in cohort.Classes)
            {
                var classStudents = students.Where(x => x.Cohort == cohort.Cohort && x.AdministrativeClass == classCode).ToList();
                foreach (var item in selectedCurriculum.Where(x => x.Semester <= cohort.CurrentSemester))
                {
                    var yearCode = AcademicYearFor(int.Parse(cohort.Cohort), item.Semester);
                    var semesterCode = item.Semester % 2 == 1 ? "HK1" : "HK2";
                    var semester = semesters[(yearCode, semesterCode)];
                    var year = academicYears[yearCode];
                    var course = courses[item.Code];
                    var lecturer = lecturerList[courseIndex++ % lecturerList.Count];
                    var completed = item.Semester < cohort.CurrentSemester;
                    var scheme = CloneScheme(course.GradingSchemes[0]);
                    scheme.AcademicYear = yearCode;
                    result.Add(new ClassSection
                    {
                        ClassSectionCode = $"{item.Code}-{classCode}-{semesterCode}",
                        CourseId = course.Id,
                        CourseCode = course.CourseCode,
                        CourseName = course.CourseName,
                        AcademicYearId = year.Id,
                        AcademicYearName = yearCode,
                        SemesterId = semester.Id,
                        SemesterCode = semesterCode,
                        SemesterName = $"Học kỳ {item.Semester}",
                        LecturerId = lecturer.Id,
                        LecturerCode = lecturer.LecturerCode,
                        LecturerName = lecturer.FullName,
                        Capacity = Math.Max(40, classStudents.Count + 5),
                        Students = classStudents.Select(x => new StudentEnrollmentSnapshot
                        {
                            StudentId = x.Id,
                            StudentCode = x.StudentCode,
                            FullName = x.FullName,
                            Status = "Enrolled"
                        }).ToList(),
                        GradingSchemeSnapshot = scheme,
                        GradeStatus = completed ? "Published" : "InProgress",
                        StartDate = semester.StartDate,
                        EndDate = semester.EndDate,
                        Schedule =
                        [
                            new ScheduleSlot
                            {
                                DayOfWeek = DayFor(courseIndex),
                                StartTime = courseIndex % 3 == 0 ? "07:00" : courseIndex % 3 == 1 ? "09:45" : "13:00",
                                EndTime = courseIndex % 3 == 0 ? "09:30" : courseIndex % 3 == 1 ? "12:15" : "15:30",
                                Room = $"{(char)('A' + courseIndex % 4)}.{courseIndex % 8 + 1}0{courseIndex % 5 + 1}"
                            }
                        ]
                    });
                }
            }
        }
        return result;
    }

    private static void PopulateAcademicRecords(
        IReadOnlyCollection<Student> students,
        IReadOnlyCollection<CohortSeed> cohorts,
        IReadOnlyCollection<CurriculumSeed> selectedCurriculum,
        IReadOnlyDictionary<string, Course> courses,
        IReadOnlyCollection<ClassSection> classSections,
        IReadOnlyDictionary<(string Year, string Semester), SemesterEntity> semesters)
    {
        var cohortLookup = cohorts.ToDictionary(x => x.Cohort);
        var sectionLookup = classSections.ToDictionary(x => (x.ClassSectionCode.Split('-')[1], x.SemesterName, x.CourseCode));
        var random = new Random(20230723);
        var studentIndex = 0;
        foreach (var student in students)
        {
            var cohort = cohortLookup[student.Cohort];
            var recordsByYear = new Dictionary<string, AcademicRecord>();
            foreach (var semesterNumber in Enumerable.Range(1, cohort.CurrentSemester))
            {
                var yearCode = AcademicYearFor(int.Parse(student.Cohort), semesterNumber);
                var semesterCode = semesterNumber % 2 == 1 ? "HK1" : "HK2";
                var semesterEntity = semesters[(yearCode, semesterCode)];
                if (!recordsByYear.TryGetValue(yearCode, out var academicRecord))
                {
                    academicRecord = new AcademicRecord
                    {
                        AcademicYearId = semesterEntity.AcademicYearId,
                        AcademicYearName = yearCode
                    };
                    recordsByYear[yearCode] = academicRecord;
                }
                var semesterRecord = new SemesterRecord
                {
                    SemesterId = semesterEntity.Id,
                    SemesterCode = semesterCode,
                    SemesterName = $"Học kỳ {semesterNumber}"
                };
                var items = selectedCurriculum.Where(x => x.Semester == semesterNumber).ToList();
                for (var coursePosition = 0; coursePosition < items.Count; coursePosition++)
                {
                    var item = items[coursePosition];
                    var section = classSections.First(x =>
                        x.Students.Any(s => s.StudentId == student.Id) &&
                        x.SemesterName == $"Học kỳ {semesterNumber}" &&
                        x.CourseCode == item.Code);
                    var course = courses[item.Code];
                    var completed = semesterNumber < cohort.CurrentSemester;
                    var baseScore = 5.6 + ((studentIndex * 13 + coursePosition * 7 + semesterNumber * 3) % 37) / 10.0;
                    if (studentIndex % 23 == 0 && coursePosition == 1 && completed) baseScore = 3.4;
                    if (studentIndex % 31 == 0 && coursePosition == 2 && completed) baseScore = 4.2;
                    baseScore = Math.Clamp(baseScore + (random.NextDouble() - 0.5) * 0.4, 0, 10);
                    var scheme = course.GradingSchemes[0];
                    var scores = BuildScores(scheme, baseScore, completed, random);
                    semesterRecord.Courses.Add(new StudentCourseRecord
                    {
                        CourseId = course.Id,
                        CourseCode = course.CourseCode,
                        CourseName = course.CourseName,
                        Credits = course.Credits,
                        ExcludeFromGpa = course.ExcludeFromGpa,
                        ClassSectionId = section.Id,
                        ClassSectionCode = section.ClassSectionCode,
                        Lecturer = new LecturerSnapshot
                        {
                            LecturerId = section.LecturerId,
                            LecturerCode = section.LecturerCode,
                            FullName = section.LecturerName
                        },
                        GradingSchemeVersion = scheme.Version,
                        Scores = scores,
                        AttemptNumber = 1,
                        ScoreStatus = completed ? "Published" : "InProgress",
                        PublishedAt = completed ? section.EndDate.AddDays(10) : null
                    });
                }
                academicRecord.Semesters.Add(semesterRecord);
            }
            student.AcademicRecords = recordsByYear.Values.OrderBy(x => x.AcademicYearName).ToList();
            studentIndex++;
        }
    }

    private static List<ScoreComponent> BuildScores(
        GradingSchemeVersion scheme,
        double baseScore,
        bool completed,
        Random random)
    {
        var result = new List<ScoreComponent>();
        for (var index = 0; index < scheme.Components.Count; index++)
        {
            var definition = scheme.Components[index];
            var missing = !completed && index == scheme.Components.Count - 1;
            double? value = missing
                ? null
                : Math.Round(Math.Clamp(baseScore + (random.NextDouble() - 0.5) * 1.2, 0, definition.MaxScore), 1);
            result.Add(new ScoreComponent
            {
                ComponentId = definition.ComponentId,
                ComponentName = definition.Name,
                Type = definition.Type,
                Weight = definition.Weight,
                MaxScore = definition.MaxScore,
                Score = value,
                Status = missing ? "NotGraded" : "Graded",
                IsRequired = definition.IsRequired,
                MinimumScore = definition.MinimumScore,
                CloMappings = definition.CloMappings.Select(x => new CloMapping
                {
                    CloCode = x.CloCode,
                    MappingWeight = x.MappingWeight
                }).ToList()
            });
        }
        return result;
    }

    private static GradingSchemeVersion BuildScheme(CurriculumSeed seed)
    {
        List<GradingComponentDefinition> components;
        if (seed.TheoryPeriods == 0 && seed.PracticePeriods == 0)
        {
            components =
            [
                Component("HD", "Đánh giá của giảng viên hướng dẫn", "Supervisor", 30, "CLO1"),
                Component("BC", "Báo cáo", "Report", 30, "CLO2"),
                Component("BV", "Bảo vệ", "Defense", 40, "CLO3", true, 4)
            ];
        }
        else if (seed.TheoryPeriods == 0 && seed.PracticePeriods > 0)
        {
            components =
            [
                Component("TH", "Thực hành thường xuyên", "Practice", 40, "CLO1"),
                Component("DA", "Bài thực hành tổng hợp", "Project", 60, "CLO2", true, 4)
            ];
        }
        else if (seed.PracticePeriods > 0)
        {
            components =
            [
                Component("CC", "Chuyên cần", "Attendance", 10, "CLO1"),
                Component("BT", "Bài tập / thực hành", "Assignment", 25, "CLO1"),
                Component("GK", "Giữa kỳ", "Midterm", 25, "CLO2"),
                Component("CK", "Cuối kỳ", "Final", 40, "CLO3", true, 3)
            ];
        }
        else
        {
            components =
            [
                Component("CC", "Chuyên cần", "Attendance", 10, "CLO1"),
                Component("BT", "Bài tập", "Assignment", 20, "CLO1"),
                Component("GK", "Giữa kỳ", "Midterm", 30, "CLO2"),
                Component("CK", "Cuối kỳ", "Final", 40, "CLO3", true, 3)
            ];
        }
        return new GradingSchemeVersion
        {
            Version = 1,
            AcademicYear = "2023-2024",
            Components = components,
            PassingScore = 4,
            RoundingMode = "Normal",
            DecimalPlaces = 2,
            EffectiveFrom = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static GradingComponentDefinition Component(
        string id,
        string name,
        string type,
        double weight,
        string clo,
        bool finalCondition = false,
        double? minimumScore = null) => new()
    {
        ComponentId = id,
        Name = name,
        Type = type,
        Weight = weight,
        MaxScore = 10,
        IsRequired = finalCondition,
        IsFinalCondition = finalCondition,
        MinimumScore = minimumScore,
        CloMappings = [new CloMapping { CloCode = clo, MappingWeight = 100 }]
    };

    private static List<CloDefinition> BuildClos(CurriculumSeed seed) =>
    [
        new()
        {
            CloCode = "CLO1",
            Name = "Kiến thức",
            Description = $"Trình bày kiến thức nền tảng của học phần {seed.Name}",
            BloomLevel = "Understand",
            Threshold = 50,
            Weight = 30
        },
        new()
        {
            CloCode = "CLO2",
            Name = "Vận dụng",
            Description = $"Vận dụng kiến thức của học phần {seed.Name} để giải quyết bài toán",
            BloomLevel = "Apply",
            Threshold = 50,
            Weight = 40
        },
        new()
        {
            CloCode = "CLO3",
            Name = "Phân tích",
            Description = $"Phân tích và đánh giá giải pháp trong học phần {seed.Name}",
            BloomLevel = "Analyze",
            Threshold = 50,
            Weight = 30
        }
    ];

    private static List<GradeScaleItem> DefaultGradeScale() =>
    [
        new() { Min = 8.5, Max = 10, Letter = "A", GradePoint = 4, Classification = "Giỏi" },
        new() { Min = 8.0, Max = 8.49, Letter = "B+", GradePoint = 3.5, Classification = "Khá" },
        new() { Min = 7.0, Max = 7.99, Letter = "B", GradePoint = 3, Classification = "Khá" },
        new() { Min = 6.5, Max = 6.99, Letter = "C+", GradePoint = 2.5, Classification = "Trung bình khá" },
        new() { Min = 5.5, Max = 6.49, Letter = "C", GradePoint = 2, Classification = "Trung bình" },
        new() { Min = 5.0, Max = 5.49, Letter = "D+", GradePoint = 1.5, Classification = "Trung bình yếu" },
        new() { Min = 4.0, Max = 4.99, Letter = "D", GradePoint = 1, Classification = "Yếu" },
        new() { Min = 0, Max = 3.99, Letter = "F", GradePoint = 0, Classification = "Kém" }
    ];

    private static string AcademicYearFor(int cohortStart, int semesterNumber)
    {
        var start = cohortStart + (semesterNumber - 1) / 2;
        return $"{start}-{start + 1}";
    }

    private static string DayFor(int index) => (index % 5) switch
    {
        0 => "Monday",
        1 => "Tuesday",
        2 => "Wednesday",
        3 => "Thursday",
        _ => "Friday"
    };

    private static string AddressFor(int index)
    {
        var addresses = new[]
        {
            "Quảng Ngãi", "TP. Hồ Chí Minh", "Quảng Nam", "Bình Định", "Đà Nẵng",
            "Phú Yên", "Gia Lai", "Đồng Nai", "Bình Dương", "Bến Tre"
        };
        return addresses[index % addresses.Length];
    }

    private static string BuildStudentName(int index)
    {
        if (index == 1) return "Nguyễn Văn Bình";
        var surnames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Dương", "Huỳnh", "Mai" };
        var middles = new[] { "Văn", "Thị", "Minh", "Ngọc", "Hoàng", "Quốc", "Thanh", "Gia", "Đức", "Hữu" };
        var givens = new[] { "An", "Bình", "Châu", "Dũng", "Giang", "Hà", "Hải", "Hạnh", "Hiếu", "Huy", "Khánh", "Lan", "Linh", "Long", "Mai", "Nam", "Ngân", "Phúc", "Quân", "Trang" };
        var value = index - 1;
        var surname = surnames[value % surnames.Length];
        var middle = middles[(value / surnames.Length) % middles.Length];
        var given = givens[(value * 7) % givens.Length];
        return $"{surname} {middle} {given}";
    }

    private static GradingSchemeVersion CloneScheme(GradingSchemeVersion source) => new()
    {
        Version = source.Version,
        AcademicYear = source.AcademicYear,
        Components = source.Components.Select(x => new GradingComponentDefinition
        {
            ComponentId = x.ComponentId,
            Name = x.Name,
            Type = x.Type,
            Weight = x.Weight,
            MaxScore = x.MaxScore,
            IsRequired = x.IsRequired,
            MinimumScore = x.MinimumScore,
            IsFinalCondition = x.IsFinalCondition,
            CloMappings = x.CloMappings.Select(m => new CloMapping
            {
                CloCode = m.CloCode,
                MappingWeight = m.MappingWeight
            }).ToList()
        }).ToList(),
        PassingScore = source.PassingScore,
        RoundingMode = source.RoundingMode,
        DecimalPlaces = source.DecimalPlaces,
        EffectiveFrom = source.EffectiveFrom,
        Active = source.Active
    };

    private static GradeScaleItem CloneScale(GradeScaleItem source) => new()
    {
        Min = source.Min,
        Max = source.Max,
        Letter = source.Letter,
        GradePoint = source.GradePoint,
        Classification = source.Classification
    };
}
