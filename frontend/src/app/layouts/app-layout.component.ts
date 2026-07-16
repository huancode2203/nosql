import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/services/auth.service'; import { ToastContainerComponent } from '../shared/toast-container.component';
interface NavItem{label:string;icon:string;route:string;}
@Component({selector:'app-layout',standalone:true,imports:[RouterOutlet,RouterLink,RouterLinkActive,ToastContainerComponent],templateUrl:'./app-layout.component.html'})
export class AppLayoutComponent{
  public auth=inject(AuthService); collapsed=signal(false); mobileOpen=signal(false); user=this.auth.user;
  nav=computed<NavItem[]>(()=>{const role=this.user()?.role;if(role==='Admin')return [
    {label:'Tổng quan',icon:'dashboard',route:'/admin/dashboard'},{label:'Tài khoản',icon:'group',route:'/admin/users'},{label:'Sinh viên',icon:'school',route:'/admin/students'},
    {label:'Giảng viên',icon:'badge',route:'/admin/lecturers'},{label:'Môn học',icon:'menu_book',route:'/admin/courses'},{label:'Lớp học phần',icon:'class',route:'/admin/class-sections'},
    {label:'Cấu trúc điểm',icon:'rule',route:'/admin/grading-schemes'},{label:'Thông báo',icon:'notifications',route:'/admin/notifications'},{label:'Sao lưu',icon:'backup',route:'/admin/backups'},{label:'Nhật ký',icon:'history',route:'/admin/audit-logs'}];
    if(role==='Lecturer')return [{label:'Tổng quan',icon:'dashboard',route:'/lecturer/dashboard'},{label:'Lớp phụ trách',icon:'class',route:'/lecturer/classes'},{label:'Nhập điểm',icon:'edit_note',route:'/lecturer/grades'},{label:'Thống kê',icon:'analytics',route:'/lecturer/statistics'},{label:'Thông báo',icon:'notifications',route:'/notifications'}];
    return [{label:'Tổng quan',icon:'dashboard',route:'/student/dashboard'},{label:'Môn đang học',icon:'auto_stories',route:'/student/current-courses'},{label:'Kết quả học tập',icon:'grading',route:'/student/grades'},{label:'Bảng điểm',icon:'description',route:'/student/transcript'},{label:'GPA',icon:'insights',route:'/student/gpa'},{label:'Kết quả CLO',icon:'radar',route:'/student/clo-results'},{label:'Lịch học',icon:'calendar_month',route:'/student/schedule'},{label:'Thông báo',icon:'notifications',route:'/notifications'}];});
  logout(){this.auth.logout();}
}
