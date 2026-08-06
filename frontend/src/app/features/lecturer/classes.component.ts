import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  ChartItem,
  ClassCloStatistic,
  ClassStatistics,
  ClassStudent,
  LecturerClass
} from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, PageHeaderComponent],
  template: `
    <app-page-header
      title="Lớp học phần phụ trách"
      subtitle="Theo dõi sinh viên, tiến độ nhập điểm, thống kê kết quả và mức đạt CLO theo từng lớp."
      eyebrow="GIẢNG VIÊN">
      <div class="button-group">
        @if (selectedClass(); as selected) {
          <button class="secondary-button" type="button" (click)="exportGradebook(selected)">
            <span class="material-symbols-outlined">download</span>
            Xuất bảng điểm
          </button>
          <a class="primary-button" [routerLink]="['/lecturer/grades', selected.id]">
            <span class="material-symbols-outlined">edit_note</span>
            Mở sổ điểm
          </a>
        }
      </div>
    </app-page-header>

    <section class="portal-toolbar">
      <label class="portal-search">
        <span class="material-symbols-outlined">search</span>
        <input
          [ngModel]="search()"
          (ngModelChange)="search.set($event)"
          placeholder="Tìm theo mã lớp, mã môn hoặc tên môn">
      </label>
      <span class="portal-count">{{ filteredClasses().length }} lớp học phần</span>
    </section>

    @if (loading()) {
      <div class="skeleton-grid">
        @for (item of [1, 2, 3, 4]; track item) {
          <div class="skeleton"></div>
        }
      </div>
    } @else if (classes().length === 0) {
      <div class="portal-empty">
        <span class="material-symbols-outlined">co_present</span>
        <h3>Chưa có lớp học phần được phân công</h3>
        <p>Dữ liệu lớp sẽ xuất hiện sau khi quản trị viên phân công giảng viên.</p>
      </div>
    } @else {
      <div class="portal-master-detail">
        <aside class="portal-master-list">
          @for (item of filteredClasses(); track item.id) {
            <button
              type="button"
              class="portal-master-item"
              [class.active]="selectedId() === item.id"
              (click)="selectClass(item.id)">
              <span class="portal-master-icon material-symbols-outlined">school</span>
              <span>
                <strong>{{ item.classSectionCode }}</strong>
                <small>{{ item.courseCode }} · {{ item.courseName }}</small>
                <em>{{ item.semesterName }} · {{ item.studentCount }} sinh viên</em>
              </span>
              <span class="badge" [class.success]="item.gradeStatus === 'Published'">
                {{ statusLabel(item.gradeStatus) }}
              </span>
            </button>
          }
        </aside>

        <main class="portal-detail">
          @if (selectedClass(); as selected) {
            <section class="panel portal-section-heading">
              <div>
                <span class="eyebrow">{{ selected.courseCode }}</span>
                <h2>{{ selected.courseName }}</h2>
                <p>
                  {{ selected.classSectionCode }} · {{ selected.academicYearName }} ·
                  {{ selected.semesterName }}
                </p>
              </div>
              <div class="portal-actions">
                @if (selected.gradeStatus === 'Published' || selected.gradeStatus === 'Locked') {
                  <button class="secondary-button" type="button" (click)="requestReopen(selected)">
                    <span class="material-symbols-outlined">lock_open</span>
                    Yêu cầu mở điểm
                  </button>
                }
                <a class="primary-button" [routerLink]="['/lecturer/grades', selected.id]">
                  <span class="material-symbols-outlined">grading</span>
                  Nhập điểm
                </a>
              </div>
            </section>

            <section class="portal-kpis">
              <article class="portal-kpi">
                <span class="material-symbols-outlined">groups</span>
                <div><small>Sinh viên</small><strong>{{ stats()?.studentCount ?? selected.studentCount }}</strong></div>
              </article>
              <article class="portal-kpi">
                <span class="material-symbols-outlined">functions</span>
                <div><small>Điểm trung bình</small><strong>{{ stats()?.average ?? 0 | number:'1.1-2' }}</strong></div>
              </article>
              <article class="portal-kpi success">
                <span class="material-symbols-outlined">task_alt</span>
                <div><small>Tỷ lệ đạt</small><strong>{{ stats()?.passRate ?? 0 | number:'1.0-1' }}%</strong></div>
              </article>
              <article class="portal-kpi warning">
                <span class="material-symbols-outlined">warning</span>
                <div><small>Cần hỗ trợ</small><strong>{{ stats()?.failed ?? 0 }}</strong></div>
              </article>
            </section>

            <section class="panel">
              <div class="panel-heading">
                <div>
                  <h3>Lịch học</h3>
                  <p>Thời gian và phòng học đã cấu hình cho lớp học phần.</p>
                </div>
                <span class="badge">{{ statusLabel(selected.gradeStatus) }}</span>
              </div>
              <div class="portal-schedule-strip">
                @for (slot of selected.schedule; track slot.dayOfWeek + slot.startTime) {
                  <div>
                    <strong>{{ dayLabel(slot.dayOfWeek) }}</strong>
                    <span>{{ slot.startTime }}–{{ slot.endTime }}</span>
                    <small>Phòng {{ slot.room || 'Chưa xếp' }}</small>
                  </div>
                } @empty {
                  <p class="portal-muted">Chưa có lịch học.</p>
                }
              </div>
            </section>

            @if (detailLoading()) {
              <div class="portal-loading">Đang tải dữ liệu lớp học phần…</div>
            } @else {
              <section class="portal-two-column">
                <article class="panel">
                  <div class="panel-heading">
                    <div>
                      <h3>Phân bố điểm tổng kết</h3>
                      <p>Tính trực tiếp bằng MongoDB Aggregation.</p>
                    </div>
                  </div>
                  <div class="portal-bars">
                    @for (item of stats()?.distribution ?? []; track item.label) {
                      <div class="portal-bar-row">
                        <span>{{ item.label }}</span>
                        <div><i [style.width.%]="distributionPercent(item)"></i></div>
                        <strong>{{ item.value }}</strong>
                      </div>
                    } @empty {
                      <p class="portal-muted">Chưa đủ dữ liệu điểm để thống kê.</p>
                    }
                  </div>
                  @if (stats(); as value) {
                    <div class="portal-mini-stats">
                      <span>Cao nhất <b>{{ value.highest | number:'1.1-2' }}</b></span>
                      <span>Thấp nhất <b>{{ value.lowest | number:'1.1-2' }}</b></span>
                      <span>Trung vị <b>{{ value.median | number:'1.1-2' }}</b></span>
                      <span>Độ lệch chuẩn <b>{{ value.standardDeviation | number:'1.1-2' }}</b></span>
                    </div>
                  }
                </article>

                <article class="panel">
                  <div class="panel-heading">
                    <div>
                      <h3>Mức đạt CLO của lớp</h3>
                      <p>Tổng hợp từ các cột điểm có ánh xạ CLO.</p>
                    </div>
                  </div>
                  <div class="portal-clo-list">
                    @for (item of clos(); track item.cloCode) {
                      <div>
                        <header>
                          <strong>{{ item.cloCode }}</strong>
                          <span>{{ item.averagePercentage | number:'1.0-1' }}%</span>
                        </header>
                        <p>{{ item.description }}</p>
                        <div class="portal-progress">
                          <i [style.width.%]="clamp(item.averagePercentage)"></i>
                          <em [style.left.%]="clamp(item.threshold)"></em>
                        </div>
                        <small>
                          {{ item.passedStudents }}/{{ item.totalStudents }} sinh viên đạt ·
                          Ngưỡng {{ item.threshold }}%
                        </small>
                      </div>
                    } @empty {
                      <p class="portal-muted">Chưa có cấu hình CLO hoặc chưa có điểm.</p>
                    }
                  </div>
                </article>
              </section>

              <section class="panel table-panel">
                <div class="table-toolbar">
                  <div>
                    <strong>Danh sách sinh viên</strong>
                    <span class="portal-muted">{{ students().length }} sinh viên</span>
                  </div>
                </div>
                <div class="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th>Mã sinh viên</th>
                        <th>Họ tên</th>
                        <th>Email</th>
                        <th>Lớp hành chính</th>
                        <th>Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (student of students(); track student.id) {
                        <tr>
                          <td><strong>{{ student.studentCode }}</strong></td>
                          <td>{{ student.fullName }}</td>
                          <td>{{ student.email }}</td>
                          <td>{{ student.administrativeClass || '—' }}</td>
                          <td><span class="badge">{{ student.status }}</span></td>
                        </tr>
                      } @empty {
                        <tr><td colspan="5" class="empty">Lớp chưa có sinh viên.</td></tr>
                      }
                    </tbody>
                  </table>
                </div>
              </section>

              <section class="portal-two-column">
                <article class="panel">
                  <div class="panel-heading"><div><h3>Sinh viên nổi bật</h3><p>Top điểm tổng kết của lớp.</p></div></div>
                  <div class="portal-ranking">
                    @for (student of stats()?.topStudents ?? []; track student.studentId; let index = $index) {
                      <div>
                        <b>{{ index + 1 }}</b>
                        <span><strong>{{ student.fullName }}</strong><small>{{ student.studentCode }}</small></span>
                        <em>{{ student.finalScore | number:'1.1-2' }} · {{ student.letterGrade }}</em>
                      </div>
                    } @empty {
                      <p class="portal-muted">Chưa có dữ liệu.</p>
                    }
                  </div>
                </article>

                <article class="panel">
                  <div class="panel-heading"><div><h3>Sinh viên cần hỗ trợ</h3><p>Điểm tổng kết dưới mức cảnh báo.</p></div></div>
                  <div class="portal-ranking risk">
                    @for (student of stats()?.atRiskStudents ?? []; track student.studentId) {
                      <div>
                        <b>!</b>
                        <span><strong>{{ student.fullName }}</strong><small>{{ student.studentCode }}</small></span>
                        <em>{{ student.finalScore | number:'1.1-2' }}</em>
                      </div>
                    } @empty {
                      <p class="portal-muted">Không có sinh viên trong nhóm cảnh báo.</p>
                    }
                  </div>
                </article>
              </section>
            }
          }
        </main>
      </div>
    }
  `
})
export class LecturerClassesComponent implements OnInit {
  readonly loading = signal(true);
  readonly detailLoading = signal(false);
  readonly classes = signal<LecturerClass[]>([]);
  readonly selectedId = signal('');
  readonly students = signal<ClassStudent[]>([]);
  readonly stats = signal<ClassStatistics | null>(null);
  readonly clos = signal<ClassCloStatistic[]>([]);
  readonly search = signal('');

