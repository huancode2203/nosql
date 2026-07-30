import {
  Component,
  OnInit,
  computed,
  signal
} from '@angular/core';
import {
  DatePipe,
  DecimalPipe
} from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { AuthService } from '../../core/services/auth.service';

interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

interface GradebookSummary {
  classSectionId: string;
  classSectionCode: string;
  courseCode: string;
  courseName: string;
  lecturerCode: string;
  lecturerName: string;
  academicYearName: string;
  semesterName: string;
  status: string;
  studentCount: number;
  completedStudentCount: number;
  invalidStudentCount: number;
  confirmationWarningCount: number;
  readyToPublish: boolean;
  updatedAt: string;
}

interface GradebookComponentItem {
  componentId: string;
  componentName: string;
  weight: number;
  maxScore: number;
  required: boolean;
  minimumScore?: number | null;
}

interface GradebookStudent {
  studentId: string;
  studentCode: string;
  fullName: string;
  scores: Record<string, number | null>;
  finalScore: number;
  letterGrade: string;
  passed: boolean;
  requiresConfirmation: boolean;
  validationMessages: string[];
}

interface GradebookDetail {
  classSectionId: string;
  classSectionCode: string;
  courseCode: string;
  courseName: string;
  lecturerCode: string;
  lecturerName: string;
  academicYearName: string;
  semesterCode: string;
  semesterName: string;
  status: string;
  updatedAt: string;
  components: GradebookComponentItem[];
  students: GradebookStudent[];
  completedStudentCount: number;
  invalidStudentCount: number;
  confirmationWarningCount: number;
  readyToPublish: boolean;
}

@Component({
  standalone: true,
  imports: [
    FormsModule,
    DatePipe,
    DecimalPipe,
    PageHeaderComponent
  ],
  templateUrl: './gradebook-review.component.html',
  styleUrl: './gradebook-review.component.scss'
})
export class GradebookReviewComponent implements OnInit {
  readonly items = signal<GradebookSummary[]>([]);
  readonly detail = signal<GradebookDetail | null>(null);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly working = signal(false);

  search = '';
  status = 'Submitted';
  pageNumber = 1;
  pageSize = 20;
  totalItems = 0;
  totalPages = 0;

  readonly readyCount = computed(
    () => this.items().filter(item => item.readyToPublish).length
  );

  readonly invalidCount = computed(
    () => this.items().reduce(
      (total, item) => total + item.invalidStudentCount,
      0
    )
  );

