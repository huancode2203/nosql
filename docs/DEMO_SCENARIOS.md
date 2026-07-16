# Kịch bản demo

1. Admin đăng nhập, mở quản lý môn học, thêm/sửa/xóa mềm một môn.
2. Mở cấu trúc điểm, chứng minh tổng trọng số 100% và snapshot phiên bản.
3. Giảng viên `gv001@lms.edu.vn` mở bảng điểm, sửa ô điểm, lưu nháp rồi công bố.
4. Chạy `mongosh database/04-aggregation-pipelines.js` để chứng minh GPA/CLO tính tại MongoDB.
5. Sinh viên `sv001@lms.edu.vn` xem chi tiết điểm, công thức động, GPA và CLO.
6. Thử gọi API sinh viên bằng token khác hoặc lớp không thuộc giảng viên để nhận 403/404.
7. Admin tạo backup, thêm dữ liệu thử, restore và xem `auditLogs`.
8. Chạy `mongosh database/05-explain.js`, kiểm tra `winningPlan` có `IXSCAN`, `totalDocsExamined` và `executionTimeMillis`.
