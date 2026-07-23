# EduManage LMS – Hệ thống quản lý và chấm điểm học tập trực tuyến

Project full-stack theo đề tài Cơ sở dữ liệu NoSQL: Angular Standalone Components, ASP.NET Core Web API và MongoDB. Gói giao diện Stitch ban đầu được chuyển thành design system mới và các màn hình có kết nối API thật. GPA, điểm môn và CLO được tính bằng MongoDB Aggregation, không dùng vòng lặp C# thay thế pipeline.

## Chức năng đã triển khai trong mã nguồn

- Đăng nhập 3 vai trò, BCrypt, JWT, refresh-token model, khóa sau nhiều lần sai.
- Dashboard riêng Admin/Giảng viên/Sinh viên.
- CRUD dùng API cho tài khoản, sinh viên, giảng viên, môn và lớp học phần.
- Bảng nhập điểm động, validation max score, lưu nháp và công bố.
- Sinh viên xem điểm thành phần, công thức, hệ 10/chữ/4 và CLO.
- Kiểm tra ownership tại backend cho lớp giảng viên và dữ liệu sinh viên.
- SignalR hub, notification collection, audit/login history.
- Index, JSON Schema, seed 80 sinh viên, aggregation, explain.
- Backup/restore bằng `mongodump`/`mongorestore`.
- Docker Compose, Postman, unit/integration test và tài liệu demo.

## Cấu trúc

```text
EduManageLMS/
├─ frontend/                    Angular 20 standalone, SCSS responsive
├─ backend/EduManageLms.Api/    ASP.NET Core 8 API
├─ backend/EduManageLms.Tests/  xUnit + MongoDB Testcontainers
├─ database/                    index, schema, seed, aggregation, explain
├─ postman/                     collection và environment
├─ docs/                        kiến trúc, MongoDB, API, test, demo
└─ docker-compose.yml
```

## Chạy bằng Docker

```bash
cp .env.example .env
docker compose up --build
```

- Web: `http://localhost:8081`
- Swagger: `http://localhost:8080/swagger`
- MongoDB: `mongodb://localhost:27017`

## Chạy không dùng Docker

Yêu cầu: Node.js 22+, .NET SDK 8, MongoDB 7 và MongoDB Database Tools.

```bash
# MongoDB đang chạy tại localhost:27017
cd backend/EduManageLms.Api
dotnet restore
dotnet run

# terminal khác
cd frontend
npm install
npm start
```

Truy cập `http://localhost:4200`.

## Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | `admin@lms.edu.vn` | `Lms@123456` |
| Giảng viên | `gv001@lms.edu.vn` | `Lms@123456` |
| Sinh viên | `sv001@lms.edu.vn` | `Lms@123456` |

Mật khẩu chỉ dùng trong development seed.

## Kiểm tra

```bash
cd frontend
npm run build
npm run lint:types

cd ../backend/EduManageLms.Tests
dotnet test

mongosh "mongodb://localhost:27017/EduManageLms" database/04-aggregation-pipelines.js
mongosh "mongodb://localhost:27017/EduManageLms" database/05-explain.js
```

## Ghi chú nghiệm thu

Frontend đã được build production thành công trong môi trường tạo project. Môi trường hiện tại không có .NET SDK và Docker daemon nên backend chưa thể được biên dịch/chạy tại đây; Dockerfile, project file và mã nguồn đã được chuẩn bị để chạy trên máy có .NET 8 hoặc bằng Docker. Xem `docs/DEMO_SCENARIOS.md` để demo theo thứ tự.

## Ban va chuc nang theo vai tro

Xem [PATCH_ROLE_FEATURES.md](PATCH_ROLE_FEATURES.md) de biet cac workflow Admin, Lecturer va Student duoc bo sung.
