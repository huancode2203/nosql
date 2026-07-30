import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { PagedResult } from '../../core/models/api.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

@Component({ standalone: true, imports: [DatePipe, PageHeaderComponent], template: `
<app-page-header title="Yêu cầu mở lại điểm" subtitle="Duyệt hoặc từ chối yêu cầu chỉnh sửa bảng điểm đã công bố."></app-page-header><article class="panel table-panel"><div class="table-wrap"><table><thead><tr><th>Lớp học phần</th><th>Giảng viên</th><th>Lý do</th><th>Trạng thái</th><th>Ngày gửi</th><th>Thao tác</th></tr></thead><tbody>@for(item of items();track item['id']){<tr><td><b>{{item['classSectionCode']}}</b></td><td>{{item['lecturerCode']}}</td><td>{{item['reason']}}</td><td><span class="badge" [class.success]="item['status']==='Approved'" [class.danger]="item['status']==='Rejected'">{{item['status']}}</span></td><td>{{item['createdAt']|date:'dd/MM/yyyy HH:mm'}}</td><td>@if(item['status']==='Pending'){<button class="primary-button small-button" [disabled]="workingId()===item['id']" (click)="review(item,true)">Duyệt</button><button class="secondary-button small-button" [disabled]="workingId()===item['id']" (click)="review(item,false)">Từ chối</button>}</td></tr>}@empty{<tr><td colspan="6" class="empty">Không có yêu cầu</td></tr>}</tbody></table></div></article>
` })
export class ReopenRequestsComponent implements OnInit {
  items = signal<Record<string, any>[]>([]);
  workingId = signal('');

  constructor(
    private api: ApiService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.api.get<PagedResult<Record<string, any>>>(
      '/admin/grade-reopen-requests',
      { pageNumber: 1, pageSize: 100 }
    ).subscribe({
      next: response => this.items.set(response.data.items ?? []),
      error: error => {
        this.items.set([]);
        this.toast.show(
          error.error?.message
          || 'Không thể tải yêu cầu mở lại điểm',
          'error'
        );
      }
    });
  }

  review(item: Record<string, any>, approve: boolean) {
    const note = prompt(
      approve ? 'Ghi chú duyệt' : 'Lý do từ chối',
      ''
    );
    if (note === null || (!approve && !note.trim())) {
      return;
    }

    this.workingId.set(item['id']);
    this.api.put(
      `/admin/grade-reopen-requests/${item['id']}`,
      { approve, note: note.trim() }
    ).subscribe({
      next: () => {
        this.workingId.set('');
        this.toast.show('Đã xử lý yêu cầu', 'success');
        this.load();
      },
      error: error => {
        this.workingId.set('');
        this.toast.show(
          error.error?.message || 'Không thể xử lý',
          'error'
        );
      }
    });
  }
}
