import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  AssignmentItem,
  LecturerClass,
  SubmissionItem
} from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface AssignmentForm {
  classSectionId: string;
  title: string;
  content: string;
  attachmentUrl: string;
  maxScore: number;
  openAt: string;
  dueAt: string;
  allowLate: boolean;
  latePenaltyPercent: number;
  cloCodesText: string;
  linkedComponentId: string;
  status: string;
}

interface GradeForm {
  score: number | null;
  feedback: string;
  resubmissionAllowed: boolean;
  status: string;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Bài tập và chấm bài"
      subtitle="Giao bài, theo dõi bài nộp và cập nhật điểm phản hồi cho sinh viên."
      eyebrow="GIẢNG VIÊN">
      <button class="primary-button" type="button" (click)="openCreate()">
        <span class="material-symbols-outlined">add_task</span>
        Tạo bài tập
      </button>
    </app-page-header>

    <section class="portal-toolbar">
      <label>
        <span>Lớp học phần</span>
        <select [ngModel]="classFilter()" (ngModelChange)="changeClass($event)">
          <option value="">Tất cả lớp phụ trách</option>
          @for (item of classes(); track item.id) {
            <option [value]="item.id">{{ item.classSectionCode }} · {{ item.courseName }}</option>
          }
        </select>
      </label>
      <label class="portal-search">
        <span class="material-symbols-outlined">search</span>
        <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Tìm bài tập">
      </label>
      <span class="portal-count">{{ filtered().length }} bài tập</span>
    </section>

    @if (loading()) {
      <div class="portal-loading">Đang tải bài tập…</div>
    } @else {
      <section class="portal-card-grid">
        @for (item of filtered(); track item.id) {
          <article class="portal-card portal-assignment-card">
            <header>
              <span class="portal-card-icon material-symbols-outlined">assignment</span>
              <span class="badge" [class.success]="item.status === 'Open'">{{ assignmentStatus(item.status) }}</span>
            </header>
            <small>{{ item.classSectionCode }} · {{ item.courseCode }}</small>
            <h3>{{ item.title }}</h3>
            <p>{{ item.content || 'Không có mô tả.' }}</p>
            <div class="portal-meta-list">
              <span><b>Mở:</b> {{ item.openAt | date:'dd/MM/yyyy HH:mm' }}</span>
              <span><b>Hạn nộp:</b> {{ item.dueAt | date:'dd/MM/yyyy HH:mm' }}</span>
              <span><b>Điểm tối đa:</b> {{ item.maxScore }}</span>
              <span><b>Bài đã nộp:</b> {{ item.submissionCount }}</span>
              <span><b>CLO:</b> {{ item.cloCodes.join(', ') || '—' }}</span>
            </div>
            <footer>
              <button class="text-button" type="button" (click)="openSubmissions(item)">
                <span class="material-symbols-outlined">fact_check</span>
                Chấm bài ({{ item.submissionCount }})
              </button>
              <span class="portal-actions">
                <button class="icon-button" type="button" title="Chỉnh sửa" (click)="openEdit(item)">
                  <span class="material-symbols-outlined">edit</span>
                </button>
                <button class="icon-button danger" type="button" title="Xóa" (click)="remove(item)">
                  <span class="material-symbols-outlined">delete</span>
                </button>
              </span>
            </footer>
          </article>
        } @empty {
          <div class="portal-empty span-2">
            <span class="material-symbols-outlined">assignment</span>
            <h3>Chưa có bài tập</h3>
            <p>Tạo bài tập đầu tiên cho lớp học phần phụ trách.</p>
          </div>
        }
      </section>
    }

