import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ScheduleItem } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';

type PeriodKey = 'morning' | 'afternoon' | 'evening';
type ScheduleKind = 'theory' | 'practice' | 'online' | 'exam' | 'suspended';

interface WeekDay {
  date: Date;
  dateKey: string;
  dayKey: string;
  label: string;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="schedule-card">
      <header class="schedule-toolbar">
        <div class="schedule-title-group">
          <h1>Lịch học, lịch thi theo tuần</h1>

          <div class="schedule-filters" role="radiogroup" aria-label="Lọc loại lịch">
            <label class="filter-option">
              <input
                type="radio"
                name="scheduleType"
                value=""
                [ngModel]="typeFilter()"
                (ngModelChange)="typeFilter.set($event)" />
              <span>Tất cả</span>
            </label>

            <label class="filter-option">
              <input
                type="radio"
                name="scheduleType"
                value="Class"
                [ngModel]="typeFilter()"
                (ngModelChange)="typeFilter.set($event)" />
              <span>Lịch học</span>
            </label>

            <label class="filter-option">
              <input
                type="radio"
                name="scheduleType"
                value="Exam"
                [ngModel]="typeFilter()"
                (ngModelChange)="typeFilter.set($event)" />
              <span>Lịch thi</span>
            </label>
          </div>
        </div>

        <div class="schedule-actions">
          <label class="date-picker" title="Chọn ngày thuộc tuần cần xem">
            <input
              type="date"
              [ngModel]="selectedDate()"
              (ngModelChange)="changeDate($event)" />
            <span class="material-symbols-outlined">calendar_month</span>
          </label>

          <button type="button" class="action-button primary" (click)="goToCurrentWeek()">
            <span class="material-symbols-outlined">today</span>
            <span>Hiện tại</span>
          </button>

          <button type="button" class="action-button primary" (click)="printSchedule()">
            <span class="material-symbols-outlined">print</span>
            <span>In lịch</span>
          </button>

          <button type="button" class="action-button primary" (click)="moveWeek(-1)">
            <span class="material-symbols-outlined">chevron_left</span>
            <span>Trở về</span>
          </button>

          <button type="button" class="action-button primary" (click)="moveWeek(1)">
            <span>Tiếp</span>
            <span class="material-symbols-outlined">chevron_right</span>
          </button>

          <button
            type="button"
            class="action-button icon-only primary"
            title="Toàn màn hình"
            aria-label="Toàn màn hình"
            (click)="toggleFullscreen()">
            <span class="material-symbols-outlined">open_in_full</span>
          </button>
        </div>
      </header>

      @if (loading()) {
        <div class="schedule-loading">
          <span class="material-symbols-outlined spin">progress_activity</span>
          <span>Đang tải lịch học…</span>
        </div>
      } @else {
        <div class="schedule-scroll">
          <div class="schedule-grid">
            <div class="grid-cell corner-cell">Ca học</div>

            @for (day of weekDays(); track day.dateKey) {
              <div class="grid-cell day-header" [class.today]="isToday(day.date)">
                <strong>{{ day.label }}</strong>
                <span>{{ formatDate(day.date) }}</span>
              </div>
            }

            @for (period of periods; track period.key) {
              <div class="grid-cell period-cell">
                <strong>{{ period.label }}</strong>
                <small>{{ period.timeLabel }}</small>
              </div>

              @for (day of weekDays(); track day.dateKey) {
                <div class="grid-cell day-cell" [class.today-column]="isToday(day.date)">
                  @for (item of eventsFor(day, period.key); track $index) {
                    <article
                      class="schedule-event"
                      [ngClass]="scheduleKind(item)"
                      [title]="eventTooltip(item)">
                      <h3>{{ item.courseName }}</h3>
                      <p class="section-code">{{ item.classSectionCode }}</p>
                      <p><strong>Tiết:</strong> {{ item.startTime }} - {{ item.endTime }}</p>
                      <p><strong>Phòng:</strong> {{ item.room || 'Chưa xếp' }}</p>
                      @if (item.lecturerName) {
                        <p><strong>GV:</strong> {{ item.lecturerName }}</p>
                      }
                      @if (item.note) {
                        <p class="event-note">{{ item.note }}</p>
                      }
                    </article>
                  }
                </div>
              }
            }
          </div>
        </div>

        @if (filtered().length === 0) {
          <div class="schedule-empty">
            <span class="material-symbols-outlined">event_busy</span>
            <div>
              <h3>Chưa có lịch phù hợp</h3>
              <p>Không có lịch học hoặc lịch thi theo bộ lọc đang chọn.</p>
            </div>
          </div>
        }

        <footer class="schedule-legend">
          <div><span class="legend-color theory"></span>Lịch học lý thuyết</div>
          <div><span class="legend-color practice"></span>Lịch học thực hành</div>
          <div><span class="legend-color online"></span>Lịch học trực tuyến</div>
          <div><span class="legend-color exam"></span>Lịch thi</div>
          <div><span class="legend-color suspended"></span>Lịch tạm ngưng</div>
        </footer>
      }
    </section>
  `,
  styles: [`
    :host {
      display: block;
      min-width: 0;
    }

