# EduManage LMS

> Hệ thống quản lý và chấm điểm học tập trực tuyến sử dụng **Angular**, **ASP.NET Core Web API** và **MongoDB**.

EduManage LMS được xây dựng cho đề tài Cơ sở dữ liệu NoSQL, hỗ trợ ba vai trò **Quản trị viên**, **Giảng viên** và **Sinh viên**. Hệ thống quản lý chương trình đào tạo, lớp học phần, điểm thành phần, GPA, CLO, thông báo, tài liệu học tập, bài tập và các quy trình duyệt bảng điểm.

---

## 1. Công nghệ sử dụng

| Thành phần | Công nghệ |
|---|---|
| Frontend | Angular 20, TypeScript, SCSS |
| Backend | ASP.NET Core 8 Web API |
| Cơ sở dữ liệu | MongoDB 7 Replica Set |
| Xác thực | JWT, BCrypt |
| Realtime | SignalR |
| Xuất dữ liệu | ClosedXML, QuestPDF |
| Triển khai | Docker, Docker Compose |
| Kiểm thử | xUnit, Angular Test |

---

## 2. Chức năng chính

### Quản trị viên

- Quản lý tài khoản, sinh viên và giảng viên.
- Quản lý khoa, ngành, chương trình đào tạo và chương trình khung.
- Quản lý môn học, lớp học phần, năm học và học kỳ.
- Cấu hình cấu trúc điểm và tỷ trọng từng thành phần.
- Duyệt, trả lại và công bố bảng điểm.
- Xử lý yêu cầu mở lại bảng điểm.
- Quản lý thông báo và nhật ký hoạt động.
- Thống kê, báo cáo, sao lưu và khôi phục dữ liệu.

### Giảng viên

- Xem danh sách lớp học phần được phân công.
- Nhập điểm trực tiếp hoặc import từ Excel.
- Lưu nháp và gửi bảng điểm để quản trị viên duyệt.
- Chuẩn hóa dữ liệu điểm trước khi lưu.
- Quản lý tài liệu giảng dạy.
- Tạo bài tập, theo dõi bài nộp và chấm bài.
- Xem thống kê kết quả lớp học.

### Sinh viên

- Xem kết quả học tập theo từng học kỳ.
- Xem điểm thành phần, điểm tổng kết, hệ 4 và điểm chữ.
- Xem GPA học kỳ, GPA tích lũy và tiến độ học tập.
- Xem chương trình khung và số tín chỉ đã tích lũy.
- Xem kết quả CLO.
- Xem môn đang học, lịch học, tài liệu và bài tập.
- Nhận thông báo từ nhà trường và giảng viên.
- Xuất bảng điểm ra Excel.

---

## 3. Cấu trúc thư mục

```text
EduManageLMS_FullStack/
├── backend/
│   ├── EduManageLms.Api/          # ASP.NET Core Web API
│   └── EduManageLms.Tests/        # xUnit tests
├── frontend/                      # Angular application
├── database/
│   ├── init/                      # MongoDB initialization scripts
│   ├── 03-seed-demo.js            # Dữ liệu mẫu
│   ├── 04-aggregation-pipelines.js
│   └── 05-explain.js
├── docs/                          # Tài liệu kiến trúc, API, test và demo
├── postman/                       # Postman collection và environment
├── templates/                     # File mẫu import Excel
├── docker-compose.yml
├── .env.example
└── README.md
```

---

## 4. Yêu cầu hệ thống

### Cách khuyến nghị: chạy bằng Docker

Cài đặt:

- Git
- Docker Desktop
- Docker Compose v2

Kiểm tra:

```powershell
git --version
docker --version
docker compose version
```

Docker Desktop phải được mở và Docker Engine phải ở trạng thái **Running**.

### Chạy không dùng Docker

Cần cài thêm:

- Node.js 22 trở lên
- .NET SDK 8
- MongoDB 7
- MongoDB Database Tools

---

## 5. Clone project

```powershell
git clone https://github.com/huancode2203/nosql.git
cd nosql
```

Kiểm tra nhánh:

