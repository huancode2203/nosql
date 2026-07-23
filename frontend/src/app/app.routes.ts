import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent) },
  { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password.component').then(m => m.ForgotPasswordComponent) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layouts/app-layout.component').then(m => m.AppLayoutComponent),
    children: [
      {
        path: 'admin', canActivate: [roleGuard(['Admin'])], children: [
          { path: 'dashboard', loadComponent: () => import('./features/dashboards/dashboard.component').then(m => m.DashboardComponent) },
          { path: 'users', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'users', title: 'Quản lý tài khoản', columns: ['username', 'fullName', 'email', 'role', 'status'] } },
          { path: 'students', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'students', title: 'Quản lý sinh viên', columns: ['studentCode', 'fullName', 'email', 'administrativeClass', 'status'] } },
          { path: 'lecturers', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'lecturers', title: 'Quản lý giảng viên', columns: ['lecturerCode', 'fullName', 'email', 'degree', 'status'] } },
          { path: 'faculties', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'faculties', title: 'Quản lý khoa', columns: ['facultyCode', 'facultyName', 'deanName', 'status'] } },
          { path: 'programs', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'programs', title: 'Chương trình đào tạo', columns: ['programCode', 'programName', 'educationLevel', 'requiredCredits', 'applicableCohort', 'status'] } },
          { path: 'academic-years', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'academic-years', title: 'Năm học', columns: ['academicYearCode', 'academicYearName', 'startDate', 'endDate', 'isCurrent', 'status'] } },
          { path: 'semesters', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'semesters', title: 'Học kỳ', columns: ['semesterCode', 'semesterName', 'academicYearId', 'academicYearName', 'startDate', 'endDate', 'status'] } },
          { path: 'courses', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'courses', title: 'Quản lý môn học', columns: ['courseCode', 'courseName', 'credits', 'theoryPeriods', 'practicePeriods', 'status'] } },
          { path: 'class-sections', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'class-sections', title: 'Quản lý lớp học phần', columns: ['classSectionCode', 'courseName', 'lecturerName', 'semesterName', 'capacity', 'gradeStatus'] } },
          { path: 'clo', loadComponent: () => import('./features/admin/grading-scheme.component').then(m => m.GradingSchemeComponent) },
          { path: 'grading-schemes', loadComponent: () => import('./features/admin/grading-scheme.component').then(m => m.GradingSchemeComponent) },
          { path: 'notifications', loadComponent: () => import('./features/admin/admin-notifications.component').then(m => m.AdminNotificationsComponent) },
          { path: 'reports', loadComponent: () => import('./features/admin/reports.component').then(m => m.ReportsComponent) },
          { path: 'grade-reopen-requests', loadComponent: () => import('./features/admin/reopen-requests.component').then(m => m.ReopenRequestsComponent) },
          { path: 'audit-logs', loadComponent: () => import('./features/admin/audit-logs.component').then(m => m.AuditLogsComponent) },
          { path: 'backups', loadComponent: () => import('./features/admin/backups.component').then(m => m.BackupsComponent) },
          { path: 'settings', loadComponent: () => import('./features/admin/resource-list.component').then(m => m.ResourceListComponent), data: { resource: 'system-settings', title: 'Cấu hình hệ thống', columns: ['key', 'group', 'value', 'description', 'editable'] } },
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
        ]
      },
      {
        path: 'lecturer', canActivate: [roleGuard(['Lecturer'])], children: [
          { path: 'dashboard', loadComponent: () => import('./features/dashboards/dashboard.component').then(m => m.DashboardComponent) },
          { path: 'classes', loadComponent: () => import('./features/lecturer/classes.component').then(m => m.LecturerClassesComponent) },
          { path: 'classes/:id', loadComponent: () => import('./features/lecturer/class-students.component').then(m => m.ClassStudentsComponent) },
          { path: 'classes/:id/students', loadComponent: () => import('./features/lecturer/class-students.component').then(m => m.ClassStudentsComponent) },
          { path: 'classes/:id/grades', loadComponent: () => import('./features/lecturer/gradebook.component').then(m => m.GradebookComponent) },
          { path: 'classes/:id/statistics', loadComponent: () => import('./features/lecturer/class-statistics.component').then(m => m.ClassStatisticsComponent) },
          { path: 'classes/:id/clo', loadComponent: () => import('./features/lecturer/class-clo.component').then(m => m.ClassCloComponent) },
          { path: 'classes/:id/materials', loadComponent: () => import('./features/lecturer/materials.component').then(m => m.LecturerMaterialsComponent) },
          { path: 'classes/:id/assignments', loadComponent: () => import('./features/lecturer/assignments.component').then(m => m.LecturerAssignmentsComponent) },
          { path: 'assignments/:id/submissions', loadComponent: () => import('./features/lecturer/submissions.component').then(m => m.SubmissionsComponent) },
          { path: 'grades', loadComponent: () => import('./features/lecturer/gradebook.component').then(m => m.GradebookComponent) },
          { path: 'notifications', loadComponent: () => import('./features/common/notifications.component').then(m => m.NotificationsComponent) },
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
        ]
      },
      {
        path: 'student', canActivate: [roleGuard(['Student'])], children: [
          { path: 'dashboard', loadComponent: () => import('./features/dashboards/dashboard.component').then(m => m.DashboardComponent) },
          { path: 'current-courses', loadComponent: () => import('./features/student/current-courses.component').then(m => m.CurrentCoursesComponent) },
          { path: 'curriculum', loadComponent: () => import('./features/student/curriculum.component').then(m => m.CurriculumComponent) },
          { path: 'grades', loadComponent: () => import('./features/student/transcript.component').then(m => m.TranscriptComponent) },
          { path: 'grades/course/:id', loadComponent: () => import('./features/student/grades.component').then(m => m.GradesComponent) },
          { path: 'transcript', loadComponent: () => import('./features/student/transcript.component').then(m => m.TranscriptComponent) },
          { path: 'gpa', loadComponent: () => import('./features/student/gpa.component').then(m => m.GpaComponent) },
          { path: 'clo-results', loadComponent: () => import('./features/student/clo.component').then(m => m.CloComponent) },
          { path: 'schedule', loadComponent: () => import('./features/student/schedule.component').then(m => m.ScheduleComponent) },
          { path: 'materials', loadComponent: () => import('./features/student/materials.component').then(m => m.StudentMaterialsComponent) },
          { path: 'assignments', loadComponent: () => import('./features/student/assignments.component').then(m => m.StudentAssignmentsComponent) },
          { path: 'notifications', loadComponent: () => import('./features/common/notifications.component').then(m => m.NotificationsComponent) },
          { path: '', pathMatch: 'full', redirectTo: 'dashboard' }
        ]
      },
      { path: 'profile', loadComponent: () => import('./features/common/profile.component').then(m => m.ProfileComponent) },
      { path: 'notifications', loadComponent: () => import('./features/common/notifications.component').then(m => m.NotificationsComponent) }
    ]
  },
  { path: 'unauthorized', loadComponent: () => import('./features/common/error-page.component').then(m => m.ErrorPageComponent), data: { code: '403', title: 'Không có quyền truy cập' } },
  { path: '**', loadComponent: () => import('./features/common/error-page.component').then(m => m.ErrorPageComponent) }
];
