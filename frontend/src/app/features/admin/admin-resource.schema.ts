export type AdminFieldType =
  | 'text'
  | 'email'
  | 'password'
  | 'number'
  | 'date'
  | 'datetime-local'
  | 'checkbox'
  | 'select'
  | 'permissions';

export interface AdminFieldOption {
  value: string;
  label: string;
}

export interface AdminFieldDefinition {
  key: string;
  label: string;
  type?: AdminFieldType;
  required?: boolean;
  createOnly?: boolean;
  options?: AdminFieldOption[];
  source?: string;
  optionLabel?: string;
}

export const ADMIN_PERMISSIONS: AdminFieldOption[] = [
  { value: 'admin.resources.read', label: 'Xem danh mục và hồ sơ' },
  { value: 'admin.resources.write', label: 'Tạo và cập nhật danh mục' },
  { value: 'admin.resources.delete', label: 'Xóa mềm và khôi phục dữ liệu' },
  { value: 'admin.users.permissions', label: 'Gán quyền lẻ cho tài khoản' },
  { value: 'admin.users.avatars', label: 'Tải lên và xóa ảnh đại diện người dùng' },
  { value: 'admin.grades.review', label: 'Xem và trả lại bảng điểm' },
  { value: 'admin.grades.publish', label: 'Công bố bảng điểm' },
  { value: 'admin.grades.lock', label: 'Khóa bảng điểm' },
  { value: 'admin.grades.reopen', label: 'Duyệt yêu cầu mở điểm' },
  { value: 'admin.backups.read', label: 'Xem và tải bản sao lưu' },
  { value: 'admin.backups.manage', label: 'Tạo, tải lên, phục hồi và xóa backup' },
  { value: 'admin.reports.read', label: 'Xem báo cáo' },
  { value: 'admin.reports.export', label: 'Xuất báo cáo' },
  { value: 'admin.audit.read', label: 'Xem nhật ký hệ thống' },
  { value: 'admin.notifications.manage', label: 'Quản lý thông báo' },
  { value: 'admin.settings.manage', label: 'Quản lý cấu hình nghiệp vụ' },
  { value: 'admin.import_export', label: 'Import và export dữ liệu' }
];

const statusOptions: AdminFieldOption[] = [
  { value: 'Active', label: 'Hoạt động' },
  { value: 'Inactive', label: 'Ngừng hoạt động' }
];