    .schedule-card {
      overflow: hidden;
      border: 1px solid #d7e0e8;
      border-radius: 10px;
      background: #ffffff;
      box-shadow: 0 8px 24px rgba(15, 42, 68, 0.08);
    }

    .schedule-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 18px;
      padding: 14px 16px;
      border-bottom: 1px solid #dbe4ec;
      background: #ffffff;
    }

    .schedule-title-group {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 18px;
      min-width: 0;
    }

    .schedule-title-group h1 {
      margin: 0;
      color: #486071;
      font-size: 20px;
      font-weight: 800;
      white-space: nowrap;
    }

    .schedule-filters {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 14px;
    }

    .filter-option {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      color: #748491;
      font-size: 14px;
      cursor: pointer;
      user-select: none;
    }

    .filter-option input {
      width: 18px;
      height: 18px;
      margin: 0;
      accent-color: #1e9bf0;
      cursor: pointer;
    }

    .schedule-actions {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      flex-wrap: wrap;
      gap: 5px;
    }

    .date-picker {
      display: flex;
      align-items: center;
      height: 36px;
      overflow: hidden;
      border: 1px solid #c7d4df;
      border-radius: 3px;
      background: #ffffff;
    }

    .date-picker input {
      width: 122px;
      height: 100%;
      padding: 0 8px;
      border: 0;
      outline: 0;
      color: #203747;
      background: transparent;
      font: inherit;
    }

    .date-picker .material-symbols-outlined {
      display: grid;
      place-items: center;
      width: 34px;
      height: 100%;
      border-left: 1px solid #c7d4df;
      color: #0e78b5;
      font-size: 20px;
    }

    .action-button {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      gap: 4px;
      min-height: 36px;
      padding: 0 10px;
      border: 0;
      border-radius: 3px;
      font: inherit;
      font-size: 13px;
      font-weight: 700;
      cursor: pointer;
      transition: transform 0.15s ease, filter 0.15s ease;
    }

    .action-button:hover {
      filter: brightness(0.96);
      transform: translateY(-1px);
    }

    .action-button.primary {
      color: #ffffff;
      background: #1597e5;
    }

    .action-button.icon-only {
      width: 36px;
      padding: 0;
    }

    .action-button .material-symbols-outlined {
      font-size: 18px;
    }

    .schedule-loading {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 10px;
      min-height: 360px;
      color: #587183;
      font-weight: 700;
    }

    .spin {
      animation: spin 0.9s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .schedule-scroll {
      overflow: auto;
      background: #ffffff;
    }

    .schedule-grid {
      display: grid;
      grid-template-columns: 64px repeat(7, minmax(135px, 1fr));
      grid-template-rows: 58px repeat(3, minmax(192px, auto));
      min-width: 1020px;
      background:
        linear-gradient(rgba(92, 116, 134, 0.07) 1px, transparent 1px),
        linear-gradient(90deg, rgba(92, 116, 134, 0.07) 1px, transparent 1px);
      background-size: 18px 18px;
    }

    .grid-cell {
      border-right: 1px solid #cfdbe4;
      border-bottom: 1px solid #cfdbe4;
    }

    .corner-cell,
    .day-header,
    .period-cell {
      background: rgba(243, 248, 251, 0.96);
    }

    .corner-cell {
      display: grid;
      place-items: center;
      color: #188ddd;
      font-size: 13px;
      font-weight: 800;
    }

    .day-header {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 3px;
      color: #0d8dea;
      text-align: center;
    }

    .day-header strong {
      font-size: 15px;
      font-weight: 800;
    }

    .day-header span {
      font-size: 14px;
      font-weight: 700;
    }

    .day-header.today {
      background: #e9f7ff;
      box-shadow: inset 0 -3px #1597e5;
    }

    .period-cell {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 5px;
      color: #68757c;
      background: rgba(255, 254, 196, 0.94);
      text-align: center;
    }

    .period-cell strong {
      font-size: 15px;
      font-weight: 800;
    }

    .period-cell small {
      color: #8a8d6a;
      font-size: 10px;
    }

    .day-cell {
      position: relative;
      display: flex;
      align-items: flex-start;
      flex-direction: column;
      gap: 8px;
      min-width: 0;
      padding: 8px;
      background: rgba(255, 255, 255, 0.58);
    }

    .day-cell.today-column {
      background: rgba(233, 247, 255, 0.52);
    }

    .schedule-event {
      width: 100%;
      min-width: 0;
      padding: 8px 7px;
      border: 1px solid;
      border-radius: 4px;
      color: #063955;
      box-shadow: 0 2px 5px rgba(0, 0, 0, 0.06);
      overflow-wrap: anywhere;
    }

    .schedule-event h3 {
      margin: 0 0 3px;
      color: #003f64;
      font-size: 14px;
      line-height: 1.35;
      font-weight: 900;
    }

    .schedule-event p {
      margin: 2px 0;
      font-size: 12px;
      line-height: 1.35;
    }

    .schedule-event .section-code {
      font-weight: 700;
    }

    .schedule-event .event-note {
      margin-top: 5px;
      padding-top: 5px;
      border-top: 1px dashed rgba(4, 55, 84, 0.22);
      font-style: italic;
    }

    .schedule-event.theory {
      border-color: #b7c8d5;
      background: #e8eef2;
    }

    .schedule-event.practice {
      border-color: #50b72e;
      background: #66d62f;
    }

    .schedule-event.online {
      border-color: #159fe9;
      background: #85d0f4;
    }

    .schedule-event.exam {
      border-color: #e1d95d;
      background: #fff9a6;
    }

    .schedule-event.suspended {
      border-color: #d9362d;
      color: #ffffff;
      background: #e84338;
    }

    .schedule-event.suspended h3 {
      color: #ffffff;
    }

    .schedule-empty {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 14px;
      padding: 24px;
      border-top: 1px solid #e0e7ed;
      color: #627683;
      text-align: left;
    }

    .schedule-empty .material-symbols-outlined {
      color: #91a2ae;
      font-size: 38px;
    }

    .schedule-empty h3,
    .schedule-empty p {
      margin: 0;
    }

    .schedule-empty h3 {
      color: #3f5868;
      font-size: 16px;
    }

    .schedule-empty p {
      margin-top: 4px;
      font-size: 13px;
    }

    .schedule-legend {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 22px;
      padding: 18px 12px 20px;
      color: #6b7c88;
      font-size: 12px;
    }

    .schedule-legend > div {
      display: inline-flex;
      align-items: center;
      gap: 10px;
    }

    .legend-color {
      width: 40px;
      height: 15px;
      border: 1px solid rgba(30, 60, 80, 0.18);
    }

    .legend-color.theory { background: #e8eef2; }
    .legend-color.practice { background: #66d62f; }
    .legend-color.online { background: #85d0f4; }
    .legend-color.exam { background: #fff9a6; }
    .legend-color.suspended { background: #e84338; }

    @media (max-width: 1200px) {
      .schedule-toolbar {
        align-items: flex-start;
        flex-direction: column;
      }

      .schedule-actions {
        justify-content: flex-start;
      }
    }

    @media (max-width: 700px) {
      .schedule-card {
        border-radius: 6px;
      }

      .schedule-toolbar {
        padding: 12px;
      }

      .schedule-title-group {
        align-items: flex-start;
        flex-direction: column;
        gap: 10px;
      }

      .schedule-title-group h1 {
        white-space: normal;
      }

      .action-button span:not(.material-symbols-outlined) {
        display: none;
      }

      .action-button {
        width: 36px;
        padding: 0;
      }

      .schedule-legend {
        gap: 12px;
      }
    }

    @media print {
      :host {
        display: block;
      }

      .schedule-card {
        border: 0;
        box-shadow: none;
      }

      .schedule-actions,
      .schedule-filters {
        display: none !important;
      }

      .schedule-toolbar {
        padding: 0 0 10px;
        border-bottom: 1px solid #999;
      }

      .schedule-scroll {
        overflow: visible;
      }

      .schedule-grid {
        min-width: 0;
        grid-template-columns: 52px repeat(7, 1fr);
        grid-template-rows: 46px repeat(3, minmax(150px, auto));
        background-size: 12px 12px;
      }

      .schedule-event {
        break-inside: avoid;
        box-shadow: none;
      }

      .schedule-event h3 {
        font-size: 10px;
      }

      .schedule-event p,
      .schedule-legend {
        font-size: 8px;
      }
    }
  `]
})
export class StudentScheduleComponent implements OnInit {
  readonly items = signal<ScheduleItem[]>([]);
  readonly loading = signal(true);
  readonly typeFilter = signal('');
  readonly selectedDate = signal(this.toInputDate(new Date()));

  readonly periods: ReadonlyArray<{
    key: PeriodKey;
    label: string;
    timeLabel: string;
  }> = [
    { key: 'morning', label: 'Sáng', timeLabel: 'Trước 12:00' },
    { key: 'afternoon', label: 'Chiều', timeLabel: '12:00–17:59' },
    { key: 'evening', label: 'Tối', timeLabel: 'Từ 18:00' }
  ];

  readonly filtered = computed(() =>
    this.items().filter(item => !this.typeFilter() || item.type === this.typeFilter())
  );

  readonly weekDays = computed<WeekDay[]>(() => {
    const monday = this.startOfWeek(this.parseLocalDate(this.selectedDate()));
    const labels = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật'];
    const keys = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

    return Array.from({ length: 7 }, (_, index) => {
      const date = new Date(monday);
      date.setDate(monday.getDate() + index);

      return {
        date,
        dateKey: this.toInputDate(date),
        dayKey: keys[index],
        label: labels[index]
      };
    });
  });

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

  changeDate(value: string): void {
    if (value) {
      this.selectedDate.set(value);
    }
  }

  moveWeek(offset: number): void {
    const date = this.parseLocalDate(this.selectedDate());
    date.setDate(date.getDate() + offset * 7);
    this.selectedDate.set(this.toInputDate(date));
  }

  goToCurrentWeek(): void {
    this.selectedDate.set(this.toInputDate(new Date()));
  }

  printSchedule(): void {
    window.print();
  }

  async toggleFullscreen(): Promise<void> {
    const element = document.querySelector('.schedule-card') as HTMLElement | null;

    try {
      if (!document.fullscreenElement) {
        await element?.requestFullscreen();
      } else {
        await document.exitFullscreen();
      }
    } catch {
      this.toast.show('Trình duyệt không hỗ trợ chế độ toàn màn hình', 'info');
    }
  }

  eventsFor(day: WeekDay, period: PeriodKey): ScheduleItem[] {
    return this.filtered().filter(item =>
      this.matchesDay(item, day) && this.periodOf(item) === period
    );
  }

  scheduleKind(item: ScheduleItem): ScheduleKind {
    if (item.type === 'Exam') {
      return 'exam';
    }

    const searchText = [
      item.courseName,
      item.room,
      item.note
    ]
      .filter(Boolean)
      .join(' ')
      .toLocaleLowerCase('vi');

    if (/(tạm ngưng|nghỉ học|hủy|hoãn)/i.test(searchText)) {
      return 'suspended';
    }

    if (/(zoom|meet|teams|online|trực tuyến)/i.test(searchText)) {
      return 'online';
    }

    if (/(thực hành|phòng máy|laboratory|lab\b)/i.test(searchText)) {
      return 'practice';
    }

    return 'theory';
  }

  eventTooltip(item: ScheduleItem): string {
    const type = item.type === 'Exam' ? 'Lịch thi' : 'Lịch học';
    const parts = [
      type,
      item.courseName,
      item.classSectionCode,
      `${item.startTime} - ${item.endTime}`,
      item.room ? `Phòng ${item.room}` : '',
      item.lecturerName || '',
      item.note || ''
    ];

    return parts.filter(Boolean).join(' · ');
  }

  formatDate(date: Date): string {
    const day = String(date.getDate()).padStart(2, '0');
    const month = String(date.getMonth() + 1).padStart(2, '0');
    return `${day}/${month}/${date.getFullYear()}`;
  }

  isToday(date: Date): boolean {
    return this.toInputDate(date) === this.toInputDate(new Date());
  }

  private matchesDay(item: ScheduleItem, day: WeekDay): boolean {
    if (item.date) {
      return this.apiDateKey(item.date) === day.dateKey;
    }

    return this.normalizeDayKey(item.dayOfWeek) === day.dayKey;
  }

  private periodOf(item: ScheduleItem): PeriodKey {
    const minutes = this.timeToMinutes(item.startTime);

    if (minutes < 12 * 60) {
      return 'morning';
    }

    if (minutes < 18 * 60) {
      return 'afternoon';
    }

    return 'evening';
  }

  private timeToMinutes(value: string): number {
    const match = /^(\d{1,2}):(\d{2})/.exec(value ?? '');

    if (!match) {
      const periodNumber = Number.parseInt(value, 10);
      if (Number.isFinite(periodNumber)) {
        if (periodNumber <= 5) {
          return 7 * 60;
        }
        if (periodNumber <= 12) {
          return 13 * 60;
        }
        return 18 * 60;
      }
      return 7 * 60;
    }

    return Number(match[1]) * 60 + Number(match[2]);
  }

  private normalizeDayKey(value: string): string {
    const normalized = (value ?? '')
      .trim()
      .toLocaleLowerCase('vi')
      .replace(/\s+/g, ' ');

    const values: Record<string, string> = {
      monday: 'Monday',
      'thứ hai': 'Monday',
      'thứ 2': 'Monday',
      tuesday: 'Tuesday',
      'thứ ba': 'Tuesday',
      'thứ 3': 'Tuesday',
      wednesday: 'Wednesday',
      'thứ tư': 'Wednesday',
      'thứ 4': 'Wednesday',
      thursday: 'Thursday',
      'thứ năm': 'Thursday',
      'thứ 5': 'Thursday',
      friday: 'Friday',
      'thứ sáu': 'Friday',
      'thứ 6': 'Friday',
      saturday: 'Saturday',
      'thứ bảy': 'Saturday',
      'thứ 7': 'Saturday',
      sunday: 'Sunday',
      'chủ nhật': 'Sunday'
    };

    return values[normalized] ?? value;
  }

  private startOfWeek(date: Date): Date {
    const result = new Date(date);
    result.setHours(0, 0, 0, 0);

    const day = result.getDay();
    const offsetToMonday = day === 0 ? -6 : 1 - day;
    result.setDate(result.getDate() + offsetToMonday);

    return result;
  }

  private parseLocalDate(value: string): Date {
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }

  private toInputDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private apiDateKey(value: string): string {
    return value.length >= 10 ? value.slice(0, 10) : value;
  }
}
