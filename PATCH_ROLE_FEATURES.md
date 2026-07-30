# Ban va bo sung chuc nang Admin - Lecturer - Student

Ban va nay thay the cac trang placeholder bang cac workflow goi API that va bo sung cac collection MongoDB can thiet.

## Admin

- CRUD va soft delete/restore: tai khoan, sinh vien, giang vien, khoa, chuong trinh dao tao, nam hoc, hoc ky, mon hoc, lop hoc phan, thong bao, cau hinh he thong.
- Import Excel theo 2 buoc preview/commit cho sinh vien, giang vien, khoa,
  chuong trinh, nam hoc, hoc ky, mon hoc va lop hoc phan; ho tro tham chieu
  bang ma thay cho ObjectId.
- Export Excel cho cac danh muc quan tri.
- Quan ly CLO va tao phien ban cau truc diem moi; kiem tra tong trong so 100%.
- Cau truc diem doc duoc du lieu MongoDB cu/du thua truong, dung cung quyen
  voi route Admin va chi cap nhat CLO/cau truc diem thay vi ghi de mon hoc.
- Modal duyet bang diem luon giu vung nut thao tac trong man hinh; nut
  "Duyet va cong bo" hien ro trang thai va quyen dang thieu.
- Bao cao tong hop, phan bo sinh vien theo khoa, trang thai hoc tap, trang
  thai bang diem va CLO; xuat Excel va PDF tao truc tiep tai backend.
- Bo loc bao cao va pham vi thong bao dung endpoint rieng dung quyen cua
  tung man hinh, khong phu thuoc quyen doc tat ca danh muc.
- Anh dai dien chi Admin co quyen le `admin.users.avatars` moi duoc tai len
  hoac xoa; sinh vien va giang vien khong the tu thay doi.
- Xem audit log co tim kiem va phan trang.
- Duyet/tu choi yeu cau mo lai bang diem.
- Backup/restore MongoDB, upload ZIP, download ZIP va xoa ban sao luu; restore tu dong tao safety backup.

## Lecturer

- Xem danh sach lop duoc phan cong va danh sach sinh vien theo dung pham vi giang vien.
- Bang diem dong, luu nhap, cong bo, import Excel preview/commit va export Excel.
- Gui yeu cau mo lai bang diem sau khi cong bo/khoa.
- Thong ke lop bang MongoDB Aggregation: trung binh, cao/thap, trung vi, do lech chuan, dat/khong dat, phan bo diem, top va nhom nguy co.
- Thong ke CLO cua lop bang MongoDB Aggregation.
- CRUD tai lieu theo lop hoc phan.
- CRUD bai tap, anh xa CLO/cot diem, han nop, nop tre va muc tru diem.
- Xem bai nop, cham diem, nhan xet va cho phep nop lai.

## Student

- Xem cac mon dang hoc.
- Bang diem hoc ky va bang diem toan khoa; export Excel.
- GPA/CLO tiep tuc dung MongoDB Aggregation.
- Xem lich hoc va lich thi.
- Xem tai lieu theo cac lop da dang ky.
- Xem bai tap, nop noi dung va nhieu file; gioi han 20 MB/file, doi ten file va luu trong Docker volume.
- Xem diem, nhan xet va trang thai bai nop.
- Cap nhat ho so ca nhan.

## Collection moi

`faculties`, `programs`, `academicYears`, `semesters`, `materials`, `assignments`, `submissions`, `examSchedules`, `systemSettings`, `gradeReopenRequests`.

## Kiem tra da thuc hien

- `npm run lint:types`: thanh cong.
- `npm run build`: thanh cong.
- Kiem tra route/quyen API phu thuoc va cac modal Admin dai: thanh cong.
- Them xUnit hoi quy cho MongoDB document cu co truong du thua.
- Docker build API bat buoc chay xUnit truoc khi publish image.
- MongoDB index script va API index initializer dung cung ten index camelCase.
- Backend can duoc xac nhan bang `docker compose build api` tren may co Docker/.NET SDK image.

## Gioi han con lai

- File hoc tap/bai nop dang luu tren Docker volume cuc bo, chua tich hop S3/MinIO.
- Export Excel va PDF da hoat dong. PDF chua co chu ky so.
- Email quen mat khau van o che do demo, khong phai mail server production.
- Dang ky hoc phan tu dong voi kiem tra tien quyet/trung lich chua duoc mo thanh cong thong tin rieng cho sinh vien.
