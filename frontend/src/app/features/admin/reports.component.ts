import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { AdminReport, ChartItem } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';

interface OptionItem {
  id: string;
  label: string;
}

interface LookupOption {
  id: string;
  code: string;
  name: string;
}

interface ReportOptions {
  academicYears: LookupOption[];
  semesters: LookupOption[];
  faculties: LookupOption[];
  programs: LookupOption[];
}

@Component({
  standalone: true,
  imports: [FormsModule, PageHeaderComponent, StatCardComponent],
  template: `
    <app-page-header
      title="Báo cáo đào tạo"
      subtitle="Lọc theo năm học, học kỳ, khoa và chương trình đào tạo.">
      @if (auth.hasPermission('admin.reports.export')) {
        <button class="secondary-button" (click)="exportExcel()">
          <span class="material-symbols-outlined">download</span>
          Xuất Excel
        </button>
      }
      @if (auth.hasPermission('admin.reports.export')) {
        <button class="secondary-button" (click)="exportPdf()">
          <span class="material-symbols-outlined">picture_as_pdf</span>
          Xuất PDF
        </button>
      }
    </app-page-header>

    <article class="panel filter-panel">
      <div class="form-grid">
        <label>Năm học
          <select [(ngModel)]="academicYearId">
            <option value="">Tất cả năm học</option>
            @for (item of academicYears(); track item.id) {
              <option [value]="item.id">{{ item.label }}</option>
            }
          </select>
        </label>
        <label>Học kỳ
          <select [(ngModel)]="semesterId">
            <option value="">Tất cả học kỳ</option>
            @for (item of semesters(); track item.id) {
              <option [value]="item.id">{{ item.label }}</option>
            }
          </select>
        </label>
        <label>Khoa
          <select [(ngModel)]="facultyId">
            <option value="">Tất cả khoa</option>
            @for (item of faculties(); track item.id) {
              <option [value]="item.id">{{ item.label }}</option>
            }
          </select>
        </label>
        <label>Chương trình
          <select [(ngModel)]="programId">
            <option value="">Tất cả chương trình</option>
            @for (item of programs(); track item.id) {
              <option [value]="item.id">{{ item.label }}</option>
            }
          </select>
        </label>
      </div>
      <div class="modal-actions">
        <button class="secondary-button" (click)="clearFilters()">Xóa bộ lọc</button>
        <button class="primary-button" (click)="load()">Áp dụng bộ lọc</button>
      </div>
    </article>

    @if (report(); as data) {
      <div class="stats-grid">
        @for (card of data.cards; track card.label) {
          <app-stat-card
            [label]="card.label"
            [value]="card.value"
            [icon]="card.icon"
            [trend]="card.trend || ''"
            [tone]="card.tone || 'primary'"/>
        }
      </div>
      <div class="dashboard-grid">
        <article class="panel">
          <div class="panel-heading"><div><h3>Sinh viên theo khoa</h3><p>Cơ cấu người học theo bộ lọc</p></div></div>
          <div class="bar-chart">
            @for (item of data.studentsByFaculty; track item.label) {
              <div class="bar-row"><span>{{ item.label }}</span><div><i [style.width.%]="percent(item, data.studentsByFaculty)"></i></div><b>{{ item.value }}</b></div>
            }
          </div>
        </article>
        <article class="panel">
          <div class="panel-heading"><div><h3>Trạng thái bảng điểm</h3><p>Tiến độ nhập và công bố</p></div></div>
          <div class="bar-chart">
            @for (item of data.gradeStatus; track item.label) {
              <div class="bar-row"><span>{{ item.label }}</span><div><i [style.width.%]="percent(item, data.gradeStatus)"></i></div><b>{{ item.value }}</b></div>
            }
          </div>
        </article>
        <article class="panel">
          <div class="panel-heading"><div><h3>Trạng thái học tập</h3></div></div>
          <div class="bar-chart">
            @for (item of data.learningStatus; track item.label) {
              <div class="bar-row"><span>{{ item.label }}</span><div><i [style.width.%]="percent(item, data.learningStatus)"></i></div><b>{{ item.value }}</b></div>
            }
          </div>
        </article>
        <article class="panel">
          <div class="panel-heading"><div><h3>Mức đạt CLO trung bình</h3><p>Tính bằng MongoDB Aggregation</p></div></div>
          <div class="bar-chart">
            @for (item of data.cloAchievement; track item.label) {
              <div class="bar-row"><span>{{ item.label }}</span><div><i [style.width.%]="item.value"></i></div><b>{{ item.value }}%</b></div>
            }
          </div>
        </article>
        <article class="panel span-2">
          <div class="panel-heading"><div><h3>Hoạt động gần đây</h3></div></div>
          <div class="activity-list">
            @for (activity of data.recentActivities; track $index) {
              <div><span class="activity-icon material-symbols-outlined">{{ activity.icon }}</span><div><b>{{ activity.title }}</b><p>{{ activity.description }}</p></div><time>{{ activity.time }}</time></div>
            }
          </div>
        </article>
      </div>
    } @else {
      <div class="skeleton-grid">
        <div class="skeleton"></div><div class="skeleton"></div>
        <div class="skeleton"></div><div class="skeleton"></div>
      </div>
    }
  `
})
export class ReportsComponent implements OnInit {
  readonly report = signal<AdminReport | null>(null);
  readonly academicYears = signal<OptionItem[]>([]);
  readonly semesters = signal<OptionItem[]>([]);
  readonly faculties = signal<OptionItem[]>([]);
  readonly programs = signal<OptionItem[]>([]);

