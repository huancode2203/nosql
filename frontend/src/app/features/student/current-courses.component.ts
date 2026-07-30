import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StudentCourse } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageHeaderComponent],
  template: `
    <app-page-header
      title="Môn học đang học"
      subtitle="Danh sách lớp học phần, giảng viên và lịch học của học kỳ hiện tại."
      eyebrow="SINH VIÊN">
      <div class="button-group">
        <a class="secondary-button" routerLink="/student/materials">
          <span class="material-symbols-outlined">folder_open</span>
          Tài liệu
        </a>
        <a class="primary-button" routerLink="/student/assignments">
          <span class="material-symbols-outlined">assignment</span>
          Bài tập
        </a>
      </div>
    </app-page-header>

    <section class="portal-toolbar">
      <label class="portal-search">
        <span class="material-symbols-outlined">search</span>
        <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Tìm môn học hoặc lớp học phần">
      </label>
      <span class="portal-count">{{ filtered().length }} môn học</span>
    </section>

    @if (loading()) {
      <div class="skeleton-grid">
        @for (item of [1, 2, 3, 4]; track item) { <div class="skeleton"></div> }
      </div>
    } @else {
      <section class="portal-card-grid">
        @for (item of filtered(); track item.classSectionId) {
          <article class="portal-card portal-course-card">
            <header>
              <span class="portal-card-icon material-symbols-outlined">auto_stories</span>
              <span class="badge" [class.success]="item.scoreStatus === 'Published'">
                {{ statusLabel(item.scoreStatus) }}
              </span>
            </header>
            <small>{{ item.courseCode }} · {{ item.credits }} tín chỉ</small>
            <h3>{{ item.courseName }}</h3>
            <p>
              Lớp {{ item.classSectionCode }} · Giảng viên {{ item.lecturerName || 'Chưa phân công' }}
            </p>
            <div class="portal-meta-list">
              <span><b>Năm học:</b> {{ item.academicYearName }}</span>
              <span><b>Học kỳ:</b> {{ item.semesterName }}</span>
            </div>
            <div class="portal-schedule-strip compact">
              @for (slot of item.schedule; track slot.dayOfWeek + slot.startTime) {
                <div>
                  <strong>{{ dayLabel(slot.dayOfWeek) }}</strong>
                  <span>{{ slot.startTime }}–{{ slot.endTime }}</span>
                  <small>{{ slot.room || 'Chưa xếp phòng' }}</small>
                </div>
              } @empty {
                <p class="portal-muted">Chưa có lịch học.</p>
              }
            </div>
            <footer>
              <a class="text-button" routerLink="/student/materials">
                <span class="material-symbols-outlined">description</span>
                Tài liệu
              </a>
              <a class="text-button" routerLink="/student/assignments">
                <span class="material-symbols-outlined">task</span>
                Bài tập
              </a>
            </footer>
          </article>
        } @empty {
          <div class="portal-empty span-2">
            <span class="material-symbols-outlined">school</span>
            <h3>Chưa có môn học trong học kỳ hiện tại</h3>
            <p>Liên hệ phòng đào tạo khi thông tin đăng ký học phần chưa được cập nhật.</p>
          </div>
        }
      </section>
    }
  `
})
export class StudentCurrentCoursesComponent implements OnInit {
  readonly courses = signal<StudentCourse[]>([]);
  readonly loading = signal(true);
  readonly search = signal('');

  readonly filtered = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return this.courses().filter(item =>
      !keyword ||
      `${item.courseCode} ${item.courseName} ${item.classSectionCode} ${item.lecturerName}`
        .toLocaleLowerCase('vi')
        .includes(keyword)
    );
  });

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.api.get<StudentCourse[]>('/student/current-courses').subscribe({
      next: response => {
        this.courses.set(response.data ?? []);
        this.loading.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải môn đang học', 'error');
      }
    });
  }

  statusLabel(status: string): string {
    const values: Record<string, string> = {
      Draft: 'Đang cập nhật',
      Submitted: 'Chờ duyệt',
      Published: 'Đã công bố',
      Locked: 'Đã khóa'
    };
    return values[status] ?? status;
  }

  dayLabel(day: string): string {
    const values: Record<string, string> = {
      Monday: 'Thứ Hai',
      Tuesday: 'Thứ Ba',
      Wednesday: 'Thứ Tư',
      Thursday: 'Thứ Năm',
      Friday: 'Thứ Sáu',
      Saturday: 'Thứ Bảy',
      Sunday: 'Chủ Nhật'
    };
    return values[day] ?? day;
  }
}
