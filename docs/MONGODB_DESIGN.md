# Thiết kế MongoDB

MongoDB phù hợp vì hồ sơ học tập là cây dữ liệu tự nhiên: sinh viên → năm học → học kỳ → môn học → cột điểm → ánh xạ CLO. Các cột điểm thay đổi theo môn nên document linh hoạt hơn một mô hình quan hệ cần nhiều bảng nối. Dữ liệu nhúng tối ưu thao tác đọc bảng điểm cá nhân; snapshot duy trì tính lịch sử; Aggregation Framework hỗ trợ `$unwind`, `$group`, `$switch`, `$bucket` và `$round` cho GPA, phân bố điểm và CLO.

Nhược điểm là document sinh viên có thể lớn, dữ liệu snapshot lặp và pipeline báo cáo có thể phức tạp. Giải pháp: chỉ nhúng snapshot tối giản; tách file sang object storage/GridFS; tách assignment, submission, audit log, notification ra collection riêng; phân trang; index theo truy vấn; theo dõi `$bsonSize`; archive hồ sơ cũ khi gần giới hạn 16 MB.

## Collection chính

- `users`: xác thực, vai trò, refresh token, khóa tài khoản.
- `students`: hồ sơ và `academicRecords` lồng nhau; không lưu GPA như nguồn dữ liệu cố định.
- `lecturers`: hồ sơ giảng viên.
- `courses`: CLO, phiên bản cấu trúc điểm, thang quy đổi.
- `classSections`: snapshot môn, giảng viên, lớp, cấu trúc điểm và trạng thái bảng điểm.
- `notifications`, `assignments`, `submissions`, `auditLogs`, `backupHistories`, `loginHistories`.

Script validator: `database/init/02-json-schema.js`. Script index: `database/init/01-indexes.js`. Pipeline demo: `database/04-aggregation-pipelines.js`.
