import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ScheduleItem } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Lịch học và lịch thi"
      subtitle="Theo dõi thời gian, phòng học và các ca thi của học kỳ hiện tại."
      eyebrow="SINH VIÊN">
    </app-page-header>

    <section class="portal-toolbar">
      <label>
        <span>Loại lịch</span>
        <select [ngModel]="typeFilter()" (ngModelChange)="typeFilter.set($event)">
          <option value="">Tất cả</option>
          <option value="Class">Lịch học</option>
          <option value="Exam">Lịch thi</option>
        </select>
      </label>
      <span class="portal-count">{{ filtered().length }} sự kiện</span>
    </section>

    @if (loading()) {
      <div class="portal-loading">Đang tải lịch học…</div>
    } @else {
      <section class="portal-timeline">
        @for (item of filtered(); track item.type + item.classSectionCode + item.startTime) {
          <article [class.exam]="item.type === 'Exam'">
            <div class="portal-time">
              <strong>{{ item.date ? (item.date | date:'dd/MM') : dayLabel(item.dayOfWeek) }}</strong>
              <span>{{ item.startTime }}–{{ item.endTime }}</span>
            </div>
            <div class="portal-timeline-icon">
              <span class="material-symbols-outlined">{{ item.type === 'Exam' ? 'quiz' : 'school' }}</span>
            </div>
            <div class="portal-timeline-content">
              <header>
                <div>
                  <small>{{ item.courseCode }} · {{ item.classSectionCode }}</small>
                  <h3>{{ item.courseName }}</h3>
                </div>
                <span class="badge" [class.danger]="item.type === 'Exam'">
                  {{ item.type === 'Exam' ? 'Lịch thi' : 'Lịch học' }}
                </span>
              </header>
              <p>
                <span class="material-symbols-outlined">location_on</span>
                Phòng {{ item.room || 'Chưa xếp' }}
                @if (item.lecturerName) { · {{ item.lecturerName }} }
              </p>
              @if (item.note) { <small>{{ item.note }}</small> }
            </div>
          </article>
        } @empty {
          <div class="portal-empty">
            <span class="material-symbols-outlined">calendar_month</span>
            <h3>Chưa có lịch</h3>
            <p>Lịch học hoặc lịch thi chưa được cập nhật.</p>
          </div>
        }
      </section>
    }
  `
})
export class StudentScheduleComponent implements OnInit {
  readonly items = signal<ScheduleItem[]>([]);
  readonly loading = signal(true);
  readonly typeFilter = signal('');

  readonly filtered = computed(() =>
    this.items().filter(item => !this.typeFilter() || item.type === this.typeFilter())
  );

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.api.get<ScheduleItem[]>('/student/schedule').subscribe({
      next: response => {
        this.items.set(response.data ?? []);
        this.loading.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải lịch học', 'error');
      }
    });
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
