import { Routes } from '@angular/router'; import { authGuard } from './core/guards/auth.guard'; import { roleGuard } from './core/guards/role.guard';
export const routes:Routes=[
 {path:'login',loadComponent:()=>import('./features/auth/login.component').then(m=>m.LoginComponent)},
 {path:'forgot-password',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Quên mật khẩu'}},
 {path:'',canActivate:[authGuard],loadComponent:()=>import('./layouts/app-layout.component').then(m=>m.AppLayoutComponent),children:[
  {path:'admin',canActivate:[roleGuard(['Admin'])],children:[
   {path:'dashboard',loadComponent:()=>import('./features/dashboards/dashboard.component').then(m=>m.DashboardComponent)},
   {path:'users',loadComponent:()=>import('./features/admin/resource-list.component').then(m=>m.ResourceListComponent),data:{resource:'users',title:'Quản lý tài khoản',columns:['username','fullName','email','role','status']}},
   {path:'students',loadComponent:()=>import('./features/admin/resource-list.component').then(m=>m.ResourceListComponent),data:{resource:'students',title:'Quản lý sinh viên',columns:['studentCode','fullName','email','administrativeClass','status']}},
   {path:'lecturers',loadComponent:()=>import('./features/admin/resource-list.component').then(m=>m.ResourceListComponent),data:{resource:'lecturers',title:'Quản lý giảng viên',columns:['lecturerCode','fullName','email','degree','status']}},
   {path:'courses',loadComponent:()=>import('./features/admin/resource-list.component').then(m=>m.ResourceListComponent),data:{resource:'courses',title:'Quản lý môn học',columns:['courseCode','courseName','credits','facultyName','status']}},
   {path:'class-sections',loadComponent:()=>import('./features/admin/resource-list.component').then(m=>m.ResourceListComponent),data:{resource:'class-sections',title:'Quản lý lớp học phần',columns:['classSectionCode','courseName','lecturerName','semesterName','gradeStatus']}},
   {path:'grading-schemes',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Cấu trúc điểm động'}},
   {path:'notifications',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Quản lý thông báo'}},
   {path:'backups',loadComponent:()=>import('./features/admin/backups.component').then(m=>m.BackupsComponent)},
   {path:'audit-logs',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Nhật ký hệ thống'}},
   {path:'',pathMatch:'full',redirectTo:'dashboard'}]},
  {path:'lecturer',canActivate:[roleGuard(['Lecturer'])],children:[{path:'dashboard',loadComponent:()=>import('./features/dashboards/dashboard.component').then(m=>m.DashboardComponent)},{path:'grades',loadComponent:()=>import('./features/lecturer/gradebook.component').then(m=>m.GradebookComponent)},{path:':section',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Không gian giảng viên'}},{path:'',pathMatch:'full',redirectTo:'dashboard'}]},
  {path:'student',canActivate:[roleGuard(['Student'])],children:[{path:'dashboard',loadComponent:()=>import('./features/dashboards/dashboard.component').then(m=>m.DashboardComponent)},{path:'grades',loadComponent:()=>import('./features/student/grades.component').then(m=>m.GradesComponent)},{path:'clo-results',loadComponent:()=>import('./features/student/clo.component').then(m=>m.CloComponent)},{path:'gpa',loadComponent:()=>import('./features/student/gpa.component').then(m=>m.GpaComponent)},{path:':section',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Không gian sinh viên'}},{path:'',pathMatch:'full',redirectTo:'dashboard'}]},
  {path:'profile',loadComponent:()=>import('./features/common/simple-page.component').then(m=>m.SimplePageComponent),data:{title:'Hồ sơ cá nhân'}},{path:'notifications',loadComponent:()=>import('./features/common/notifications.component').then(m=>m.NotificationsComponent)},
 ]},
 {path:'unauthorized',loadComponent:()=>import('./features/common/error-page.component').then(m=>m.ErrorPageComponent),data:{code:'403',title:'Không có quyền truy cập'}},
 {path:'**',loadComponent:()=>import('./features/common/error-page.component').then(m=>m.ErrorPageComponent)}
];
