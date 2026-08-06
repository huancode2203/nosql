import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PagedResult } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';
import { ToastService } from '../../core/services/toast.service';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface LookupOption {
  id: string;
  code: string;
  name: string;
}

interface NotificationOptions {
  faculties: LookupOption[];
  classSections: LookupOption[];
}

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Quản lý thông báo"
      subtitle="Gửi thông báo theo vai trò, lớp, khoa hoặc người dùng cụ thể.">
      <button class="primary-button" [disabled]="deletedOnly() || saving()" (click)="open()">
        <span class="material-symbols-outlined">add_alert</span> Tạo thông báo
      </button>
    </app-page-header>

    <article class="panel table-panel">
      <div class="table-toolbar">
        <div class="search-box">
          <span class="material-symbols-outlined">search</span>
          <input [(ngModel)]="search" (keyup.enter)="load()" placeholder="Tìm theo tiêu đề..."/>
          <button (click)="load()">Tìm</button>
        </div>
        <div class="inline-actions">
          <button class="secondary-button" [class.danger]="deletedOnly()" (click)="toggleTrash()">
            <span class="material-symbols-outlined">{{ deletedOnly() ? 'arrow_back' : 'delete_sweep' }}</span>
            {{ deletedOnly() ? 'Quay lại danh sách' : 'Thùng rác' }}
          </button>
        </div>
      </div>
      <div class="table-wrap">
        <table>
          <thead><tr><th>Tiêu đề</th><th>Loại</th><th>Ưu tiên</th><th>Đối tượng</th><th>Trạng thái</th><th>Ngày tạo</th><th></th></tr></thead>
          <tbody>
            @for (item of items(); track item['id']) {
              <tr>
                <td><b>{{item['title']}}</b><small class="table-subtext">{{item['content']}}</small></td>
                <td>{{item['type']}}</td>
                <td><span class="badge" [class.danger]="item['priority'] === 'High'">{{item['priority']}}</span></td>
                <td>{{audienceLabel(item)}}</td>
                <td><span class="badge" [class.success]="item['status'] === 'Sent'">{{item['status']}}</span></td>
                <td>{{item['createdAt'] | date:'dd/MM/yyyy HH:mm'}}</td>
                <td>
                  @if (deletedOnly()) {
                    <button class="secondary-button small-button" [disabled]="!!workingId()" (click)="restore(item)">
                      <span class="material-symbols-outlined">settings_backup_restore</span> Khôi phục
                    </button>
                  } @else {
                    <button class="icon-button" [disabled]="!!workingId()" (click)="open(item)"><span class="material-symbols-outlined">edit</span></button>
                    <button class="icon-button danger" [disabled]="!!workingId()" (click)="remove(item)"><span class="material-symbols-outlined">delete</span></button>
                  }
                </td>
              </tr>
            } @empty {
              <tr><td colspan="7" class="empty">Chưa có thông báo</td></tr>
            }
          </tbody>
        </table>
      </div>
    </article>

    @if (modal()) {
      <div class="modal-backdrop" (click)="!saving() && modal.set(false)">
        <form class="modal" (click)="$event.stopPropagation()" (submit)="$event.preventDefault(); save()">
          <div class="modal-heading">
            <div><h3>{{form.id ? 'Cập nhật' : 'Tạo'}} thông báo</h3><p>Thông báo được hiển thị theo phạm vi người nhận.</p></div>
            <button type="button" class="icon-button" [disabled]="saving()" (click)="modal.set(false)"><span class="material-symbols-outlined">close</span></button>
          </div>
          <div class="form-grid">
            <label class="full-row">Tiêu đề<input [(ngModel)]="form.title" name="title" required/></label>
            <label class="full-row">Nội dung<textarea [(ngModel)]="form.content" name="content" rows="5" required></textarea></label>
            <label>Loại<select [(ngModel)]="form.type" name="type"><option>General</option><option>Academic</option><option>Grade</option><option>Emergency</option></select></label>
            <label>Ưu tiên<select [(ngModel)]="form.priority" name="priority"><option>Low</option><option>Normal</option><option>High</option></select></label>
            <label>Đối tượng
              <select [(ngModel)]="form.audienceType" name="audienceType" (ngModelChange)="audienceId = ''">
                <option value="All">Tất cả</option>
                <option value="Student">Sinh viên</option>
                <option value="Lecturer">Giảng viên</option>
                <option value="Admin">Quản trị viên</option>
                <option value="Faculty">Theo khoa</option>
                <option value="ClassSection">Theo lớp học phần</option>
                <option value="SpecificUsers">Người dùng cụ thể</option>
              </select>
            </label>
            <label>Trạng thái<select [(ngModel)]="form.status" name="status"><option>Draft</option><option>Sent</option></select></label>
            @if (form.audienceType === 'Faculty') {
              <label class="full-row">Khoa
                <select [(ngModel)]="audienceId" name="audienceId" required>
                  <option value="">Chọn khoa</option>
                  @for (item of faculties(); track item.id) {
                    <option [value]="item.id">{{item.code}} - {{item.name}}</option>
                  }
                </select>
              </label>
            }
            @if (form.audienceType === 'ClassSection') {
              <label class="full-row">Lớp học phần
                <select [(ngModel)]="audienceId" name="audienceId" required>
                  <option value="">Chọn lớp học phần</option>
                  @for (item of classSections(); track item.id) {
                    <option [value]="item.id">{{item.code}} - {{item.name}}</option>
                  }
                </select>
              </label>
            }
            @if (form.audienceType === 'SpecificUsers') {
              <label class="full-row">ID người nhận, cách nhau bằng dấu phẩy
                <input [(ngModel)]="recipients" name="recipients" required placeholder="Nhập ID các tài khoản"/>
              </label>
            }
            <label>Hiển thị từ<input type="datetime-local" [(ngModel)]="form.displayFrom" name="displayFrom" required/></label>
            <label>Hết hạn<input type="datetime-local" [(ngModel)]="form.expiresAt" name="expiresAt"/></label>
          </div>
          <div class="modal-actions">
            <button type="button" class="secondary-button" [disabled]="saving()" (click)="modal.set(false)">Hủy</button>
            <button class="primary-button" type="submit" [disabled]="saving()">{{ saving() ? 'Đang lưu...' : 'Lưu thông báo' }}</button>
          </div>
        </form>
      </div>
    }
  `
})
export class AdminNotificationsComponent implements OnInit {
  items = signal<Record<string, any>[]>([]);
  faculties = signal<LookupOption[]>([]);
  classSections = signal<LookupOption[]>([]);
  modal = signal(false);
  saving = signal(false);
  workingId = signal('');
  deletedOnly = signal(false);
  search = '';
  recipients = '';
  audienceId = '';
  form: any = this.empty();

  constructor(private api: ApiService, private toast: ToastService) {}

  ngOnInit() {
    this.load();
    this.loadAudienceOptions();
  }

  load() {
    this.api.get<PagedResult<Record<string, any>>>('/admin/notifications', {
      pageNumber: 1,
      pageSize: 100,
      search: this.search,
      deletedOnly: this.deletedOnly()
    }).subscribe({
      next: response => this.items.set(response.data.items ?? []),
      error: error => {
        this.items.set([]);
        this.toast.show(
          error.error?.message || 'Không thể tải danh sách thông báo',
          'error'
        );
      }
    });
  }

  open(item?: Record<string, any>) {
    if (this.saving() || this.deletedOnly()) return;
    this.form = item
      ? { ...item, displayFrom: this.localDate(item['displayFrom']), expiresAt: this.localDate(item['expiresAt']) }
      : this.empty();
    this.recipients = (item?.['recipientIds'] || []).join(',');
    this.audienceId = item?.['audienceId'] || '';
    this.modal.set(true);
  }

  save() {
    if (this.saving()) return;
    const title = String(this.form.title ?? '').trim();
    const content = String(this.form.content ?? '').trim();
    if (!title || !content) {
      this.toast.show('Vui lòng nhập tiêu đề và nội dung thông báo', 'error');
      return;
    }
    if ((this.form.audienceType === 'Faculty' || this.form.audienceType === 'ClassSection') && !this.audienceId) {
      this.toast.show('Vui lòng chọn phạm vi người nhận', 'error');
      return;
    }
    const recipientIds = this.recipients
      .split(',')
      .map(value => value.trim())
      .filter(Boolean);
    if (this.form.audienceType === 'SpecificUsers') {
      if (recipientIds.length === 0) {
        this.toast.show('Vui lòng nhập ít nhất một ID người nhận', 'error');
        return;
      }
      if (recipientIds.some(value => !/^[a-f\d]{24}$/i.test(value))) {
        this.toast.show('ID người nhận phải là ObjectId MongoDB gồm 24 ký tự', 'error');
        return;
      }
    }
    const displayFrom = new Date(this.form.displayFrom);
    const expiresAt = this.form.expiresAt
      ? new Date(this.form.expiresAt)
      : null;
    if (Number.isNaN(displayFrom.getTime())) {
      this.toast.show('Thời điểm bắt đầu hiển thị không hợp lệ', 'error');
      return;
    }
    if (expiresAt && (Number.isNaN(expiresAt.getTime()) || expiresAt <= displayFrom)) {
      this.toast.show('Thời điểm hết hạn phải sau thời điểm hiển thị', 'error');
      return;
    }

    const id = this.form.id;
    const body = {
      title,
      content,
      type: this.form.type,
      priority: this.form.priority,
      audienceType: this.form.audienceType,
      status: this.form.status,
      audienceId: this.audienceId || null,
      recipientIds: this.form.audienceType === 'SpecificUsers'
        ? recipientIds
        : [],
      displayFrom: displayFrom.toISOString(),
      expiresAt: expiresAt?.toISOString() ?? null
    };

    this.saving.set(true);
    const call = id
      ? this.api.put(`/admin/notifications/${id}`, body)
      : this.api.post('/admin/notifications', body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.show('Đã lưu thông báo', 'success');
        this.modal.set(false);
        this.load();
      },
      error: error => {
        this.saving.set(false);
        this.toast.show(error.error?.message || 'Không thể lưu thông báo', 'error');
      }
    });
  }

  remove(item: Record<string, any>) {
    if (this.workingId()) return;
    if (!confirm('Xóa thông báo này?')) return;
    this.workingId.set(item['id']);
    this.api.delete(`/admin/notifications/${item['id']}`).subscribe({
      next: () => {
        this.workingId.set('');
        this.toast.show('Đã xóa thông báo', 'success');
        this.load();
      },
      error: error => {
        this.workingId.set('');
        this.toast.show(
          error.error?.message || 'Không thể xóa thông báo',
          'error'
        );
      }
    });
  }

  restore(item: Record<string, any>) {
    if (this.workingId()) return;
    if (!confirm('Khôi phục thông báo này?')) return;
    this.workingId.set(item['id']);
    this.api.post(`/admin/notifications/${item['id']}/restore`, {}).subscribe({
      next: () => {
        this.workingId.set('');
        this.toast.show('Đã khôi phục thông báo', 'success');
        this.load();
      },
      error: error => {
        this.workingId.set('');
        this.toast.show(error.error?.message || 'Không thể khôi phục thông báo', 'error');
      }
    });
  }

  toggleTrash() {
    this.deletedOnly.update(value => !value);
    this.load();
  }

  audienceLabel(item: Record<string, any>) {
    if (item['audienceType'] === 'Faculty') return `Khoa: ${item['audienceName'] || item['audienceId']}`;
    if (item['audienceType'] === 'ClassSection') return `Lớp: ${item['audienceName'] || item['audienceId']}`;
    if (item['audienceType'] === 'SpecificUsers') return `${(item['recipientIds'] || []).length} người dùng`;
    return item['audienceType'];
  }

  private loadAudienceOptions() {
    this.api.get<NotificationOptions>(
      '/admin/notification-options'
    ).subscribe({
      next: response => {
        this.faculties.set(response.data.faculties ?? []);
        this.classSections.set(response.data.classSections ?? []);
      },
      error: error => this.toast.show(
        error.error?.message
        || 'Không thể tải danh sách khoa và lớp học phần',
        'error'
      )
    });
  }

  private empty() {
    return {
      title: '',
      content: '',
      type: 'Academic',
      priority: 'Normal',
      audienceType: 'All',
      status: 'Sent',
      displayFrom: this.localDate(new Date().toISOString()),
      expiresAt: ''
    };
  }

  private localDate(value?: string) {
    if (!value) return '';
    const date = new Date(value);
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }
}
