using MongoDB.Bson;

namespace EduManageLms.Api.Application;

internal static class AdminResourceCreatePolicy
{
    internal static void ApplyStructuralDefaults(
        string resource,
        Dictionary<string, object?> body)
    {
        switch (resource.ToLowerInvariant())
        {
            case "students":
                // Hồ sơ học tập chỉ được hình thành từ luồng đăng ký lớp/nhập điểm.
                body["academicRecords"] = new BsonArray();
                break;
            case "courses":
                // CLO và cấu trúc điểm được quản lý tại màn hình thiết kế môn học.
                body["clos"] = new BsonArray();
                body["gradingSchemes"] = new BsonArray();
                break;
            case "class-sections":
                // Không cho phép chèn danh sách sinh viên hoặc bỏ qua vòng đời bảng điểm
                // thông qua endpoint CRUD tổng quát.
                body["students"] = new BsonArray();
                body["gradeStatus"] = "Draft";
                break;
        }
    }

    internal static string[] RequiredFields(string resource) =>
        resource.ToLowerInvariant() switch
        {
            "faculties" => ["facultyCode", "facultyName"],
            "programs" => ["programCode", "programName"],
            "academic-years" =>
                ["academicYearCode", "academicYearName", "startDate", "endDate"],
            "semesters" =>
                [
                    "semesterCode", "semesterName", "academicYearId",
                    "startDate", "endDate"
                ],
            "courses" =>
                ["courseCode", "courseName", "credits", "clos", "gradingSchemes"],
            "class-sections" =>
                [
                    "classSectionCode", "courseId", "lecturerId", "semesterId",
                    "students", "gradingSchemeSnapshot", "gradeStatus"
                ],
            "notifications" => ["title", "content"],
            "system-settings" => ["key", "value"],
            "users" => ["username", "email", "fullName", "role"],
            "students" =>
                [
                    "studentCode", "fullName", "email", "faculty", "program",
                    "academicRecords"
                ],
            "lecturers" => ["lecturerCode", "fullName", "email"],
            _ => []
        };
}
