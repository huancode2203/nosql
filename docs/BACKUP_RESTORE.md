# Backup và Restore

API gọi `mongodump`/`mongorestore` bằng `ProcessStartInfo`, chuỗi kết nối lấy từ cấu hình hoặc biến môi trường, không ghi mật khẩu trực tiếp trong source. Restore yêu cầu chuỗi xác nhận `RESTORE`, tạo backup an toàn trước restore, dùng `--drop`, rồi ghi audit log.

Docker image API sao chép `mongodump` và `mongorestore` từ image MongoDB chính thức, vì vậy chức năng backup/restore hoạt động trong Docker Compose.

Lệnh thủ công:

```bash
mongodump --uri="mongodb://localhost:27017" --db=EduManageLms --out=./backups/manual
mongorestore --uri="mongodb://localhost:27017" --db=EduManageLms --drop ./backups/manual/EduManageLms
```
