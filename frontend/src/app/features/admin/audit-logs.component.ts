import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { PagedResult } from '../../core/models/api.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

@Component({
  standalone: true,
  imports: [DatePipe, FormsModule, PageHeaderComponent],
  template: `
    <app-page-header
      title="Nhật ký hệ thống"
      subtitle="Theo dõi đăng nhập, thay đổi dữ liệu, nhập điểm, backup và restore.">
    </app-page-header>

    <article class="panel table-panel">
      <div class="table-toolbar audit-filter-grid">
        <div class="search-box">
          <span class="material-symbols-outlined">search</span>
          <input
            [(ngModel)]="search"
            (keyup.enter)="applyFilters()"
            placeholder="Người dùng, hành động, đối tượng..."/>
        </div>
        <select [(ngModel)]="role">
          <option value="">Tất cả vai trò</option>
          <option>Admin</option>
          <option>Lecturer</option>
          <option>Student</option>
          <option>System</option>
        </select>
        <input [(ngModel)]="action" placeholder="Hành động"/>
        <select [(ngModel)]="result">
          <option value="">Tất cả kết quả</option>
          <option>Success</option>
          <option>Failed</option>
        </select>
        <label>Từ ngày<input type="date" [(ngModel)]="fromDate"/></label>
        <label>Đến ngày<input type="date" [(ngModel)]="toDate"/></label>
        <button class="primary-button" (click)="applyFilters()">Lọc</button>
        <button class="secondary-button" (click)="clearFilters()">Xóa lọc</button>
      </div>

      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Thời gian</th>
              <th>Người thực hiện</th>
              <th>Vai trò</th>
              <th>Hành động</th>
              <th>Đối tượng</th>
              <th>Kết quả</th>
              <th>IP</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of items(); track item['id']) {
              <tr>
                <td>{{item['createdAt'] | date:'dd/MM/yyyy HH:mm:ss'}}</td>
                <td>{{item['userName'] || item['userId']}}</td>
                <td><span class="badge">{{item['role']}}</span></td>
                <td><b>{{item['action']}}</b></td>
                <td>{{item['entity']}} {{item['entityId'] || ''}}</td>
                <td>
                  <span
                    class="badge"
                    [class.success]="item['result'] === 'Success'"
                    [class.danger]="item['result'] !== 'Success'">
                    {{item['result']}}
                  </span>
                </td>
                <td>{{item['ipAddress']}}</td>
                <td>
                  <button class="icon-button" title="Xem chi tiết" (click)="selected.set(item)">
                    <span class="material-symbols-outlined">visibility</span>
                  </button>
                </td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="empty">Chưa có nhật ký</td></tr>
            }
          </tbody>
        </table>
      </div>

      <footer class="table-footer">
        <span>{{total}} bản ghi</span>
        <div>
          <button [disabled]="page === 1" (click)="previous()">Trước</button>
          <b>{{page}}</b>
          <button [disabled]="page * pageSize >= total" (click)="next()">Sau</button>
        </div>
      </footer>
    </article>

    @if (selected(); as item) {
      <div class="modal-backdrop" (click)="selected.set(null)">
        <section class="modal audit-detail" (click)="$event.stopPropagation()">
          <div class="modal-heading">
            <div>
              <h3>Chi tiết nhật ký</h3>
              <p>{{item['action']}} · {{item['entity']}} {{item['entityId'] || ''}}</p>
            </div>
            <button class="icon-button" (click)="selected.set(null)">
              <span class="material-symbols-outlined">close</span>
            </button>
          </div>
          <div class="audit-meta">
            <span><b>Người thực hiện:</b> {{item['userName'] || item['userId']}}</span>
            <span><b>Vai trò:</b> {{item['role']}}</span>
            <span><b>Thời gian:</b> {{item['createdAt'] | date:'dd/MM/yyyy HH:mm:ss'}}</span>
            <span><b>Kết quả:</b> {{item['result']}}</span>
            <span><b>IP:</b> {{item['ipAddress'] || '—'}}</span>
            <span><b>Trình duyệt:</b> {{item['userAgent'] || '—'}}</span>
          </div>
          @if (item['note']) {
            <p class="audit-note"><b>Ghi chú:</b> {{item['note']}}</p>
          }
          <div class="audit-json-grid">
            <div><h4>Trước thay đổi</h4><pre>{{pretty(item['before'])}}</pre></div>
            <div><h4>Sau thay đổi</h4><pre>{{pretty(item['after'])}}</pre></div>
          </div>
        </section>
      </div>
    }
  `
})
export class AuditLogsComponent implements OnInit {
  items = signal<Record<string, any>[]>([]);
  selected = signal<Record<string, any> | null>(null);
  search = '';
  role = '';
  action = '';
  result = '';
  fromDate = '';
  toDate = '';
  page = 1;
  pageSize = 20;
  total = 0;

  constructor(
    private api: ApiService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.get<PagedResult<Record<string, any>>>('/admin/audit-logs', {
      search: this.search,
      role: this.role,
      action: this.action,
      result: this.result,
      from: this.fromDate ? new Date(`${this.fromDate}T00:00:00`).toISOString() : '',
      to: this.toDate ? new Date(`${this.toDate}T23:59:59`).toISOString() : '',
      pageNumber: this.page,
      pageSize: this.pageSize
    }).subscribe({
      next: response => {
        this.items.set(response.data.items ?? []);
        this.total = response.data.totalItems ?? 0;
      },
      error: error => {
        this.items.set([]);
        this.total = 0;
        this.toast.show(
          error.error?.message || 'Không thể tải nhật ký hệ thống',
          'error'
        );
      }
    });
  }

  applyFilters() {
    this.page = 1;
    this.load();
  }

  clearFilters() {
    this.search = '';
    this.role = '';
    this.action = '';
    this.result = '';
    this.fromDate = '';
    this.toDate = '';
    this.applyFilters();
  }

  previous() {
    if (this.page > 1) {
      this.page -= 1;
      this.load();
    }
  }

  next() {
    if (this.page * this.pageSize < this.total) {
      this.page += 1;
      this.load();
    }
  }

  pretty(value: unknown) {
    return value == null ? 'Không có dữ liệu' : JSON.stringify(value, null, 2);
  }
}