    @if (formModal()) {
      <div class="modal-backdrop" (click)="closeForm()">
        <section class="modal portal-modal-wide" (click)="$event.stopPropagation()">
          <div class="modal-heading">
            <div>
              <h3>{{ editingId ? 'Cập nhật bài tập' : 'Tạo bài tập' }}</h3>
              <p>Thiết lập thời gian, thang điểm và liên kết với cột điểm.</p>
            </div>
            <button class="icon-button" type="button" (click)="closeForm()">
              <span class="material-symbols-outlined">close</span>
            </button>
          </div>
          <div class="portal-form-grid">
            <label class="span-2">
              <span>Lớp học phần *</span>
              <select [(ngModel)]="form.classSectionId">
                <option value="">Chọn lớp học phần</option>
                @for (item of classes(); track item.id) {
                  <option [value]="item.id">{{ item.classSectionCode }} · {{ item.courseName }}</option>
                }
              </select>
            </label>
            <label class="span-2"><span>Tiêu đề *</span><input [(ngModel)]="form.title"></label>
            <label class="span-2"><span>Nội dung</span><textarea [(ngModel)]="form.content" rows="4"></textarea></label>
            <label class="span-2"><span>Đường dẫn tệp đính kèm</span><input [(ngModel)]="form.attachmentUrl" placeholder="https://…"></label>
            <label><span>Thời điểm mở *</span><input type="datetime-local" [(ngModel)]="form.openAt"></label>
            <label><span>Hạn nộp *</span><input type="datetime-local" [(ngModel)]="form.dueAt"></label>
            <label><span>Điểm tối đa</span><input type="number" min="0.1" step="0.1" [(ngModel)]="form.maxScore"></label>
            <label><span>Trạng thái</span>
              <select [(ngModel)]="form.status">
                <option value="Draft">Bản nháp</option>
                <option value="Open">Đang mở</option>
                <option value="Closed">Đã đóng</option>
              </select>
            </label>
            <label><span>Mã cột điểm liên kết</span><input [(ngModel)]="form.linkedComponentId" placeholder="BT"></label>
            <label><span>CLO, phân cách bằng dấu phẩy</span><input [(ngModel)]="form.cloCodesText" placeholder="CLO1, CLO2"></label>
            <label class="portal-check"><input type="checkbox" [(ngModel)]="form.allowLate"><span>Cho phép nộp trễ</span></label>
            <label><span>Trừ điểm nộp trễ (%)</span><input type="number" min="0" max="100" [(ngModel)]="form.latePenaltyPercent"></label>
          </div>
          <div class="modal-actions">
            <button class="secondary-button" type="button" (click)="closeForm()">Hủy</button>
            <button class="primary-button" type="button" [disabled]="saving()" (click)="save()">
              {{ saving() ? 'Đang lưu…' : 'Lưu bài tập' }}
            </button>
          </div>
        </section>
      </div>
    }

