import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LecturerClass, MaterialItem } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface MaterialForm {
  classSectionId: string;
  title: string;
  description: string;
  category: string;
  chapter: string;
  resourceType: string;
  resourceUrl: string;
  visibleFrom: string;
  visibleUntil: string;
  status: string;
}

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Tài liệu giảng dạy"
      subtitle="Tạo, cập nhật và công bố tài liệu theo từng lớp học phần."
      eyebrow="GIẢNG VIÊN">
      <button class="primary-button" type="button" (click)="openCreate()">
        <span class="material-symbols-outlined">add</span>
        Thêm tài liệu
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
        <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Tìm tài liệu">
      </label>
      <span class="portal-count">{{ filtered().length }} tài liệu</span>
    </section>

    @if (loading()) {
      <div class="portal-loading">Đang tải tài liệu…</div>
    } @else {
      <section class="portal-card-grid">
        @for (item of filtered(); track item.id) {
          <article class="portal-card">
            <header>
              <span class="portal-card-icon material-symbols-outlined">{{ resourceIcon(item.resourceType) }}</span>
              <span class="badge" [class.success]="item.status === 'Published'">
                {{ item.status === 'Published' ? 'Đã công bố' : item.status }}
              </span>
            </header>
            <small>{{ item.classSectionCode }} · {{ item.category || 'Tài liệu' }}</small>
            <h3>{{ item.title }}</h3>
            <p>{{ item.description || 'Không có mô tả.' }}</p>
            <div class="portal-meta-list">
              <span><b>Chương:</b> {{ item.chapter || '—' }}</span>
              <span><b>Hiển thị:</b> {{ item.visibleFrom | date:'dd/MM/yyyy HH:mm' }}</span>
              <span><b>Loại:</b> {{ item.resourceType }}</span>
            </div>
            <footer>
              <a
                class="text-button"
                [href]="api.assetUrl(item.resourceUrl)"
                target="_blank"
                rel="noopener">
                <span class="material-symbols-outlined">open_in_new</span>
                Mở
              </a>
              <span class="portal-actions">
                <button class="icon-button" type="button" title="Chỉnh sửa" (click)="openEdit(item)">
                  <span class="material-symbols-outlined">edit</span>
                </button>
                <button class="icon-button danger" type="button" title="Xóa" [disabled]="!!deletingId()" (click)="remove(item)">
                  <span class="material-symbols-outlined">delete</span>
                </button>
              </span>
            </footer>
          </article>
        } @empty {
          <div class="portal-empty span-2">
            <span class="material-symbols-outlined">folder_open</span>
            <h3>Chưa có tài liệu</h3>
            <p>Hãy tạo tài liệu đầu tiên cho lớp học phần.</p>
          </div>
        }
      </section>
    }

    @if (modal()) {
      <div class="modal-backdrop" (click)="closeModal()">
        <section class="modal portal-modal-wide" (click)="$event.stopPropagation()">
          <div class="modal-heading">
            <div>
              <h3>{{ editingId ? 'Cập nhật tài liệu' : 'Thêm tài liệu' }}</h3>
              <p>Thông tin hiển thị cho sinh viên của lớp học phần.</p>
            </div>
            <button class="icon-button" type="button" (click)="closeModal()">
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
            <label class="span-2">
              <span>Tiêu đề *</span>
              <input [(ngModel)]="form.title" placeholder="Ví dụ: Bài giảng chương 1">
            </label>
            <label class="span-2">
              <span>Mô tả</span>
              <textarea [(ngModel)]="form.description" rows="3"></textarea>
            </label>
            <label>
              <span>Danh mục</span>
              <input [(ngModel)]="form.category" placeholder="Bài giảng, đề cương…">
            </label>
            <label>
              <span>Chương</span>
              <input [(ngModel)]="form.chapter" placeholder="Chương 1">
            </label>
            <label>
              <span>Loại tài nguyên</span>
              <select [(ngModel)]="form.resourceType">
                <option>PDF</option>
                <option>Link</option>
                <option>Video</option>
                <option>Slide</option>
                <option>Document</option>
              </select>
            </label>
            <label>
              <span>Trạng thái</span>
              <select [(ngModel)]="form.status">
                <option value="Draft">Bản nháp</option>
                <option value="Published">Công bố</option>
                <option value="Hidden">Ẩn</option>
              </select>
            </label>
            <label class="span-2">
              <span>Đường dẫn tài liệu *</span>
              <input [(ngModel)]="form.resourceUrl" placeholder="https://… hoặc /uploads/…">
            </label>
            <label>
              <span>Hiển thị từ</span>
              <input type="datetime-local" [(ngModel)]="form.visibleFrom">
            </label>
            <label>
              <span>Ẩn sau thời điểm</span>
              <input type="datetime-local" [(ngModel)]="form.visibleUntil">
            </label>
          </div>

          <div class="modal-actions">
            <button class="secondary-button" type="button" (click)="closeModal()">Hủy</button>
            <button class="primary-button" type="button" [disabled]="saving()" (click)="save()">
              {{ saving() ? 'Đang lưu…' : 'Lưu tài liệu' }}
            </button>
          </div>
        </section>
      </div>
    }
  `
})
export class LecturerMaterialsComponent implements OnInit {
  readonly classes = signal<LecturerClass[]>([]);
  readonly materials = signal<MaterialItem[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly deletingId = signal('');
  readonly modal = signal(false);
  readonly classFilter = signal('');
  readonly search = signal('');
  editingId = '';
  form: MaterialForm = this.emptyForm();

  readonly filtered = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return this.materials().filter(item =>
      !keyword ||
      `${item.title} ${item.description} ${item.courseName} ${item.category}`
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
    this.api.get<MaterialItem[]>('/lecturer/materials', params).subscribe({
      next: response => {
        this.materials.set(response.data ?? []);
        this.loading.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.toast.show(error.error?.message || 'Không thể tải tài liệu', 'error');
      }
    });
  }

  openCreate(): void {
    this.editingId = '';
    this.form = this.emptyForm();
    this.form.classSectionId = this.classFilter() || this.classes()[0]?.id || '';
    this.modal.set(true);
  }

  openEdit(item: MaterialItem): void {
    this.editingId = item.id;
    this.form = {
      classSectionId: item.classSectionId,
      title: item.title,
      description: item.description,
      category: item.category,
      chapter: item.chapter,
      resourceType: item.resourceType,
      resourceUrl: item.resourceUrl,
      visibleFrom: this.toLocalInput(item.visibleFrom),
      visibleUntil: this.toLocalInput(item.visibleUntil),
      status: item.status
    };
    this.modal.set(true);
  }

  closeModal(): void {
    if (!this.saving()) this.modal.set(false);
  }

  save(): void {
    if (this.saving()) return;
    if (!this.form.classSectionId || !this.form.title.trim() || !this.form.resourceUrl.trim()) {
      this.toast.show('Vui lòng chọn lớp, nhập tiêu đề và đường dẫn tài liệu', 'error');
      return;
    }
    const visibleFrom = this.form.visibleFrom
      ? new Date(this.form.visibleFrom)
      : new Date();
    const visibleUntil = this.form.visibleUntil
      ? new Date(this.form.visibleUntil)
      : null;
    if (Number.isNaN(visibleFrom.getTime())) {
      this.toast.show('Thời điểm hiển thị không hợp lệ', 'error');
      return;
    }
    if (visibleUntil && (Number.isNaN(visibleUntil.getTime()) || visibleUntil <= visibleFrom)) {
      this.toast.show('Thời điểm ẩn phải sau thời điểm hiển thị', 'error');
      return;
    }

    const body = {
      ...this.form,
      title: this.form.title.trim(),
      description: this.form.description.trim(),
      category: this.form.category.trim(),
      chapter: this.form.chapter.trim(),
      resourceUrl: this.form.resourceUrl.trim(),
      visibleFrom: visibleFrom.toISOString(),
      visibleUntil: visibleUntil?.toISOString() ?? null
    };

    this.saving.set(true);
    const request = this.editingId
      ? this.api.put<MaterialItem>(`/lecturer/materials/${this.editingId}`, body)
      : this.api.post<MaterialItem>('/lecturer/materials', body);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.modal.set(false);
        this.toast.show(this.editingId ? 'Đã cập nhật tài liệu' : 'Đã tạo tài liệu', 'success');
        this.load();
      },
      error: error => {
        this.saving.set(false);
        this.toast.show(error.error?.message || 'Lưu tài liệu thất bại', 'error');
      }
    });
  }

  remove(item: MaterialItem): void {
    if (this.deletingId()) return;
    if (!window.confirm(`Xóa tài liệu “${item.title}”?`)) return;
    this.deletingId.set(item.id);
    this.api.delete(`/lecturer/materials/${item.id}`).subscribe({
      next: () => {
        this.deletingId.set('');
        this.toast.show('Đã xóa tài liệu', 'success');
        this.load();
      },
      error: error => {
        this.deletingId.set('');
        this.toast.show(error.error?.message || 'Xóa tài liệu thất bại', 'error');
      }
    });
  }

  resourceIcon(type: string): string {
    const value = type.toLowerCase();
    if (value.includes('video')) return 'play_circle';
    if (value.includes('pdf')) return 'picture_as_pdf';
    if (value.includes('slide')) return 'slideshow';
    if (value.includes('link')) return 'link';
    return 'description';
  }

  private emptyForm(): MaterialForm {
    const now = new Date();
    now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
    return {
      classSectionId: '',
      title: '',
      description: '',
      category: 'Bài giảng',
      chapter: '',
      resourceType: 'PDF',
      resourceUrl: '',
      visibleFrom: now.toISOString().slice(0, 16),
      visibleUntil: '',
      status: 'Published'
    };
  }

  private toLocalInput(value?: string): string {
    if (!value) return '';
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