export const ADMIN_RESOURCE_FIELDS: Record<string, AdminFieldDefinition[]> = {
  users: [
    { key: 'username', label: 'Tên đăng nhập', required: true },
    { key: 'fullName', label: 'Họ và tên', required: true },
    { key: 'email', label: 'Email', type: 'email', required: true },
    { key: 'password', label: 'Mật khẩu ban đầu', type: 'password', createOnly: true },
    {
      key: 'role',
      label: 'Vai trò',
      type: 'select',
      required: true,
      options: [
        { value: 'Admin', label: 'Quản trị viên' },
        { value: 'Lecturer', label: 'Giảng viên' },
        { value: 'Student', label: 'Sinh viên' }
      ]
    },
    { key: 'studentCode', label: 'Mã sinh viên' },
    { key: 'lecturerCode', label: 'Mã giảng viên' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions },
    { key: 'permissions', label: 'Quyền lẻ', type: 'permissions' }
  ],
  students: [
    { key: 'studentCode', label: 'Mã sinh viên', required: true },
    { key: 'fullName', label: 'Họ và tên', required: true },
    { key: 'email', label: 'Email', type: 'email', required: true },
    { key: 'phone', label: 'Số điện thoại' },
    { key: 'address', label: 'Địa chỉ' },
    {
      key: 'gender',
      label: 'Giới tính',
      type: 'select',
      options: [
        { value: 'Nam', label: 'Nam' },
        { value: 'Nữ', label: 'Nữ' },
        { value: 'Khác', label: 'Khác' }
      ]
    },
    { key: 'dateOfBirth', label: 'Ngày sinh', type: 'date' },
    { key: 'facultyId', label: 'Khoa', type: 'select', source: 'faculties', optionLabel: 'facultyName' },
    { key: 'programId', label: 'Chương trình', type: 'select', source: 'programs', optionLabel: 'programName' },
    { key: 'cohort', label: 'Khóa tuyển sinh' },
    { key: 'administrativeClass', label: 'Lớp hành chính' },
    {
      key: 'status',
      label: 'Trạng thái học tập',
      type: 'select',
      options: [
        { value: 'Studying', label: 'Đang học' },
        { value: 'Suspended', label: 'Bảo lưu/đình chỉ' },
        { value: 'Graduated', label: 'Đã tốt nghiệp' }
      ]
    }
  ],
  lecturers: [
    { key: 'lecturerCode', label: 'Mã giảng viên', required: true },
    { key: 'fullName', label: 'Họ và tên', required: true },
    { key: 'email', label: 'Email', type: 'email', required: true },
    { key: 'phone', label: 'Số điện thoại' },
    { key: 'degree', label: 'Học vị' },
    { key: 'title', label: 'Chức danh' },
    { key: 'department', label: 'Bộ môn' },
    { key: 'facultyId', label: 'Khoa', type: 'select', source: 'faculties', optionLabel: 'facultyName' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  faculties: [
    { key: 'facultyCode', label: 'Mã khoa', required: true },
    { key: 'facultyName', label: 'Tên khoa', required: true },
    { key: 'deanName', label: 'Trưởng khoa' },
    { key: 'phone', label: 'Số điện thoại' },
    { key: 'description', label: 'Mô tả' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  programs: [
    { key: 'programCode', label: 'Mã chương trình', required: true },
    { key: 'programName', label: 'Tên chương trình', required: true },
    { key: 'facultyId', label: 'Khoa', type: 'select', source: 'faculties', optionLabel: 'facultyName' },
    { key: 'applicableCohort', label: 'Khóa áp dụng' },
    { key: 'requiredCredits', label: 'Tổng tín chỉ', type: 'number' },
    { key: 'requiredCompulsoryCredits', label: 'Tín chỉ bắt buộc', type: 'number' },
    { key: 'requiredElectiveCredits', label: 'Tín chỉ tự chọn', type: 'number' },
    { key: 'durationYears', label: 'Số năm đào tạo', type: 'number' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  'academic-years': [
    { key: 'academicYearCode', label: 'Mã năm học', required: true },
    { key: 'academicYearName', label: 'Tên năm học', required: true },
    { key: 'startDate', label: 'Ngày bắt đầu', type: 'date', required: true },
    { key: 'endDate', label: 'Ngày kết thúc', type: 'date', required: true },
    { key: 'isCurrent', label: 'Năm học hiện tại', type: 'checkbox' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  semesters: [
    { key: 'semesterCode', label: 'Mã học kỳ', required: true },
    { key: 'semesterName', label: 'Tên học kỳ', required: true },
    { key: 'academicYearId', label: 'Năm học', type: 'select', source: 'academic-years', optionLabel: 'academicYearName', required: true },
    { key: 'startDate', label: 'Ngày bắt đầu', type: 'date', required: true },
    { key: 'endDate', label: 'Ngày kết thúc', type: 'date', required: true },
    { key: 'gradeEntryStart', label: 'Mở nhập điểm', type: 'datetime-local' },
    { key: 'gradeEntryEnd', label: 'Đóng nhập điểm', type: 'datetime-local' },
    { key: 'publishDate', label: 'Ngày dự kiến công bố', type: 'date' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  courses: [
    { key: 'courseCode', label: 'Mã môn học', required: true },
    { key: 'courseName', label: 'Tên môn học', required: true },
    { key: 'englishName', label: 'Tên tiếng Anh' },
    { key: 'credits', label: 'Số tín chỉ', type: 'number', required: true },
    { key: 'theoryPeriods', label: 'Số tiết lý thuyết', type: 'number' },
    { key: 'practicePeriods', label: 'Số tiết thực hành', type: 'number' },
    { key: 'facultyId', label: 'Khoa phụ trách', type: 'select', source: 'faculties', optionLabel: 'facultyName' },
    { key: 'excludeFromGpa', label: 'Không tính GPA', type: 'checkbox' },
    { key: 'isCoreCourse', label: 'Môn cốt lõi', type: 'checkbox' },
    { key: 'status', label: 'Trạng thái', type: 'select', options: statusOptions }
  ],
  'class-sections': [
    { key: 'classSectionCode', label: 'Mã lớp học phần', required: true },
    { key: 'courseId', label: 'Môn học', type: 'select', source: 'courses', optionLabel: 'courseName', required: true },
    { key: 'lecturerId', label: 'Giảng viên', type: 'select', source: 'lecturers', optionLabel: 'fullName', required: true },
    { key: 'semesterId', label: 'Học kỳ', type: 'select', source: 'semesters', optionLabel: 'semesterName', required: true },
    { key: 'capacity', label: 'Sĩ số tối đa', type: 'number' },
    { key: 'startDate', label: 'Ngày bắt đầu', type: 'date' },
    { key: 'endDate', label: 'Ngày kết thúc', type: 'date' },
    {
      key: 'gradeStatus',
      label: 'Trạng thái bảng điểm',
      type: 'select',
      options: [
        { value: 'Draft', label: 'Bản nháp' },
        { value: 'Submitted', label: 'Chờ duyệt' },
        { value: 'Published', label: 'Đã công bố' },
        { value: 'Locked', label: 'Đã khóa' },
        { value: 'Reopened', label: 'Đã mở lại' }
      ]
    }
  ],
  'system-settings': [
    {
      key: 'key',
      label: 'Khóa cấu hình',
      type: 'select',
      required: true,
      options: [
        { value: 'Grade.PassingScore', label: 'Điểm đạt môn (0–10)' },
        { value: 'Grade.DecimalPlaces', label: 'Số chữ số thập phân (0–4)' },
        { value: 'Clo.DefaultThreshold', label: 'Ngưỡng đạt CLO (0–100)' },
        { value: 'Security.MaxFailedLogins', label: 'Số lần đăng nhập sai tối đa (1–20)' },
        { value: 'Grade.AutoLock.Enabled', label: 'Tự động khóa bảng điểm (true/false)' }
      ]
    },
    { key: 'value', label: 'Giá trị', required: true },
    { key: 'group', label: 'Nhóm cấu hình' },
    { key: 'description', label: 'Mô tả' },
    { key: 'editable', label: 'Cho phép chỉnh sửa', type: 'checkbox' }
  ]
};

export const ADMIN_IMPORT_RESOURCES = new Set([
  'students',
  'lecturers',
  'faculties',
  'programs',
  'academic-years',
  'semesters',
  'courses',
  'class-sections'
]);