  academicYearId = '';
  semesterId = '';
  facultyId = '';
  programId = '';

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService,
    readonly auth: AuthService
  ) {}

  ngOnInit() {
    this.loadOptions();
    this.load();
  }

  load() {
    this.report.set(null);
    this.api.get<AdminReport>('/admin/reports', this.params()).subscribe({
      next: response => this.report.set(response.data),
      error: error => this.toast.show(
        error.error?.message || 'Không thể tải báo cáo',
        'error'
      )
    });
  }

  clearFilters() {
    this.academicYearId = '';
    this.semesterId = '';
    this.facultyId = '';
    this.programId = '';
    this.load();
  }

  exportExcel() {
    this.api.getBlob('/admin/reports/export', this.params()).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `BaoCaoDaoTao-${new Date().toISOString().slice(0, 10)}.xlsx`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.show('Không thể xuất báo cáo Excel', 'error')
    });
  }

  percent(item: ChartItem, list: ChartItem[]) {
    const max = Math.max(...list.map(value => value.value), 1);
    return item.value / max * 100;
  }

  exportPdf() {
    this.api.getBlob('/admin/reports/export-pdf', this.params()).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `BaoCaoDaoTao-${new Date().toISOString().slice(0, 10)}.pdf`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.toast.show('Không thể xuất báo cáo PDF', 'error')
    });
  }

  private params() {
    return {
      academicYearId: this.academicYearId,
      semesterId: this.semesterId,
      facultyId: this.facultyId,
      programId: this.programId
    };
  }

  private loadOptions() {
    this.api.get<ReportOptions>('/admin/report-options').subscribe({
      next: response => {
        const options = response.data;
        this.academicYears.set(this.options(options.academicYears));
        this.semesters.set(this.options(options.semesters));
        this.faculties.set(this.options(options.faculties));
        this.programs.set(this.options(options.programs));
      },
      error: error => this.toast.show(
        error.error?.message || 'Không thể tải dữ liệu cho bộ lọc báo cáo',
        'error'
      )
    });
  }

  private options(items: LookupOption[] = []): OptionItem[] {
    return items.map(item => ({
      id: item.id,
      label: item.name
        ? `${item.code ? `${item.code} - ` : ''}${item.name}`
        : item.code || item.id
    }));
  }
}
