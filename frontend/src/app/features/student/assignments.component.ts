import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AssignmentItem, StudentCourse } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Bài tập"
      subtitle="Theo dõi hạn nộp, gửi bài và xem kết quả chấm của các môn đang học."
      eyebrow="SINH VIÊN">
    </app-page-header>

    <section class="portal-toolbar">
      <label>
        <span>Môn học</span>
        <select [ngModel]="classFilter()" (ngModelChange)="changeClass($event)">
          <option value="">Tất cả môn đang học</option>
          @for (item of courses(); track item.classSectionId) {
            <option [value]="item.classSectionId">{{ item.courseCode }} · {{ item.courseName }}</option>
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
              <span class="badge" [class.success]="item.studentSubmissionStatus" [class.danger]="isOverdue(item) && !item.studentSubmissionStatus">
                {{ submissionStatus(item) }}
              </span>
            </header>
            <small>{{ item.courseCode }} · {{ item.classSectionCode }}</small>
            <h3>{{ item.title }}</h3>
            <p>{{ item.content || 'Không có mô tả.' }}</p>
            <div class="portal-meta-list">
              <span><b>Mở:</b> {{ item.openAt | date:'dd/MM/yyyy HH:mm' }}</span>
              <span><b>Hạn nộp:</b> {{ item.dueAt | date:'dd/MM/yyyy HH:mm' }}</span>
              <span><b>Điểm tối đa:</b> {{ item.maxScore }}</span>
              <span><b>Nộp trễ:</b> {{ item.allowLate ? 'Cho phép' : 'Không' }}</span>
              <span><b>CLO:</b> {{ item.cloCodes.join(', ') || '—' }}</span>
            </div>

            @if (item.attachmentUrl) {
              <a class="portal-attachment" [href]="api.assetUrl(item.attachmentUrl)" target="_blank" rel="noopener">
                <span class="material-symbols-outlined">attach_file</span>
                Tệp yêu cầu bài tập
              </a>
            }

            @if (item.studentSubmissionStatus) {
              <div class="portal-result-box">
                <span>Trạng thái <b>{{ item.studentSubmissionStatus }}</b></span>
                <span>Điểm <b>{{ item.studentScore ?? 'Chưa chấm' }}/{{ item.maxScore }}</b></span>
                @if (item.studentFeedback) {
                  <p><b>Phản hồi:</b> {{ item.studentFeedback }}</p>
                }
              </div>
            }

            <footer>
              <button
                class="primary-button full"
                type="button"
                [disabled]="!canSubmit(item)"
                (click)="openSubmit(item)">
                <span class="material-symbols-outlined">upload_file</span>
                {{ item.studentSubmissionStatus ? 'Nộp lại bài' : 'Nộp bài' }}
              </button>
            </footer>
          </article>
        } @empty {
          <div class="portal-empty span-2">
            <span class="material-symbols-outlined">task_alt</span>
            <h3>Chưa có bài tập</h3>
            <p>Không có bài tập phù hợp với bộ lọc hiện tại.</p>
          </div>
        }
      </section>
    }

    @if (submitModal()) {
      <div class="modal-backdrop" (click)="closeSubmit()">
        <section class="modal portal-modal-wide" (click)="$event.stopPropagation()">
          <div class="modal-heading">
            <div>
              <h3>Nộp bài · {{ selected()?.title }}</h3>
              <p>Tối đa 20 MB mỗi tệp; hỗ trợ tài liệu, bảng tính, ZIP và hình ảnh.</p>
            </div>
            <button class="icon-button" type="button" (click)="closeSubmit()">
              <span class="material-symbols-outlined">close</span>
            </button>
          </div>

          <label class="portal-field">
            <span>Nội dung bài làm</span>
            <textarea rows="7" [(ngModel)]="textContent" placeholder="Nhập nội dung hoặc ghi chú cho giảng viên"></textarea>
          </label>

          <label class="portal-dropzone">
            <input type="file" multiple (change)="chooseFiles($event)">
            <span class="material-symbols-outlined">cloud_upload</span>
            <strong>Chọn tệp bài nộp</strong>
            <small>Có thể chọn nhiều tệp cùng lúc</small>
          </label>

          @if (selectedFiles.length) {
            <div class="portal-file-list selected">
              @for (file of selectedFiles; track file.name) {
                <span>
                  <span class="material-symbols-outlined">description</span>
                  {{ file.name }} · {{ file.size / 1024 / 1024 | number:'1.1-2' }} MB
                </span>
              }
            </div>
          }

          <div class="modal-actions">
            <button class="secondary-button" type="button" (click)="closeSubmit()">Hủy</button>
            <button class="primary-button" type="button" [disabled]="submitting()" (click)="submit()">
              {{ submitting() ? 'Đang tải lên…' : 'Xác nhận nộp bài' }}
            </button>
          </div>
        </section>
      </div>
    }
  `
})
export class StudentAssignmentsComponent implements OnInit {
  readonly courses = signal<StudentCourse[]>([]);
  readonly assignments = signal<AssignmentItem[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly submitModal = signal(false);
  readonly selected = signal<AssignmentItem | null>(null);
  readonly classFilter = signal('');
  readonly search = signal('');
  textContent = '';
  selectedFiles: File[] = [];

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
    this.api.get<StudentCourse[]>('/student/current-courses').subscribe({
      next: response => {
        this.courses.set(response.data ?? []);
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
    this.api.get<AssignmentItem[]>('/student/assignments', params).subscribe({
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

  canSubmit(item: AssignmentItem): boolean {
    const now = Date.now();
    const open = new Date(item.openAt).getTime();
    const due = new Date(item.dueAt).getTime();
    if (item.status !== 'Open' || now < open) return false;
    return now <= due || item.allowLate;
  }

  isOverdue(item: AssignmentItem): boolean {
    return Date.now() > new Date(item.dueAt).getTime();
  }

  submissionStatus(item: AssignmentItem): string {
    if (item.studentSubmissionStatus) {
      const values: Record<string, string> = {
        Submitted: 'Đã nộp',
        Late: 'Đã nộp trễ',
        Graded: 'Đã chấm',
        Accepted: 'Đã tiếp nhận',
        NeedsRevision: 'Cần chỉnh sửa'
      };
      return values[item.studentSubmissionStatus] ?? item.studentSubmissionStatus;
    }
    if (Date.now() < new Date(item.openAt).getTime()) return 'Chưa mở';
    if (this.isOverdue(item)) return item.allowLate ? 'Quá hạn · được nộp trễ' : 'Đã quá hạn';
    return 'Chưa nộp';
  }

  openSubmit(item: AssignmentItem): void {
    if (!this.canSubmit(item)) return;
    this.selected.set(item);
    this.textContent = '';
    this.selectedFiles = [];
    this.submitModal.set(true);
  }

  closeSubmit(): void {
    if (!this.submitting()) {
      this.submitModal.set(false);
      this.selected.set(null);
    }
  }

  chooseFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFiles = Array.from(input.files ?? []);
    const oversized = this.selectedFiles.find(file => file.size > 20 * 1024 * 1024);
    if (oversized) {
      this.toast.show(`Tệp ${oversized.name} vượt quá 20 MB`, 'error');
      this.selectedFiles = this.selectedFiles.filter(file => file.size <= 20 * 1024 * 1024);
    }
  }

  submit(): void {
    const assignment = this.selected();
    if (!assignment) return;
    if (!this.textContent.trim() && this.selectedFiles.length === 0) {
      this.toast.show('Hãy nhập nội dung hoặc chọn ít nhất một tệp', 'error');
      return;
    }

    const form = new FormData();
    form.append('textContent', this.textContent.trim());
    this.selectedFiles.forEach(file => form.append('files', file, file.name));

    this.submitting.set(true);
    this.api.postForm(`/student/assignments/${assignment.id}/submit`, form).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitModal.set(false);
        this.toast.show('Nộp bài thành công', 'success');
        this.load();
      },
      error: error => {
        this.submitting.set(false);
        this.toast.show(error.error?.message || 'Nộp bài thất bại', 'error');
      }
    });
  }
}
