import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ApiService } from '../core/services/api.service';
import { AuthService } from '../core/services/auth.service';
import { ToastContainerComponent } from '../shared/toast-container.component';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  description?: string;
}

interface NotificationItem {
  id: string;
  isRead: boolean;
}

interface FloatingPosition {
  x: number;
  y: number;
}

interface LauncherDragState {
  pointerId: number;
  startPointerX: number;
  startPointerY: number;
  startButtonX: number;
  startButtonY: number;
  buttonWidth: number;
  buttonHeight: number;
  moved: boolean;
}

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastContainerComponent],
  templateUrl: './app-layout.component.html'
})
export class AppLayoutComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly api = inject(ApiService);
  readonly auth = inject(AuthService);

  readonly launcherOpen = signal(false);
  readonly userMenuOpen = signal(false);
  readonly search = signal('');
  readonly unreadCount = signal(0);
  readonly darkMode = signal(localStorage.getItem('theme') === 'dark');
  readonly user = this.auth.user;
  readonly launcherDragging = signal(false);
  readonly launcherPosition = signal<FloatingPosition>(this.readLauncherPosition());

  private launcherDragState: LauncherDragState | null = null;
  private readonly launcherPositionStorageKey = 'eduManageFloatingLauncherPosition';

  readonly nav = computed<NavItem[]>(() => {
    const role = this.user()?.role;
    if (role === 'Admin') {
      return [
        { label: 'Tổng quan', icon: 'dashboard', route: '/admin/dashboard', description: 'Số liệu toàn hệ thống' },
        { label: 'Tài khoản', icon: 'group', route: '/admin/users', description: 'Quản lý truy cập' },
        { label: 'Sinh viên', icon: 'school', route: '/admin/students', description: 'Hồ sơ và học vụ' },
        { label: 'Giảng viên', icon: 'badge', route: '/admin/lecturers', description: 'Nhân sự giảng dạy' },
        { label: 'Khoa', icon: 'account_balance', route: '/admin/faculties' },
        { label: 'Chương trình đào tạo', icon: 'schema', route: '/admin/programs' },
        { label: 'Năm học', icon: 'date_range', route: '/admin/academic-years' },
        { label: 'Học kỳ', icon: 'calendar_view_month', route: '/admin/semesters' },
        { label: 'Môn học', icon: 'menu_book', route: '/admin/courses' },
        { label: 'Lớp học phần', icon: 'class', route: '/admin/class-sections' },
        { label: 'Duyệt bảng điểm', icon: 'fact_check', route: '/admin/gradebooks', description: 'Kiểm tra và công bố điểm' },
        { label: 'Cấu trúc điểm & CLO', icon: 'rule', route: '/admin/grading-schemes' },
        { label: 'Thông báo', icon: 'notifications', route: '/admin/notifications' },
        { label: 'Báo cáo', icon: 'analytics', route: '/admin/reports' },
        { label: 'Yêu cầu mở điểm', icon: 'lock_open', route: '/admin/grade-reopen-requests' },
        { label: 'Sao lưu', icon: 'backup', route: '/admin/backups' },
        { label: 'Nhật ký', icon: 'history', route: '/admin/audit-logs' },
        { label: 'Cấu hình', icon: 'settings', route: '/admin/settings' }
      ];
    }

    if (role === 'Lecturer') {
      return [
        { label: 'Tổng quan', icon: 'dashboard', route: '/lecturer/dashboard' },
        { label: 'Lớp phụ trách', icon: 'class', route: '/lecturer/classes' },
        { label: 'Nhập điểm', icon: 'edit_note', route: '/lecturer/grades' },
        { label: 'Thông báo', icon: 'notifications', route: '/notifications' },
        { label: 'Hồ sơ cá nhân', icon: 'manage_accounts', route: '/profile' }
      ];
    }

    return [
      { label: 'Tổng quan', icon: 'dashboard', route: '/student/dashboard' },
      { label: 'Kết quả học tập', icon: 'grading', route: '/student/grades' },
      { label: 'Chương trình khung', icon: 'view_list', route: '/student/curriculum' },
      { label: 'Môn đang học', icon: 'auto_stories', route: '/student/current-courses' },
      { label: 'GPA & tiến độ', icon: 'insights', route: '/student/gpa' },
      { label: 'Kết quả CLO', icon: 'radar', route: '/student/clo-results' },
      { label: 'Lịch học & thi', icon: 'calendar_month', route: '/student/schedule' },
      { label: 'Tài liệu', icon: 'folder_open', route: '/student/materials' },
      { label: 'Bài tập', icon: 'assignment', route: '/student/assignments' },
      { label: 'Thông báo', icon: 'notifications', route: '/notifications' },
      { label: 'Hồ sơ cá nhân', icon: 'manage_accounts', route: '/profile' }
    ];
  });

  readonly filteredNav = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return keyword
      ? this.nav().filter(item => `${item.label} ${item.description ?? ''}`.toLocaleLowerCase('vi').includes(keyword))
      : this.nav();
  });

  readonly primaryNav = computed(() => this.nav().slice(0, 6));
  readonly homeRoute = computed(() => this.auth.homeFor(this.user()?.role ?? 'Student'));

  ngOnInit(): void {
    document.body.classList.toggle('dark-theme', this.darkMode());
    requestAnimationFrame(() => this.clampLauncherToViewport());
    this.api.get<NotificationItem[]>('/notifications', { isRead: false, pageSize: 100 }).subscribe({
      next: response => this.unreadCount.set(response.data.length),
      error: () => this.unreadCount.set(0)
    });
  }

  submitSearch(): void {
    const first = this.filteredNav()[0];
    if (first) {
      this.router.navigateByUrl(first.route);
      this.search.set('');
      this.launcherOpen.set(false);
    }
  }

  startLauncherDrag(event: PointerEvent): void {
    if (event.button !== 0) {
      return;
    }

    const button = event.currentTarget as HTMLElement;
    const rect = button.getBoundingClientRect();

    event.preventDefault();
    button.setPointerCapture(event.pointerId);
    this.userMenuOpen.set(false);
    this.launcherDragging.set(true);
    this.launcherDragState = {
      pointerId: event.pointerId,
      startPointerX: event.clientX,
      startPointerY: event.clientY,
      startButtonX: this.launcherPosition().x,
      startButtonY: this.launcherPosition().y,
      buttonWidth: rect.width,
      buttonHeight: rect.height,
      moved: false
    };
  }

  moveLauncherDrag(event: PointerEvent): void {
    const state = this.launcherDragState;
    if (!state || state.pointerId !== event.pointerId) {
      return;
    }

    const deltaX = event.clientX - state.startPointerX;
    const deltaY = event.clientY - state.startPointerY;

    if (!state.moved && Math.hypot(deltaX, deltaY) >= 5) {
      state.moved = true;
    }

    if (!state.moved) {
      return;
    }

    event.preventDefault();
    const margin = 8;
    const maxX = Math.max(margin, window.innerWidth - state.buttonWidth - margin);
    const maxY = Math.max(margin, window.innerHeight - state.buttonHeight - margin);

    this.launcherPosition.set({
      x: Math.min(maxX, Math.max(margin, state.startButtonX + deltaX)),
      y: Math.min(maxY, Math.max(margin, state.startButtonY + deltaY))
    });
  }

  endLauncherDrag(event: PointerEvent): void {
    const state = this.launcherDragState;
    if (!state || state.pointerId !== event.pointerId) {
      return;
    }

    const button = event.currentTarget as HTMLElement;
    if (button.hasPointerCapture(event.pointerId)) {
      button.releasePointerCapture(event.pointerId);
    }

    this.launcherDragState = null;
    this.launcherDragging.set(false);

    if (state.moved) {
      this.saveLauncherPosition();
      return;
    }

    this.launcherOpen.set(!this.launcherOpen());
  }

  handleLauncherKeydown(event: KeyboardEvent): void {
    const step = event.shiftKey ? 30 : 12;
    const current = this.launcherPosition();

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.launcherOpen.set(!this.launcherOpen());
      return;
    }

    let x = current.x;
    let y = current.y;

    switch (event.key) {
      case 'ArrowLeft':
        x -= step;
        break;
      case 'ArrowRight':
        x += step;
        break;
      case 'ArrowUp':
        y -= step;
        break;
      case 'ArrowDown':
        y += step;
        break;
      default:
        return;
    }

    event.preventDefault();
    this.launcherPosition.set({ x, y });
    this.clampLauncherToViewport();
    this.saveLauncherPosition();
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.clampLauncherToViewport();
  }

  private readLauncherPosition(): FloatingPosition {
    const defaultPosition: FloatingPosition = {
      x: 18,
      y: typeof window !== 'undefined' && window.innerWidth <= 640 ? 145 : 165
    };

    try {
      const raw = localStorage.getItem('eduManageFloatingLauncherPosition');
      if (!raw) {
        return defaultPosition;
      }

      const saved = JSON.parse(raw) as Partial<FloatingPosition>;
      return {
        x: Number.isFinite(saved.x) ? Number(saved.x) : defaultPosition.x,
        y: Number.isFinite(saved.y) ? Number(saved.y) : defaultPosition.y
      };
    }
    catch {
      return defaultPosition;
    }
  }

  private clampLauncherToViewport(): void {
    const button = document.querySelector<HTMLElement>('.floating-launcher-button');
    if (!button) {
      return;
    }

    const margin = 8;
    const width = button.offsetWidth || 48;
    const height = button.offsetHeight || 48;
    const current = this.launcherPosition();

    this.launcherPosition.set({
      x: Math.min(Math.max(margin, current.x), Math.max(margin, window.innerWidth - width - margin)),
      y: Math.min(Math.max(margin, current.y), Math.max(margin, window.innerHeight - height - margin))
    });
  }

  private saveLauncherPosition(): void {
    localStorage.setItem(
      this.launcherPositionStorageKey,
      JSON.stringify(this.launcherPosition())
    );
  }

  toggleTheme(): void {
    const next = !this.darkMode();
    this.darkMode.set(next);
    document.body.classList.toggle('dark-theme', next);
    localStorage.setItem('theme', next ? 'dark' : 'light');
  }

  closeMenus(): void {
    this.launcherOpen.set(false);
    this.userMenuOpen.set(false);
  }

  logout(): void {
    this.closeMenus();
    this.auth.logout();
  }
}