```powershell
git branch --show-current
git status
```

---

## 6. Tạo file cấu hình môi trường

### Windows PowerShell

```powershell
Copy-Item .env.example .env
```

### Linux hoặc macOS

```bash
cp .env.example .env
```

Mở file `.env` và thay các giá trị bí mật:

```env
MONGO_ROOT_USERNAME=lms_admin
MONGO_ROOT_PASSWORD=change_this_password
MONGO_DATABASE=EduManageLms
MONGO_REPLICA_KEY=replace_with_a_long_random_replica_set_key
JWT_KEY=change_this_to_a_random_secret_at_least_32_characters
JWT_ISSUER=EduManageLms
JWT_AUDIENCE=EduManageLms.Client
ASPNETCORE_ENVIRONMENT=Development
```

Không commit file `.env` có mật khẩu thật lên GitHub.

---

## 7. Chạy project bằng Docker

Tại thư mục chứa `docker-compose.yml`:

```powershell
docker compose up -d --build
```

Kiểm tra trạng thái:

```powershell
docker compose ps
```

Trạng thái mong đợi:

```text
edumanage-mongo       Running / Healthy
edumanage-api         Running / Healthy
edumanage-frontend    Running
```

Xem log toàn hệ thống:

```powershell
docker compose logs -f
```

Xem log từng service:

```powershell
docker compose logs api --tail 150
docker compose logs frontend --tail 150
docker compose logs mongo --tail 150
```

---

## 8. Địa chỉ truy cập

| Dịch vụ | Địa chỉ |
|---|---|
| Website | http://localhost:8081 |
| Swagger API | http://localhost:8080/swagger |
| API Health Check | http://localhost:8080/health |
| MongoDB | mongodb://localhost:27017 |

Sau khi build lại frontend, nên nhấn:

```text
Ctrl + F5
```

để trình duyệt tải lại toàn bộ file mới.

---

## 9. Tài khoản demo

| Vai trò | Email | Mật khẩu |
|---|---|---|
| Quản trị viên | `admin@lms.edu.vn` | `Lms@123456` |
| Giảng viên | `gv001@lms.edu.vn` | `Lms@123456` |
| Sinh viên | `sv001@lms.edu.vn` | `Lms@123456` |

Các tài khoản này chỉ dùng cho môi trường development và dữ liệu seed.

---

## 10. Dừng và khởi động lại project

Dừng container nhưng giữ dữ liệu:

```powershell
docker compose stop
```

Khởi động lại:

```powershell
docker compose start
```

Dừng và xóa container nhưng vẫn giữ volume:

```powershell
docker compose down
```

Chạy lại:

```powershell
docker compose up -d
```

Build lại toàn bộ:

```powershell
docker compose up -d --build
```

Build riêng frontend:

```powershell
docker compose build frontend
docker compose up -d --no-build frontend
```

Build riêng API:

```powershell
docker compose build api
docker compose up -d --no-build api
```

---

## 11. Cập nhật code mới từ GitHub

Khi chưa chỉnh sửa source local:

```powershell
git pull origin main
docker compose up -d --build
```

Khi đã có thay đổi local, kiểm tra trước:

```powershell
git status
git diff
```

Không dùng `git reset --hard` khi chưa sao lưu các thay đổi cần giữ.

---

## 12. Chạy frontend và backend không dùng Docker

### Backend

Đảm bảo MongoDB đang chạy tại `localhost:27017`.

```powershell
cd backend\EduManageLms.Api
dotnet restore
dotnet run
```

Swagger thường được mở tại địa chỉ ghi trong terminal.

### Frontend

Mở một terminal khác:

```powershell
cd frontend
npm install
npm start
```

Truy cập:

```text
http://localhost:4200
```

---

## 13. Build và kiểm thử

### Frontend

```powershell
cd frontend
npm ci
npm run lint:types
npm run build
```

### Backend

```powershell
cd backend\EduManageLms.Api
dotnet restore
dotnet build
```

### Test backend

```powershell
cd backend\EduManageLms.Tests
dotnet test
```

---

