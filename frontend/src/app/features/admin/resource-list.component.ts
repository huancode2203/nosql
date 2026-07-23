import { Component, OnInit, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { PagedResult } from '../../core/models/api.models';
import { ImportPreview } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

@Component({ standalone: true, imports: [FormsModule, PageHeaderComponent], templateUrl: './resource-list.component.html' })
export class ResourceListComponent implements OnInit {
  resource = input.required<string>();
  title = input.required<string>();
  subtitle = input('Quản lý dữ liệu hệ thống');
  columns = input<string[]>([]);
  items = signal<Record<string, any>[]>([]);
  loading = signal(false);
  search = '';
  page = 1;
  total = 0;
  modal = signal(false);
  importModal = signal(false);
  importing = signal(false);
  importFile: File | null = null;
  preview = signal<ImportPreview | null>(null);
  editing = signal<Record<string, any>>({});

  constructor(private api: ApiService, private toast: ToastService) {}
  ngOnInit() { this.load(); }

  load() {
    this.loading.set(true);
    this.api.get<PagedResult<Record<string, any>>>(`/admin/${this.resource()}`, { pageNumber: this.page, pageSize: 20, search: this.search }).subscribe({
      next: response => { this.items.set(response.data.items); this.total = response.data.totalItems; this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  open(item?: Record<string, any>) { this.editing.set(item ? { ...item } : this.defaultValue()); this.modal.set(true); }

  save() {
    const entity = this.editing();
    const id = entity['id'];
    const body = { ...entity };
    delete body['id'];
    const call = id ? this.api.put(`/admin/${this.resource()}/${id}`, body) : this.api.post(`/admin/${this.resource()}`, body);
    call.subscribe({
      next: () => { this.toast.show('Lưu dữ liệu thành công', 'success'); this.modal.set(false); this.load(); },
      error: error => this.toast.show(error.error?.message || 'Không thể lưu dữ liệu', 'error')
    });
  }

  remove(item: Record<string, any>) {
    if (!confirm('Xác nhận xóa mềm bản ghi này?')) return;
    this.api.delete(`/admin/${this.resource()}/${item['id']}`).subscribe({
      next: () => { this.toast.show('Đã xóa mềm', 'success'); this.load(); },
      error: () => this.toast.show('Không thể xóa', 'error')
    });
  }

  exportData() {
    this.api.getBlob(`/admin/export/${this.resource()}`).subscribe({
      next: blob => this.download(blob, `${this.resource()}-${new Date().toISOString().slice(0, 10)}.xlsx`),
      error: () => this.toast.show('Không thể export dữ liệu', 'error')
    });
  }

  chooseImport(fileList: FileList | null) {
    const file = fileList?.item(0) || null;
    if (!file) return;
    this.importFile = file;
    this.preview.set(null);
    this.importModal.set(true);
    this.previewImport(false);
  }

  previewImport(commit: boolean) {
    if (!this.importFile || this.resource() !== 'students') return;
    const form = new FormData();
    form.append('file', this.importFile);
    this.importing.set(true);
    this.api.postForm<ImportPreview>('/admin/import/students', form, { commit }).subscribe({
      next: response => {
        this.preview.set(response.data);
        this.importing.set(false);
        if (commit) { this.toast.show('Import sinh viên thành công', 'success'); this.importModal.set(false); this.load(); }
      },
      error: error => { this.importing.set(false); this.toast.show(error.error?.message || 'Import thất bại', 'error'); }
    });
  }

  updateField(key: string, value: any) { this.editing.update(current => ({ ...current, [key]: value })); }
  display(value: any) { return typeof value === 'object' && value ? JSON.stringify(value) : value; }
  fieldType(column: string) { return /credits|capacity|year|duration|count|score/i.test(column) ? 'number' : /date|from|until|start|end/i.test(column) ? 'date' : 'text'; }

  private defaultValue(): Record<string, any> {
    const value: Record<string, any> = {};
    this.columns().forEach(column => value[column] = column === 'status' ? 'Active' : '');
    return value;
  }

  private download(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
