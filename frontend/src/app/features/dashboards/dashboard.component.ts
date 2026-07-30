import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardData } from '../../core/models/api.models';
import { LecturerClass, ScheduleItem, SemesterAverageChart, SemesterOption, StudentCourse, UserProfile } from '../../core/models/portal.models';
import { LoadingComponent } from '../../shared/loading.component';

interface QuickAction {
  label: string;
  icon: string;
  route: string;
  tone?: string;
  permission?: string;
}

@Component({
  standalone: true,
  imports: [RouterLink, DatePipe, LoadingComponent],
  templateUrl: './dashboard.component.html'
})
export class DashboardComponent implements OnInit {
  readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  readonly user = this.auth.user;
  readonly data = signal<DashboardData | null>(null);
  readonly profile = signal<UserProfile | null>(null);
  readonly studentCourses = signal<StudentCourse[]>([]);
  readonly lecturerClasses = signal<LecturerClass[]>([]);
  readonly schedule = signal<ScheduleItem[]>([]);
  readonly semesterOptions = signal<SemesterOption[]>([]);
  readonly selectedSemesterKey = signal('');
  readonly semesterChart = signal<SemesterAverageChart | null>(null);
  readonly loading = signal(true);

  readonly role = computed(() => this.user()?.role ?? 'Student');
  readonly isStudent = computed(() => this.role() === 'Student');
  readonly isLecturer = computed(() => this.role() === 'Lecturer');
  readonly isAdmin = computed(() => this.role() === 'Admin');

  readonly quickActions = computed<QuickAction[]>(() => {
    if (this.isAdmin()) {
      const actions: QuickAction[] = [
        { label: 'Tài khoản', icon: 'group', route: '/admin/users', permission: 'admin.resources.read' },
        { label: 'Sinh viên', icon: 'school', route: '/admin/students', permission: 'admin.resources.read' },
        { label: 'Giảng viên', icon: 'badge', route: '/admin/lecturers', permission: 'admin.resources.read' },
        { label: 'Môn học', icon: 'menu_book', route: '/admin/courses', permission: 'admin.resources.read' },
        { label: 'Lớp học phần', icon: 'class', route: '/admin/class-sections', permission: 'admin.resources.read' },
        { label: 'Cấu trúc điểm', icon: 'rule', route: '/admin/grading-schemes', permission: 'admin.settings.manage' },
        { label: 'Báo cáo', icon: 'analytics', route: '/admin/reports', permission: 'admin.reports.read' },
        { label: 'Sao lưu', icon: 'backup', route: '/admin/backups', permission: 'admin.backups.read' }
      ];
      return actions.filter(action =>
        !action.permission || this.auth.hasPermission(action.permission)
      );
    }
    if (this.isLecturer()) {
      return [
        { label: 'Lớp phụ trách', icon: 'class', route: '/lecturer/classes' },
        { label: 'Nhập điểm', icon: 'edit_note', route: '/lecturer/grades' },
        { label: 'Thống kê lớp', icon: 'monitoring', route: '/lecturer/classes' },
        { label: 'Tài liệu', icon: 'folder_open', route: '/lecturer/classes' },
        { label: 'Bài tập', icon: 'assignment', route: '/lecturer/classes' },
        { label: 'Thông báo', icon: 'notifications', route: '/notifications' }
      ];
    }
    return [
      { label: 'Kết quả học tập', icon: 'grading', route: '/student/grades' },
      { label: 'Lịch theo tuần', icon: 'calendar_month', route: '/student/schedule' },
      { label: 'Chương trình khung', icon: 'view_list', route: '/student/curriculum' },
      { label: 'Lớp học phần', icon: 'layers', route: '/student/current-courses' },
      { label: 'GPA & tiến độ', icon: 'query_stats', route: '/student/gpa' },
      { label: 'Chuẩn đầu ra CLO', icon: 'radar', route: '/student/clo-results' },
      { label: 'Tài liệu', icon: 'folder_open', route: '/student/materials' },
      { label: 'Bài tập', icon: 'assignment', route: '/student/assignments' },
      { label: 'Hộp thư', icon: 'mail', route: '/notifications' }
    ];
  });

  readonly requiredCredits = computed(() => this.profile()?.requiredCredits || 130);
  readonly passedCredits = computed(() => Number(this.data()?.cards.find(card => card.label.toLocaleLowerCase('vi').includes('tín chỉ'))?.value ?? 0));
  readonly progressPercent = computed(() => Math.min(100, Math.round(this.passedCredits() * 1000 / Math.max(1, this.requiredCredits())) / 10));
  readonly currentItems = computed(() => this.isStudent() ? this.studentCourses().slice(0, 5) : this.lecturerClasses().slice(0, 5));

  ngOnInit(): void {
    const rolePath = this.role().toLowerCase();
    this.api.get<DashboardData>(`/${rolePath}/dashboard`).subscribe({
      next: response => this.data.set(response.data),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false)
    });
    this.api.get<UserProfile>('/profile').subscribe({ next: response => this.profile.set(response.data) });

    if (this.isStudent()) {
      this.api.get<StudentCourse[]>('/student/current-courses').subscribe({ next: response => this.studentCourses.set(response.data) });
      this.api.get<ScheduleItem[]>('/student/schedule').subscribe({ next: response => this.schedule.set(response.data) });
      this.api.get<SemesterOption[]>('/student/semester-options').subscribe({
        next: response => {
          this.semesterOptions.set(response.data);
          const selected = response.data.find(item => item.allCoursesGraded) ?? response.data[0];
          if (selected) {
            this.selectedSemesterKey.set(selected.key);
            this.loadSemesterChart(selected.key);
          }
        }
      });
    } else if (this.isLecturer()) {
      this.api.get<LecturerClass[]>('/lecturer/classes').subscribe({ next: response => this.lecturerClasses.set(response.data) });
    }
  }


  onSemesterChange(event: Event): void {
    const key = (event.target as HTMLSelectElement).value;
    this.selectedSemesterKey.set(key);
    this.loadSemesterChart(key);
  }

  loadSemesterChart(key: string): void {
    if (!key) return;
    this.api.get<SemesterAverageChart>('/student/semester-average-chart', { semesterKey: key }).subscribe({
      next: response => this.semesterChart.set(response.data),
      error: () => this.semesterChart.set(null)
    });
  }

  chartBarHeight(score: number): number {
    return Math.max(0, Math.min(100, score * 10));
  }

  maxDistribution(): number {
    return Math.max(1, ...(this.data()?.gradeDistribution.map(item => item.value) ?? [1]));
  }

  todayLabel(): string {
    return new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date());
  }

  roleName(): string {
    return this.isAdmin() ? 'Quản trị viên' : this.isLecturer() ? 'Giảng viên' : 'Sinh viên';
  }

  classRoute(id: string): string {
    return this.isLecturer() ? `/lecturer/classes/${id}` : '/student/current-courses';
  }

  scheduleCount(type: string): number {
    return this.schedule().filter(item => item.type === type).length;
  }

  print(): void {
    window.print();
  }
}
