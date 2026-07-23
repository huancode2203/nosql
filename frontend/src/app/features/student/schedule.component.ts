import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { ScheduleItem } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

const DAY_NAMES: Record<string, string> = {
  Monday: 'Thứ Hai', Tuesday: 'Thứ Ba', Wednesday: 'Thứ Tư', Thursday: 'Thứ Năm',
  Friday: 'Thứ Sáu', Saturday: 'Thứ Bảy', Sunday: 'Chủ Nhật'
};

@Component({
  standalone: true,
  imports: [DatePipe, PageHeaderComponent],
  template: `
  <app-page-header title="Lịch học và lịch thi" subtitle="Tổng hợp thời khóa biểu và lịch thi của các lớp đã đăng ký."></app-page-header>
  <section class="academic-filter-bar panel compact-filters">
    <label>Loại lịch<select [value]="typeFilter()" (change)="typeFilter.set($any($event.target).value)"><option value="all">Tất cả</option><option value="Class">Lịch học</option><option value="Exam">Lịch thi</option></select></label>
    <div class="schedule-summary"><span class="status-chip">{{ classCount() }} buổi học</span><span class="status-chip warning">{{ examCount() }} lịch thi</span></div>
  </section>
  <div class="schedule-grid">
    @for (day of days; track day) {
      <article class="panel">
        <div class="panel-heading"><div><span class="eyebrow">{{ dayLabel(day).toLocaleUpperCase('vi') }}</span><h3>{{ dayLabel(day) }}</h3></div><span class="badge">{{ byDay(day).length }} lịch</span></div>
        <div class="timeline">
          @for (item of byDay(day); track item.type + item.courseCode + item.startTime + item.date) {
            <div class="timeline-item" [class.exam]="item.type === 'Exam'">
              <time>{{ item.startTime }}<small>{{ item.endTime }}</small></time>
              <div><span class="eyebrow">{{ item.type === 'Exam' ? 'LỊCH THI' : item.classSectionCode }}</span><h4>{{ item.courseCode }} - {{ item.courseName }}</h4><p>{{ item.room }} · {{ item.lecturerName || item.note }}</p>@if (item.date) { <small>{{ item.date | date:'dd/MM/yyyy' }}</small> }</div>
            </div>
          } @empty { <div class="empty compact-empty">Không có lịch</div> }
        </div>
      </article>
    }
  </div>`
})
export class ScheduleComponent implements OnInit {
  readonly items = signal<ScheduleItem[]>([]);
  readonly typeFilter = signal('all');
  readonly days = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];
  readonly classCount = computed(() => this.items().filter(item => item.type !== 'Exam').length);
  readonly examCount = computed(() => this.items().filter(item => item.type === 'Exam').length);

  constructor(private readonly api: ApiService) {}
  ngOnInit(): void { this.api.get<ScheduleItem[]>('/student/schedule').subscribe(response => this.items.set(response.data)); }
  dayLabel(day: string): string { return DAY_NAMES[day] || day; }
  byDay(day: string): ScheduleItem[] {
    return this.items().filter(item => item.dayOfWeek === day && (this.typeFilter() === 'all' || item.type === this.typeFilter()));
  }
}