    @if (submissionModal()) {
      <div class="modal-backdrop" (click)="closeSubmissions()">
        <section class="modal portal-modal-xl" (click)="$event.stopPropagation()">
          <div class="modal-heading">
            <div>
              <h3>Bài nộp · {{ selectedAssignment()?.title }}</h3>
              <p>Chấm điểm, phản hồi và cho phép sinh viên nộp lại.</p>
            </div>
            <button class="icon-button" type="button" (click)="closeSubmissions()">
              <span class="material-symbols-outlined">close</span>
            </button>
          </div>

          @if (submissionsLoading()) {
            <div class="portal-loading">Đang tải bài nộp…</div>
          } @else {
            <div class="portal-submission-list">
              @for (item of submissions(); track item.id) {
                <article>
                  <header>
                    <div>
                      <strong>{{ item.studentName }}</strong>
                      <small>{{ item.studentCode }} · {{ item.submittedAt | date:'dd/MM/yyyy HH:mm' }}</small>
                    </div>
                    <span class="badge" [class.danger]="item.isLate">
                      {{ item.isLate ? 'Nộp trễ' : item.status }}
                    </span>
                  </header>
                  <p>{{ item.textContent || 'Không có nội dung văn bản.' }}</p>
                  @if (item.files?.length) {
                    <div class="portal-file-list">
                      @for (file of item.files; track file.url) {
                        <a [href]="api.assetUrl(file.url)" target="_blank" rel="noopener">
                          <span class="material-symbols-outlined">attach_file</span>
                          {{ file.originalName }}
                        </a>
                      }
                    </div>
                  }
                  <div class="portal-grade-form">
                    <label>
                      <span>Điểm</span>
                      <input
                        type="number"
                        min="0"
                        [max]="selectedAssignment()?.maxScore ?? 10"
                        step="0.1"
                        [(ngModel)]="gradeForms[item.id].score">
                    </label>
                    <label class="span-2">
                      <span>Phản hồi</span>
                      <textarea rows="2" [(ngModel)]="gradeForms[item.id].feedback"></textarea>
                    </label>
                    <label>
                      <span>Trạng thái</span>
                      <select [(ngModel)]="gradeForms[item.id].status">
                        <option value="Graded">Đã chấm</option>
                        <option value="NeedsRevision">Cần chỉnh sửa</option>
                        <option value="Accepted">Đã tiếp nhận</option>
                      </select>
                    </label>
                    <label class="portal-check">
                      <input type="checkbox" [(ngModel)]="gradeForms[item.id].resubmissionAllowed">
                      <span>Cho phép nộp lại</span>
                    </label>
                    <button class="primary-button" type="button" (click)="saveGrade(item)">
                      Lưu điểm
                    </button>
                  </div>
                </article>
              } @empty {
                <div class="portal-empty">
                  <span class="material-symbols-outlined">inbox</span>
                  <h3>Chưa có bài nộp</h3>
                </div>
              }
            </div>
          }
        </section>
      </div>
    }
  `
})
export class LecturerAssignmentsComponent implements OnInit {
  readonly classes = signal<LecturerClass[]>([]);
  readonly assignments = signal<AssignmentItem[]>([]);
  readonly submissions = signal<SubmissionItem[]>([]);
  readonly selectedAssignment = signal<AssignmentItem | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly submissionsLoading = signal(false);
  readonly formModal = signal(false);
  readonly submissionModal = signal(false);
  readonly classFilter = signal('');
  readonly search = signal('');
  editingId = '';
  form: AssignmentForm = this.emptyForm();
  gradeForms: Record<string, GradeForm> = {};

  readonly filtered = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return this.assignments().filter(item =>
      !keyword ||
      `${item.title} ${item.content} ${item.courseName} ${item.classSectionCode}`
        .toLocaleLowerCase('vi')
        .includes(keyword)
    );
  });

  constructor(
    readonly api: ApiService,
    private readonly toast: ToastService
  ) {}

  ngOnInit(): void {
    this.api.get<LecturerClass[]>('/lecturer/classes').subscribe({
      next: response => {
        this.classes.set(response.data ?? []);
        this.load();
      },
      error: () => this.load()
    });
  }

  changeClass(value: string): void {
    this.classFilter.set(value);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const params = this.classFilter()
      ? { classSectionId: this.classFilter() }
      : undefined;
    this.api.get<AssignmentItem[]>('/lecturer/assignments', params).subscribe({
      next: response => {
        this.assignments.set(response.data ?? []);
        this.loading.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải bài tập', 'error');
      }
    });
  }

  openCreate(): void {
    this.editingId = '';
    this.form = this.emptyForm();
    this.form.classSectionId = this.classFilter() || this.classes()[0]?.id || '';
    this.formModal.set(true);
  }

  openEdit(item: AssignmentItem): void {
    this.editingId = item.id;
    this.form = {
      classSectionId: item.classSectionId,
      title: item.title,
      content: item.content,
      attachmentUrl: item.attachmentUrl,
      maxScore: item.maxScore,
      openAt: this.toLocalInput(item.openAt),
      dueAt: this.toLocalInput(item.dueAt),
      allowLate: item.allowLate,
      latePenaltyPercent: item.latePenaltyPercent,
      cloCodesText: item.cloCodes.join(', '),
      linkedComponentId: item.linkedComponentId ?? '',
      status: item.status
    };
    this.formModal.set(true);
  }

  closeForm(): void {
    if (!this.saving()) this.formModal.set(false);
  }

  save(): void {
    if (!this.form.classSectionId || !this.form.title.trim() || !this.form.openAt || !this.form.dueAt) {
      this.toast.show('Vui lòng nhập đầy đủ lớp, tiêu đề và thời gian', 'error');
      return;
    }
    if (new Date(this.form.dueAt) <= new Date(this.form.openAt)) {
      this.toast.show('Hạn nộp phải sau thời điểm mở', 'error');
      return;
    }

    const body = {
      classSectionId: this.form.classSectionId,
      title: this.form.title.trim(),
      content: this.form.content,
      attachmentUrl: this.form.attachmentUrl,
      maxScore: Number(this.form.maxScore),
      openAt: new Date(this.form.openAt).toISOString(),
      dueAt: new Date(this.form.dueAt).toISOString(),
      allowLate: this.form.allowLate,
      latePenaltyPercent: Number(this.form.latePenaltyPercent),
      cloCodes: this.form.cloCodesText
        .split(',')
        .map(value => value.trim())
        .filter(Boolean),
      linkedComponentId: this.form.linkedComponentId.trim() || null,
      status: this.form.status
    };

    this.saving.set(true);
    const request = this.editingId
      ? this.api.put<AssignmentItem>(`/lecturer/assignments/${this.editingId}`, body)
      : this.api.post<AssignmentItem>('/lecturer/assignments', body);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.formModal.set(false);
        this.toast.show(this.editingId ? 'Đã cập nhật bài tập' : 'Đã tạo bài tập', 'success');
        this.load();
      },
      error: error => {
        this.saving.set(false);
        this.toast.show(error.error?.message || 'Lưu bài tập thất bại', 'error');
      }
    });
  }

  remove(item: AssignmentItem): void {
    if (!window.confirm(`Xóa bài tập “${item.title}”?`)) return;
    this.api.delete(`/lecturer/assignments/${item.id}`).subscribe({
      next: () => {
        this.toast.show('Đã xóa bài tập', 'success');
        this.load();
      },
      error: error => this.toast.show(error.error?.message || 'Xóa bài tập thất bại', 'error')
    });
  }

  openSubmissions(item: AssignmentItem): void {
    this.selectedAssignment.set(item);
    this.submissionModal.set(true);
    this.submissionsLoading.set(true);
    this.api.get<SubmissionItem[]>(`/lecturer/assignments/${item.id}/submissions`).subscribe({
      next: response => {
        const rows = response.data ?? [];
        this.submissions.set(rows);
        this.gradeForms = Object.fromEntries(rows.map(row => [
          row.id,
          {
            score: row.score ?? null,
            feedback: row.feedback ?? '',
            resubmissionAllowed: row.resubmissionAllowed,
            status: row.status === 'Graded' ? 'Graded' : 'Accepted'
          }
        ]));
        this.submissionsLoading.set(false);
      },
      error: error => {
        this.submissionsLoading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải bài nộp', 'error');
      }
    });
  }

  closeSubmissions(): void {
    this.submissionModal.set(false);
    this.selectedAssignment.set(null);
    this.submissions.set([]);
  }

  saveGrade(item: SubmissionItem): void {
    const form = this.gradeForms[item.id];
    const assignment = this.selectedAssignment();
    if (!form || form.score === null || !assignment) {
      this.toast.show('Vui lòng nhập điểm bài nộp', 'error');
      return;
    }
    if (form.score < 0 || form.score > assignment.maxScore) {
      this.toast.show(`Điểm phải từ 0 đến ${assignment.maxScore}`, 'error');
      return;
    }

    this.api.put(`/lecturer/submissions/${item.id}/grade`, {
      score: Number(form.score),
      feedback: form.feedback,
      resubmissionAllowed: form.resubmissionAllowed,
      status: form.status
    }).subscribe({
      next: () => {
        this.toast.show(`Đã lưu điểm cho ${item.studentName}`, 'success');
        this.openSubmissions(assignment);
      },
      error: error => this.toast.show(error.error?.message || 'Chấm bài thất bại', 'error')
    });
  }

  assignmentStatus(status: string): string {
    const values: Record<string, string> = {
      Draft: 'Bản nháp',
      Open: 'Đang mở',
      Closed: 'Đã đóng'
    };
    return values[status] ?? status;
  }

  private emptyForm(): AssignmentForm {
    const open = new Date();
    const due = new Date(open.getTime() + 7 * 24 * 60 * 60 * 1000);
    open.setMinutes(open.getMinutes() - open.getTimezoneOffset());
    due.setMinutes(due.getMinutes() - due.getTimezoneOffset());
    return {
      classSectionId: '',
      title: '',
      content: '',
      attachmentUrl: '',
      maxScore: 10,
      openAt: open.toISOString().slice(0, 16),
      dueAt: due.toISOString().slice(0, 16),
      allowLate: true,
      latePenaltyPercent: 10,
      cloCodesText: 'CLO1',
      linkedComponentId: 'BT',
      status: 'Open'
    };
  }

  private toLocalInput(value: string): string {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