## 14. Import dữ liệu và file mẫu

Các file Excel mẫu nằm trong:

```text
templates/
```

Các script MongoDB nằm trong:

```text
database/
```

Chạy aggregation mẫu:

```powershell
mongosh `
  "mongodb://localhost:27017/EduManageLms" `
  database/04-aggregation-pipelines.js
```

Chạy explain:

```powershell
mongosh `
  "mongodb://localhost:27017/EduManageLms" `
  database/05-explain.js
```

---

## 15. Sao lưu và dữ liệu Docker

Docker Compose sử dụng các volume:

```text
mongo_data
backup_data
upload_data
```

Không chạy lệnh sau khi chưa sao lưu:

```powershell
docker compose down -v
```

Lệnh trên xóa volume và có thể làm mất dữ liệu MongoDB, file backup và file upload.

Khi cần khởi tạo lại dữ liệu mẫu từ đầu:

```powershell
docker compose down -v
docker compose up -d --build
```

Chỉ sử dụng quy trình này trong môi trường development.

---

## 16. Xử lý lỗi thường gặp

### Docker Desktop chưa chạy

```text
Cannot connect to the Docker daemon
```

Cách xử lý:

1. Mở Docker Desktop.
2. Đợi Docker Engine hiển thị Running.
3. Chạy lại:

```powershell
docker info
docker compose up -d --build
```

### MongoDB unhealthy

```powershell
docker compose logs mongo --tail 200
docker compose restart mongo
docker compose ps
```

### Không tải được image .NET

Ví dụ:

```text
lookup mcr.microsoft.com: no such host
```

Thử:

```powershell
ipconfig /flushdns
wsl --shutdown
```

Khởi động lại Docker Desktop, sau đó:

```powershell
docker pull mcr.microsoft.com/dotnet/sdk:8.0
docker pull mcr.microsoft.com/dotnet/aspnet:8.0
docker compose up -d --build
```

Có thể cần tắt VPN hoặc đổi DNS Windows sang:

```text
1.1.1.1
8.8.8.8
```

### Frontend chưa hiển thị thay đổi mới

```powershell
docker compose build frontend
docker compose up -d --no-build frontend
```

Sau đó nhấn `Ctrl + F5`.

### Cổng đang được sử dụng

Kiểm tra:

```powershell
netstat -ano | findstr :8080
netstat -ano | findstr :8081
netstat -ano | findstr :27017
```

---

## 17. Quy trình đóng góp code

Tạo nhánh mới:

```powershell
git checkout -b feature/ten-chuc-nang
```

Sau khi chỉnh sửa:

```powershell
git status
git add -A
git commit -m "Add ten chuc nang"
git push -u origin feature/ten-chuc-nang
```

Sau đó tạo Pull Request về nhánh `main`.

Không đưa vào GitHub:

- `.env`
- mật khẩu thật
- JWT secret thật
- thư mục backup
- log build
- `node_modules`
- `bin`
- `obj`
- volume hoặc dữ liệu MongoDB local

---

## 18. Tài liệu bổ sung

Xem thư mục `docs/`:

- Thiết kế kiến trúc.
- Thiết kế MongoDB.
- Tài liệu API.
- Kịch bản demo.
- Test case.
- Hướng dẫn backup và restore.

Postman collection nằm trong thư mục:

```text
postman/
```

---

## 19. Lưu ý bảo mật

- Thay toàn bộ mật khẩu mặc định trước khi triển khai thật.
- JWT key phải đủ dài và không được commit.
- Không dùng tài khoản seed trong production.
- Chỉ mở cổng MongoDB ra ngoài khi thật sự cần thiết.
- Thiết lập HTTPS, reverse proxy và chính sách backup khi triển khai production.

---

## Tác giả

**Phạm Đăng Huấn**

Repository:

```text
https://github.com/huancode2203/nosql
```

---

## Giấy phép

Project được xây dựng phục vụ mục đích học tập và nghiên cứu. Hãy bổ sung file `LICENSE` trước khi phân phối hoặc sử dụng trong môi trường thương mại.
