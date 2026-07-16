# Danh sách test case trọng yếu

| ID | Nhóm | Kịch bản | Kết quả mong đợi |
|---|---|---|---|
| AUTH-01 | Auth | Đăng nhập đúng | 200, JWT và refresh token |
| AUTH-02 | Auth | Sai mật khẩu 5 lần | Tài khoản bị khóa tạm thời |
| SEC-01 | Authorization | Sinh viên đọc điểm người khác | 403 hoặc 404 |
| SEC-02 | Authorization | Giảng viên mở lớp không phụ trách | 404 |
| GRD-01 | Điểm | Tổng trọng số khác 100% | Validation thất bại |
| GRD-02 | Điểm | Điểm vượt maxScore | 400 và lỗi đúng cột |
| GRD-03 | Điểm | Công bố bảng điểm khóa | 403 |
| GPA-01 | Aggregation | GPA học kỳ theo tín chỉ | Đúng công thức, `$group` tại MongoDB |
| GPA-02 | Aggregation | Môn học lại | Chỉ một kết quả được tính theo cấu hình |
| CLO-01 | Aggregation | CLO nhiều cột đóng góp | Tỷ lệ chuẩn hóa đúng |
| BAK-01 | Backup | `mongodump` thành công | Có file và history Success |
| BAK-02 | Restore | Sai chuỗi RESTORE | 400, không thay đổi dữ liệu |
| UI-01 | Frontend | Role Guard | Menu và route đúng vai trò |
| UI-02 | Frontend | Bảng điểm responsive | Cuộn ngang, không vỡ layout |
