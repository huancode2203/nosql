import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

interface BackupItem { id: string; fileName: string; sizeBytes: number; status: string; type?: string; performedBy: string; createdAt: string; }

@Component({
  standalone: true,
  imports: [PageHeaderComponent, DatePipe],
  template: `
    <app-page-header title="Sao lưu và phục hồi" subtitle="Quản lý bản sao lưu MongoDB bằng mongodump và mongorestore.">
      <input #backupFile type="file" accept=".zip" hidden (change)="upload($any($event.target).files?.[0]); backupFile.value=''"/>
      <button class="secondary-button" (click)="backupFile.click()" [disabled]="working()"><span class="material-symbols-outlined">upload</span>Tải ZIP lên</button>
      <button class="primary-button" (click)="create()" [disabled]="working()"><span class="material-symbols-outlined">backup</span>{{working() ? 'Đang xử lý...' : 'Tạo bản sao lưu'}}</button>
    </app-page-header>
    <div class="alert-item danger" style="margin-bottom:16px"><span class="material-symbols-outlined">warning</span><div><b>Phục hồi có thể ghi đè dữ liệu hiện tại</b><p>Hệ thống tự tạo một bản sao lưu an toàn trước khi restore và ghi audit log.</p></div></div>
    <article class="panel table-panel"><div class="table-wrap"><table><thead><tr><th>Tên bản sao lưu</th><th>Loại</th><th>Thời gian</th><th>Dung lượng</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
      @for(item of items(); track item.id){<tr><td><b>{{item.fileName}}</b></td><td>{{item.type||'Manual'}}</td><td>{{item.createdAt | date:'dd/MM/yyyy HH:mm'}}</td><td>{{formatSize(item.sizeBytes)}}</td><td><span class="badge" [class.success]="item.status==='Success'" [class.danger]="item.status==='Failed'">{{item.status}}</span></td><td><div class="inline-actions"><button class="icon-button" title="Tải xuống" (click)="download(item)" [disabled]="item.status!=='Success'"><span class="material-symbols-outlined">download</span></button><button class="secondary-button" (click)="restore(item)" [disabled]="item.status!=='Success'||working()"><span class="material-symbols-outlined">restore</span> Phục hồi</button><button class="icon-button danger" title="Xóa" (click)="remove(item)" [disabled]="working()"><span class="material-symbols-outlined">delete</span></button></div></td></tr>} @empty {<tr><td colspan="6" class="empty">Chưa có bản sao lưu</td></tr>}
    </tbody></table></div></article>`
})
export class BackupsComponent implements OnInit {
  items = signal<BackupItem[]>([]); working = signal(false);
  constructor(private api: ApiService, private toast: ToastService) {}
  ngOnInit(){ this.load(); }
  load(){ this.api.get<BackupItem[]>('/admin/backups').subscribe(r => this.items.set(r.data)); }
  create(){ this.working.set(true); this.api.post('/admin/backups',{}).subscribe({next:()=>this.finish('Sao lưu thành công'),error:e=>this.fail(e,'Sao lưu thất bại')}); }
  upload(file?:File){if(!file)return;const form=new FormData();form.append('file',file);this.working.set(true);this.api.postForm('/admin/backups/upload',form).subscribe({next:()=>this.finish('Tải bản sao lưu lên thành công'),error:e=>this.fail(e,'Tải file thất bại')});}
  download(item:BackupItem){this.api.getBlob(`/admin/backups/${item.id}/download`).subscribe(blob=>{const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=`${item.fileName}.zip`;a.click();URL.revokeObjectURL(url);});}
  restore(item:BackupItem){const confirmation=prompt(`Nhập RESTORE để phục hồi ${item.fileName}`);if(confirmation!=='RESTORE')return;this.working.set(true);this.api.post(`/admin/backups/${item.id}/restore`,{confirmation}).subscribe({next:()=>this.finish('Phục hồi thành công'),error:e=>this.fail(e,'Phục hồi thất bại')});}
  remove(item:BackupItem){if(!confirm(`Xóa bản sao lưu ${item.fileName}?`))return;this.working.set(true);this.api.delete(`/admin/backups/${item.id}`).subscribe({next:()=>this.finish('Xóa bản sao lưu thành công'),error:e=>this.fail(e,'Xóa thất bại')});}
  formatSize(bytes:number){if(!bytes)return '0 B';const units=['B','KB','MB','GB'];const i=Math.min(Math.floor(Math.log(bytes)/Math.log(1024)),3);return `${(bytes/1024**i).toFixed(1)} ${units[i]}`;}
  private finish(message:string){this.toast.show(message,'success');this.working.set(false);this.load();}
  private fail(error:any,fallback:string){this.toast.show(error.error?.message||fallback,'error');this.working.set(false);}
}