  readonly selectedClass = computed(() =>
    this.classes().find(item => item.id === this.selectedId()) ?? null
  );

  readonly filteredClasses = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    if (!keyword) return this.classes();
    return this.classes().filter(item =>
      `${item.classSectionCode} ${item.courseCode} ${item.courseName}`
        .toLocaleLowerCase('vi')
        .includes(keyword)
    );
  });

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.loadClasses();
  }

  loadClasses(): void {
    this.loading.set(true);
    this.api.get<LecturerClass[]>('/lecturer/classes').subscribe({
      next: response => {
        const rows = response.data ?? [];
        this.classes.set(rows);
        const nextId = rows.some(item => item.id === this.selectedId())
          ? this.selectedId()
          : rows[0]?.id ?? '';
        this.selectedId.set(nextId);
        this.loading.set(false);
        if (nextId) this.loadDetails(nextId);
      },
      error: error => {
        this.loading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải danh sách lớp', 'error');
      }
    });
  }

  selectClass(id: string): void {
    if (!id || id === this.selectedId()) return;
    this.selectedId.set(id);
    this.loadDetails(id);
  }

  loadDetails(id: string): void {
    this.detailLoading.set(true);
    forkJoin({
      students: this.api.get<ClassStudent[]>(`/lecturer/classes/${id}/students`),
      statistics: this.api.get<ClassStatistics>(`/lecturer/classes/${id}/statistics`),
      clos: this.api.get<ClassCloStatistic[]>(`/lecturer/classes/${id}/clo`)
    }).subscribe({
      next: response => {
        this.students.set(response.students.data ?? []);
        this.stats.set(response.statistics.data);
        this.clos.set(response.clos.data ?? []);
        this.detailLoading.set(false);
      },
      error: error => {
        this.students.set([]);
        this.stats.set(null);
        this.clos.set([]);
        this.detailLoading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải dữ liệu chi tiết lớp', 'error');
      }
    });
  }

  exportGradebook(item: LecturerClass): void {
    this.api.getBlob(`/lecturer/classes/${item.id}/gradebook/export`).subscribe({
      next: blob => this.download(blob, `bang-diem-${item.classSectionCode}.xlsx`),
      error: error => this.toast.show(error.error?.message || 'Xuất bảng điểm thất bại', 'error')
    });
  }

  requestReopen(item: LecturerClass): void {
    const reason = window.prompt(
      'Nhập lý do yêu cầu mở lại bảng điểm',
      'Cần điều chỉnh điểm sau khi rà soát'
    );
    if (!reason?.trim()) return;

    this.api.post(`/lecturer/classes/${item.id}/request-reopen`, {
      reason: reason.trim()
    }).subscribe({
      next: () => this.toast.show('Đã gửi yêu cầu mở lại bảng điểm', 'success'),
      error: error => this.toast.show(error.error?.message || 'Không thể gửi yêu cầu', 'error')
    });
  }

  distributionPercent(item: ChartItem): number {
    const total = Math.max(1, this.stats()?.studentCount ?? 1);
    return this.clamp(item.value * 100 / total);
  }

  clamp(value: number): number {
    return Math.min(100, Math.max(0, Number(value) || 0));
  }

  statusLabel(status: string): string {
    const values: Record<string, string> = {
      Draft: 'Bản nháp',
      InProgress: 'Đang nhập',
      Reopened: 'Đã mở lại',
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

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
