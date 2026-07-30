import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MaterialItem, StudentCourse } from '../../core/models/portal.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Tài liệu học tập"
      subtitle="Tài liệu đã được giảng viên công bố cho các lớp học phần của bạn."
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
        <input [ngModel]="search()" (ngModelChange)="search.set($event)" placeholder="Tìm tiêu đề, chương hoặc loại tài liệu">
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
              <span class="badge success">Đã công bố</span>
            </header>
            <small>{{ item.courseCode }} · {{ item.classSectionCode }}</small>
            <h3>{{ item.title }}</h3>
            <p>{{ item.description || 'Không có mô tả.' }}</p>
            <div class="portal-meta-list">
              <span><b>Danh mục:</b> {{ item.category || 'Tài liệu' }}</span>
              <span><b>Chương:</b> {{ item.chapter || '—' }}</span>
              <span><b>Loại:</b> {{ item.resourceType }}</span>
              <span><b>Công bố:</b> {{ item.visibleFrom | date:'dd/MM/yyyy HH:mm' }}</span>
            </div>
            <footer>
              <a
                class="primary-button full"
                [href]="api.assetUrl(item.resourceUrl)"
                target="_blank"
                rel="noopener">
                <span class="material-symbols-outlined">open_in_new</span>
                Mở tài liệu
              </a>
            </footer>
          </article>
        } @empty {
          <div class="portal-empty span-2">
            <span class="material-symbols-outlined">folder_open</span>
            <h3>Chưa có tài liệu được công bố</h3>
            <p>Hãy kiểm tra lại sau hoặc liên hệ giảng viên phụ trách.</p>
          </div>
        }
      </section>
    }
  `
})
export class StudentMaterialsComponent implements OnInit {
  readonly courses = signal<StudentCourse[]>([]);
  readonly materials = signal<MaterialItem[]>([]);
  readonly loading = signal(true);
  readonly classFilter = signal('');
  readonly search = signal('');

  readonly filtered = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return this.materials().filter(item =>
      !keyword ||
      `${item.title} ${item.description} ${item.category} ${item.chapter} ${item.courseName}`
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
    this.api.get<MaterialItem[]>('/student/materials', params).subscribe({
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

  resourceIcon(type: string): string {
    const value = type.toLowerCase();
    if (value.includes('video')) return 'play_circle';
    if (value.includes('pdf')) return 'picture_as_pdf';
    if (value.includes('slide')) return 'slideshow';
    if (value.includes('link')) return 'link';
    return 'description';
  }
}
