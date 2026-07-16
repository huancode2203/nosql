# Phạm vi triển khai hiện tại

## Đã có mã nguồn chạy theo luồng chính

- Angular Standalone, lazy route, interceptor, guard và layout responsive 3 vai trò.
- Login JWT/BCrypt, refresh token rotation, logout, logout all, đổi mật khẩu, khóa tài khoản khi sai nhiều lần.
- Dashboard Admin/Giảng viên/Sinh viên lấy dữ liệu từ API.
- CRUD API và giao diện danh sách cho tài khoản, sinh viên, giảng viên, môn học, lớp học phần.
- Bảng nhập điểm động, validation theo `maxScore`, lưu nháp, công bố và audit log.
- Điểm môn, GPA tích lũy và CLO bằng MongoDB Aggregation Pipeline.
- Thông báo, đánh dấu đã đọc, SignalR hub.
- Backup/restore bằng `mongodump`/`mongorestore` và trang quản trị.
- Seed 80 sinh viên, 10 giảng viên, 15 môn, 12 lớp, 20 thông báo.
- Index, JSON Schema, explain, Postman, Docker Compose, unit/integration test và Excel mẫu.

## Khung mở rộng đã định tuyến nhưng chưa triển khai toàn bộ nghiệp vụ sâu

Các phân hệ tài liệu, bài tập/nộp bài, lịch thi, import preview nhiều bước, export PDF/CSV, chương trình đào tạo đầy đủ, quản lý khoa/ngành/năm học/học kỳ và báo cáo nâng cao đã có vị trí route/collection trong kiến trúc nhưng chưa được hoàn thiện toàn bộ CRUD và màn hình chi tiết trong bản này.

Để đạt toàn bộ danh sách hơn 100 chức năng trong prompt, tiếp tục triển khai theo cùng pattern Controller → Service → MongoDB và thay các trang `SimplePageComponent` bằng component nghiệp vụ tương ứng.