  readonly warningCount = computed(
    () => this.items().reduce(
      (total, item) => total + item.confirmationWarningCount,
      0
    )
  );

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService,
    readonly auth: AuthService
  ) {}

  ngOnInit() {
    this.load();
  }

  load(resetPage = false) {
    if (resetPage) {
      this.pageNumber = 1;
    }

    this.loading.set(true);

    this.api.get<PagedResult<GradebookSummary>>(
      '/admin/gradebooks',
      {
        status: this.status,
        search: this.search.trim(),
        pageNumber: this.pageNumber,
        pageSize: this.pageSize
      }
    ).subscribe({
      next: response => {
        const data =
          response.data as PagedResult<GradebookSummary>;

        this.items.set(data.items ?? []);
        this.totalItems = data.totalItems ?? 0;
        this.totalPages = data.totalPages ?? 0;
        this.loading.set(false);
      },
      error: error => {
        this.items.set([]);
        this.loading.set(false);
        this.toast.show(
          error.error?.message
          || 'Không thể tải danh sách bảng điểm.',
          'error'
        );
      }
    });
  }

  open(item: GradebookSummary) {
    this.detailLoading.set(true);
    this.detail.set(null);

    this.api.get<GradebookDetail>(
      `/admin/gradebooks/${item.classSectionId}`
    ).subscribe({
      next: response => {
        this.detail.set(response.data as GradebookDetail);
        this.detailLoading.set(false);
      },
      error: error => {
        this.detailLoading.set(false);
        this.toast.show(
          error.error?.message
          || 'Không thể tải chi tiết bảng điểm.',
          'error'
        );
      }
    });
  }

  closeDetail() {
    if (!this.working()) {
      this.detail.set(null);
    }
  }

  publish() {
    const current = this.detail();
    if (!current || !current.readyToPublish) {
      return;
    }

    const reason = window.prompt(
      'Nhập ghi chú công bố bảng điểm:',
      'Đã kiểm tra đầy đủ thành phần điểm và danh sách sinh viên.'
    );

    if (!reason?.trim()) {
      return;
    }

    if (!window.confirm(
      `Công bố bảng điểm ${current.classSectionCode} cho `
      + `${current.students.length} sinh viên?`
    )) {
      return;
    }

    this.working.set(true);

    this.api.post(
      `/admin/gradebooks/${current.classSectionId}/publish`,
      { reason: reason.trim() }
    ).subscribe({
      next: () => {
        this.toast.show(
          'Công bố bảng điểm thành công.',
          'success'
        );
        this.working.set(false);
        this.detail.set(null);
        this.load();
      },
      error: error => {
        this.working.set(false);
        this.toast.show(
          error.error?.message
          || 'Không thể công bố bảng điểm.',
          'error'
        );
      }
    });
  }

  returnToLecturer() {
    const current = this.detail();
    if (!current || current.status !== 'Submitted') {
      return;
    }

    const reason = window.prompt(
      'Nhập lý do trả lại cho giảng viên:'
    );

    if (!reason?.trim()) {
      return;
    }

    this.working.set(true);

    this.api.post(
      `/admin/gradebooks/${current.classSectionId}/return`,
      { reason: reason.trim() }
    ).subscribe({
      next: () => {
        this.toast.show(
          'Đã trả bảng điểm lại cho giảng viên.',
          'success'
        );
        this.working.set(false);
        this.detail.set(null);
        this.load();
      },
      error: error => {
        this.working.set(false);
        this.toast.show(
          error.error?.message
          || 'Không thể trả lại bảng điểm.',
          'error'
        );
      }
    });
  }

  lock() {
    const current = this.detail();
    if (!current || current.status !== 'Published') {
      return;
    }

    const reason = window.prompt(
      'Nhập lý do khóa bảng điểm:',
      'Đã hết thời hạn điều chỉnh điểm.'
    );
    if (!reason?.trim()) {
      return;
    }
    if (!window.confirm(
      `Khóa bảng điểm ${current.classSectionCode}? `
      + 'Giảng viên sẽ không thể chỉnh sửa cho đến khi có yêu cầu mở lại.'
    )) {
      return;
    }

    this.working.set(true);
    this.api.post(
      `/admin/gradebooks/${current.classSectionId}/lock`,
      { reason: reason.trim() }
    ).subscribe({
      next: () => {
        this.toast.show('Khóa bảng điểm thành công.', 'success');
        this.working.set(false);
        this.detail.set(null);
        this.load();
      },
      error: error => {
        this.working.set(false);
        this.toast.show(
          error.error?.message || 'Không thể khóa bảng điểm.',
          'error'
        );
      }
    });
  }

  previousPage() {
    if (this.pageNumber > 1) {
      this.pageNumber--;
      this.load();
    }
  }

  nextPage() {
    if (this.pageNumber < this.totalPages) {
      this.pageNumber++;
      this.load();
    }
  }

  statusLabel(status: string) {
    const labels: Record<string, string> = {
      Draft: 'Bản nháp',
      Submitted: 'Chờ duyệt',
      Published: 'Đã công bố',
      Locked: 'Đã khóa',
      Reopened: 'Đã mở lại'
    };

    return labels[status] ?? status;
  }

  score(
    student: GradebookStudent,
    componentId: string
  ) {
    return student.scores[componentId];
  }
}
