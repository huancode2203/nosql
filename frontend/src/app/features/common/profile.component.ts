import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { UserProfile } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface ProfileForm {
  phone: string;
  address: string;
  dateOfBirth: string;
  gender: string;
  secondaryEmail: string;
}

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Hồ sơ cá nhân"
      subtitle="Cập nhật thông tin liên hệ; mã số, họ tên và ảnh đại diện do Admin quản lý.">
      <button
        class="primary-button"
        (click)="save()"
        [disabled]="saving()">
        <span class="material-symbols-outlined">save</span>
        Lưu thay đổi
      </button>
    </app-page-header>

    @if (profile(); as item) {
      <div class="profile-grid">
        <article class="panel profile-card">
          <div class="profile-avatar">
            @if (item.avatarUrl) {
              <img [src]="api.assetUrl(item.avatarUrl)" alt="Ảnh đại diện">
            } @else {
              {{ item.fullName.charAt(0) }}
            }
          </div>
          <h2>{{ item.fullName }}</h2>
          <span class="badge success">{{ item.role }} · {{ item.status }}</span>
          <p class="field-help">
            Ảnh đại diện chỉ có thể được tải lên hoặc xóa tại màn hình
            Quản lý tài khoản của Admin.
          </p>
          <div class="detail-list">
            <div><span>Tên đăng nhập</span><b>{{ item.username }}</b></div>
            <div><span>Email</span><b>{{ item.email }}</b></div>
            <div>
              <span>Mã số</span>
              <b>{{ item.studentCode || item.lecturerCode || '-' }}</b>
            </div>
            <div><span>Khoa</span><b>{{ item.facultyName || '-' }}</b></div>
            <div>
              <span>Chương trình</span>
              <b>{{ item.programName || '-' }}</b>
            </div>
            <div>
              <span>Đăng nhập gần nhất</span>
              <b>{{ item.lastLoginAt | date:'dd/MM/yyyy HH:mm' }}</b>
            </div>
          </div>
        </article>

        <article class="panel">
          <div class="panel-heading">
            <div>
              <h3>Thông tin cá nhân được phép cập nhật</h3>
              <p>Dữ liệu được đồng bộ vào hồ sơ sinh viên hoặc giảng viên.</p>
            </div>
          </div>
          <div class="form-grid">
            <label>
              Số điện thoại
              <input [(ngModel)]="form.phone">
            </label>

            @if (item.role === 'Student') {
              <label>
                Giới tính
                <select [(ngModel)]="form.gender">
                  <option value="">-- Chọn --</option>
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                  <option value="Khác">Khác</option>
                </select>
              </label>
              <label>
                Ngày sinh
                <input type="date" [(ngModel)]="form.dateOfBirth">
              </label>
              <label class="full-row">
                Địa chỉ
                <input [(ngModel)]="form.address">
              </label>
            }

            <label class="full-row">
              Email phụ
              <input
                type="email"
                [(ngModel)]="form.secondaryEmail"
                placeholder="Email liên hệ khác email đăng nhập">
            </label>
          </div>
        </article>
      </div>
    } @else {
      <div class="skeleton-grid">
        <div class="skeleton"></div>
        <div class="skeleton"></div>
      </div>
    }
  `
})
export class ProfileComponent implements OnInit {
  readonly profile = signal<UserProfile | null>(null);
  readonly saving = signal(false);
  form: ProfileForm = {
    phone: '',
    address: '',
    dateOfBirth: '',
    gender: '',
    secondaryEmail: ''
  };

  constructor(
    readonly api: ApiService,
    private readonly toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.get<UserProfile>('/profile').subscribe(response => {
      this.profile.set(response.data);
      this.form = {
        phone: response.data.phone || '',
        address: response.data.address || '',
        dateOfBirth: response.data.dateOfBirth?.slice(0, 10) || '',
        gender: response.data.gender || '',
        secondaryEmail: response.data.secondaryEmail || ''
      };
    });
  }

  save() {
    this.saving.set(true);
    this.api.put<UserProfile>('/profile', {
      ...this.form,
      dateOfBirth: this.form.dateOfBirth
        ? new Date(this.form.dateOfBirth).toISOString()
        : null
    }).subscribe({
      next: response => {
        this.profile.set(response.data);
        this.saving.set(false);
        this.toast.show('Cập nhật hồ sơ thành công', 'success');
      },
      error: error => {
        this.saving.set(false);
        this.toast.show(
          error.error?.message || 'Không thể cập nhật hồ sơ',
          'error'
        );
      }
    });
  }
}
