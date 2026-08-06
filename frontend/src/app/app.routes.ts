import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { permissionGuard } from './core/guards/permission.guard';

const adminResource = (
  path: string,
  resource: string,
  title: string,
  columns: string[],
  subtitle = 'Quản lý dữ liệu hệ thống',
  permission = 'admin.resources.read'
) => ({
  path,
  canActivate: [permissionGuard(permission)],
  loadComponent: () =>
    import('./features/admin/resource-list.component')
      .then(module => module.ResourceListComponent),
  data: {
    resource,
    title,
    subtitle,
    columns
  }
});

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component')
        .then(module => module.LoginComponent)
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/common/simple-page.component')
        .then(module => module.SimplePageComponent),
    data: {
      title: 'Quên mật khẩu'
    }
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layouts/app-layout.component')
        .then(module => module.AppLayoutComponent),
    children: [
      {
        path: 'admin',
        canActivate: [roleGuard(['Admin'])],
        children: [
          {
            path: 'dashboard',
            loadComponent: () =>
              import('./features/dashboards/dashboard.component')
                .then(module => module.DashboardComponent)
          },

          adminResource(
            'users',
            'users',
            'Quản lý tài khoản',
            ['username', 'fullName', 'email', 'role', 'status'],
            'Tạo tài khoản, cập nhật vai trò và trạng thái truy cập.'
          ),
          adminResource(
            'students',
            'students',
            'Quản lý sinh viên',
            [
              'studentCode',
              'fullName',
              'email',
              'administrativeClass',
              'status'
            ],
            'Quản lý hồ sơ, lớp hành chính và trạng thái học tập.'
          ),
          adminResource(
            'lecturers',
            'lecturers',
            'Quản lý giảng viên',
            [
              'lecturerCode',
              'fullName',
              'email',
              'degree',
              'status'
            ],
            'Quản lý hồ sơ và trạng thái công tác của giảng viên.'
          ),
          adminResource(
            'faculties',
            'faculties',
            'Quản lý khoa',
            [
              'facultyCode',
              'facultyName',
              'deanName',
              'phone',
              'status'
            ],
            'Quản lý danh mục khoa và đơn vị đào tạo.'
          ),
          adminResource(
            'programs',
            'programs',
            'Chương trình đào tạo',
            [
              'programCode',
              'programName',
              'applicableCohort',
              'requiredCredits',
              'status'
            ],
            'Quản lý chương trình, khóa áp dụng và tổng số tín chỉ.'
          ),
          adminResource(
            'academic-years',
            'academic-years',
            'Quản lý năm học',
            [
              'academicYearCode',
              'academicYearName',
              'startDate',
              'endDate',
              'isCurrent',
              'status'
            ],
            'Thiết lập năm học hiện tại và thời gian áp dụng.'
          ),
          adminResource(
            'semesters',
            'semesters',
            'Quản lý học kỳ',
            [
              'semesterCode',
              'semesterName',
              'academicYearName',
              'startDate',
              'endDate',
              'status'
            ],
            'Quản lý học kỳ, thời gian học và trạng thái nhập điểm.'
          ),
          adminResource(
            'courses',
            'courses',
            'Quản lý môn học',
            [
              'courseCode',
              'courseName',
              'credits',
              'facultyName',
              'status'
            ],
            'Quản lý môn học, tín chỉ và đơn vị phụ trách.'
          ),
          adminResource(
            'class-sections',
            'class-sections',
            'Quản lý lớp học phần',
            [
              'classSectionCode',
              'courseName',
              'lecturerName',
              'semesterName',
              'gradeStatus'
            ],
            'Quản lý lớp học phần, giảng viên và trạng thái bảng điểm.'
          ),

          {
            path: 'gradebooks',
            canActivate: [permissionGuard('admin.grades.review')],
            loadComponent: () =>
              import('./features/admin/gradebook-review.component')
                .then(module => module.GradebookReviewComponent)
          },
          {
            path: 'grading-schemes',
            canActivate: [permissionGuard('admin.settings.manage')],
            loadComponent: () =>
              import('./features/admin/grading-scheme.component')
                .then(module => module.GradingSchemeComponent)
          },
          {
            path: 'notifications',
            canActivate: [permissionGuard('admin.notifications.manage')],
            loadComponent: () =>
              import('./features/admin/admin-notifications.component')
                .then(module => module.AdminNotificationsComponent)
          },
          {
            path: 'reports',
            canActivate: [permissionGuard('admin.reports.read')],
            loadComponent: () =>
              import('./features/admin/reports.component')
                .then(module => module.ReportsComponent)
          },
          {
            path: 'grade-reopen-requests',
            canActivate: [permissionGuard('admin.grades.reopen')],
            loadComponent: () =>
              import('./features/admin/reopen-requests.component')
                .then(module => module.ReopenRequestsComponent)
          },
          {
            path: 'backups',
            canActivate: [permissionGuard('admin.backups.read')],
            loadComponent: () =>
              import('./features/admin/backups.component')
                .then(module => module.BackupsComponent)
          },
          {
            path: 'audit-logs',
            canActivate: [permissionGuard('admin.audit.read')],
            loadComponent: () =>
              import('./features/admin/audit-logs.component')
                .then(module => module.AuditLogsComponent)
          },
          adminResource(
            'settings',
            'system-settings',
            'Cấu hình hệ thống',
            ['key', 'value', 'group', 'description'],
            'Quản lý tham số vận hành và cấu hình nghiệp vụ.',
            'admin.settings.manage'
          ),

          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'dashboard'
          },
          {
            path: '**',
            redirectTo: 'dashboard'
          }
        ]
      },
      {
        path: 'lecturer',
        canActivate: [roleGuard(['Lecturer'])],
        children: [
          {
            path: 'dashboard',
            loadComponent: () =>
              import('./features/dashboards/dashboard.component')
                .then(module => module.DashboardComponent)
          },
          {
            path: 'classes',
            loadComponent: () =>
              import('./features/lecturer/classes.component')
                .then(module => module.LecturerClassesComponent)
          },
          {
            path: 'grades/:id',
            loadComponent: () =>
              import('./features/lecturer/gradebook.component')
                .then(module => module.GradebookComponent)
          },
          {
            path: 'grades',
            loadComponent: () =>
              import('./features/lecturer/gradebook.component')
                .then(module => module.GradebookComponent)
          },
          {
            path: 'materials',
            loadComponent: () =>
              import('./features/lecturer/materials.component')
                .then(module => module.LecturerMaterialsComponent)
          },
          {
            path: 'assignments',
            loadComponent: () =>
              import('./features/lecturer/assignments.component')
                .then(module => module.LecturerAssignmentsComponent)
          },
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'dashboard'
          },
          {
            path: '**',
            redirectTo: 'dashboard'
          }
        ]
      },      {
        path: 'student',
        canActivate: [roleGuard(['Student'])],
        children: [
          {
            path: 'dashboard',
            loadComponent: () =>
              import('./features/dashboards/dashboard.component')
                .then(module => module.DashboardComponent)
          },
          {
            path: 'grades',
            loadComponent: () =>
              import('./features/student/grades.component')
                .then(module => module.GradesComponent)
          },
          {
            path: 'clo-results',
            loadComponent: () =>
              import('./features/student/clo.component')
                .then(module => module.CloComponent)
          },
          {
            path: 'gpa',
            loadComponent: () =>
              import('./features/student/gpa.component')
                .then(module => module.GpaComponent)
          },
          {
            path: 'curriculum',
            loadComponent: () =>
              import('./features/student/curriculum.component')
                .then(module => module.CurriculumComponent)
          },
          {
            path: 'current-courses',
            loadComponent: () =>
              import('./features/student/current-courses.component')
                .then(module => module.StudentCurrentCoursesComponent)
          },
          {
            path: 'schedule',
            loadComponent: () =>
              import('./features/student/schedule.component')
                .then(module => module.StudentScheduleComponent)
          },
          {
            path: 'materials',
            loadComponent: () =>
              import('./features/student/materials.component')
                .then(module => module.StudentMaterialsComponent)
          },
          {
            path: 'assignments',
            loadComponent: () =>
              import('./features/student/assignments.component')
                .then(module => module.StudentAssignmentsComponent)
          },
          {
            path: '',
            pathMatch: 'full',
            redirectTo: 'dashboard'
          },
          {
            path: '**',
            redirectTo: 'dashboard'
          }
        ]
      },      {
        path: 'profile',
        loadComponent: () =>
          import('./features/common/profile.component')
            .then(module => module.ProfileComponent)
      },
      {
        path: 'notifications',
        loadComponent: () =>
          import('./features/common/notifications.component')
            .then(module => module.NotificationsComponent)
      }
    ]
  },
  {
    path: 'unauthorized',
    loadComponent: () =>
      import('./features/common/error-page.component')
        .then(module => module.ErrorPageComponent),
    data: {
      code: '403',
      title: 'Không có quyền truy cập'
    }
  },
  {
    path: '**',
    loadComponent: () =>
      import('./features/common/error-page.component')
        .then(module => module.ErrorPageComponent)
  }
];
