import { Component, OnInit, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { PagedResult } from '../../core/models/api.models';
import { ImportPreview } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';
import {
  ADMIN_IMPORT_RESOURCES,
  ADMIN_PERMISSIONS,
  ADMIN_RESOURCE_FIELDS,
  AdminFieldDefinition,
  AdminFieldOption
} from './admin-resource.schema';

interface AdminAvatarResult {
  userId: string;
  avatarUrl?: string;
}

@Component({
  standalone: true,
  imports: [FormsModule, PageHeaderComponent],
  templateUrl: './resource-list.component.html'
})
export class ResourceListComponent implements OnInit {
  resource = input.required<string>();
  title = input.required<string>();
  subtitle = input('Quản lý dữ liệu hệ thống');
  columns = input<string[]>([]);

  readonly items = signal<Record<string, any>[]>([]);
  readonly loading = signal(false);
  readonly deletedOnly = signal(false);
  readonly modal = signal(false);
  readonly importModal = signal(false);
  readonly importing = signal(false);
  readonly avatarUploading = signal(false);
  readonly preview = signal<ImportPreview | null>(null);
  readonly editing = signal<Record<string, any>>({});
  readonly optionMap = signal<Record<string, AdminFieldOption[]>>({});
  readonly fields = computed(
    () => ADMIN_RESOURCE_FIELDS[this.resource()]
      ?? this.columns().map(key => ({ key, label: key }))
  );

  readonly permissionOptions = ADMIN_PERMISSIONS;
  search = '';
  page = 1;
  total = 0;
  importFile: File | null = null;

  constructor(
    private readonly api: ApiService,
    private readonly toast: ToastService,
    readonly auth: AuthService
  ) {}

  ngOnInit() {
    this.load();
    this.loadOptions();
  }

  load(resetPage = false) {
    if (resetPage) {
      this.page = 1;
    }
    this.loading.set(true);
    this.api.get<PagedResult<Record<string, any>>>(
      `/admin/${this.resource()}`,
      {
        pageNumber: this.page,
        pageSize: 20,
        search: this.search,
        deletedOnly: this.deletedOnly()
      }
    ).subscribe({
      next: response => {
        this.items.set(response.data.items ?? []);
        this.total = response.data.totalItems ?? 0;
        this.loading.set(false);
      },
      error: error => {
        this.items.set([]);
        this.loading.set(false);
        this.toast.show(
          error.error?.message || 'Không thể tải dữ liệu.',
          'error'
        );
      }
    });
  }

  toggleTrash() {
    this.deletedOnly.update(value => !value);
    this.load(true);
  }

  open(item?: Record<string, any>) {
    const value = item
      ? this.normalizeForForm(item)
      : this.defaultValue();
    this.editing.set(value);
    this.modal.set(true);
  }

  save() {
    const entity = this.editing();
    const id = entity['id'];
    const body: Record<string, unknown> = {};
    for (const field of this.fields()) {
      if (field.createOnly && id) {
        continue;
      }
      if (
        field.type === 'permissions'
        && !this.canManageUserPermissions()
      ) {
        continue;
      }
      const value = entity[field.key];
      if (value !== undefined && value !== null && value !== '') {
        body[field.key] = value;
      } else if (field.type === 'checkbox') {
        body[field.key] = false;
      }
    }

    const call = id
      ? this.api.put(`/admin/${this.resource()}/${id}`, body)
      : this.api.post(`/admin/${this.resource()}`, body);
    call.subscribe({
      next: () => {
        this.toast.show('Lưu dữ liệu thành công', 'success');
        this.modal.set(false);
        this.load();
        this.loadOptions();
      },
      error: error => this.toast.show(
        error.error?.message || 'Không thể lưu dữ liệu',
        'error'
      )
    });
  }

  remove(item: Record<string, any>) {
    if (!confirm('Xác nhận xóa mềm bản ghi này?')) {
      return;
    }
    this.api.delete(`/admin/${this.resource()}/${item['id']}`).subscribe({
      next: () => {
        this.toast.show('Đã chuyển bản ghi vào thùng rác', 'success');
        this.load();
      },
      error: error => this.toast.show(
        error.error?.message || 'Không thể xóa',
        'error'
      )
    });
  }

  restore(item: Record<string, any>) {
    if (!confirm('Khôi phục bản ghi này?')) {
      return;
    }
    this.api.post(
      `/admin/${this.resource()}/${item['id']}/restore`,
      {}
    ).subscribe({
      next: () => {
        this.toast.show('Khôi phục dữ liệu thành công', 'success');
        this.load();
      },
      error: error => this.toast.show(
        error.error?.message || 'Không thể khôi phục',
        'error'
      )
    });
  }

  exportData() {
    this.api.getBlob(`/admin/export/${this.resource()}`).subscribe({
      next: blob => this.download(
        blob,
        `${this.resource()}-${new Date().toISOString().slice(0, 10)}.xlsx`
      ),
      error: () => this.toast.show('Không thể export dữ liệu', 'error')
    });
  }

  canImport() {
    return ADMIN_IMPORT_RESOURCES.has(this.resource());
  }

  canWrite() {
    return this.resource() === 'system-settings'
      ? this.auth.hasPermission('admin.settings.manage')
      : this.auth.hasPermission('admin.resources.write');
  }

  canDelete() {
    return this.resource() === 'system-settings'
      ? this.auth.hasPermission('admin.settings.manage')
      : this.auth.hasPermission('admin.resources.delete');
  }

  canImportExport() {
    return this.auth.hasPermission('admin.import_export');
  }

  canManageUserPermissions() {
    return this.auth.hasPermission('admin.users.permissions');
  }

  canManageUserAvatars() {
    return this.resource() === 'users'
      && this.auth.hasPermission('admin.users.avatars');
  }

  avatarSrc() {
    return this.api.assetUrl(this.editing()['avatarUrl']);
  }

  chooseAvatar(fileList: FileList | null) {
    const file = fileList?.item(0);
    const userId = this.editing()['id'];
    if (!file || !userId || !this.canManageUserAvatars()) {
      return;
    }

    const form = new FormData();
    form.append('file', file);
    this.avatarUploading.set(true);
    this.api.postForm<AdminAvatarResult>(
      `/admin/users/${userId}/avatar`,
      form
    ).subscribe({
      next: response => {
        this.updateField('avatarUrl', response.data.avatarUrl || null);
        this.avatarUploading.set(false);
        this.toast.show('Đã cập nhật ảnh đại diện', 'success');
        this.load();
      },
      error: error => {
        this.avatarUploading.set(false);
        this.toast.show(
          error.error?.message || 'Không thể tải ảnh đại diện',
          'error'
        );
      }
    });
  }

  removeAvatar() {
    const userId = this.editing()['id'];
    if (!userId || !this.canManageUserAvatars()) {
      return;
    }
    if (!confirm('Xóa ảnh đại diện của tài khoản này?')) {
      return;
    }

    this.avatarUploading.set(true);
    this.api.delete<AdminAvatarResult>(
      `/admin/users/${userId}/avatar`
    ).subscribe({
      next: () => {
        this.updateField('avatarUrl', null);
        this.avatarUploading.set(false);
        this.toast.show('Đã xóa ảnh đại diện', 'success');
        this.load();
      },
      error: error => {
        this.avatarUploading.set(false);
        this.toast.show(
          error.error?.message || 'Không thể xóa ảnh đại diện',
          'error'
        );
      }
    });
  }

  chooseImport(fileList: FileList | null) {
    const file = fileList?.item(0) || null;
    if (!file) {
      return;
    }
    this.importFile = file;
    this.preview.set(null);
    this.importModal.set(true);
    this.previewImport(false);
  }

  previewImport(commit: boolean) {
    if (!this.importFile || !this.canImport()) {
      return;
    }
    const form = new FormData();
    form.append('file', this.importFile);
    this.importing.set(true);
    this.api.postForm<ImportPreview>(
      `/admin/import/${this.resource()}`,
      form,
      { commit }
    ).subscribe({
      next: response => {
        this.preview.set(response.data);
        this.importing.set(false);
        if (commit) {
          this.toast.show('Import dữ liệu thành công', 'success');
          this.importModal.set(false);
          this.load();
          this.loadOptions();
        }
      },
      error: error => {
        this.importing.set(false);
        this.toast.show(
          error.error?.message || 'Import thất bại',
          'error'
        );
      }
    });
  }

  updateField(key: string, value: any) {
    this.editing.update(current => ({ ...current, [key]: value }));
  }

  togglePermission(permission: string, checked: boolean) {
    const current = new Set<string>(
      Array.isArray(this.editing()['permissions'])
        ? this.editing()['permissions']
        : []
    );
    checked ? current.add(permission) : current.delete(permission);
    this.updateField('permissions', [...current]);
  }

  hasPermission(permission: string) {
    return (this.editing()['permissions'] ?? []).includes(permission);
  }

  fieldOptions(field: AdminFieldDefinition) {
    return field.options ?? this.optionMap()[field.source ?? ''] ?? [];
  }

  display(value: any) {
    if (Array.isArray(value)) {
      return value.join(', ');
    }
    return typeof value === 'object' && value
      ? JSON.stringify(value)
      : value;
  }

  private loadOptions() {
    const sources = [
      ...new Set(
        this.fields()
          .map(field => field.source)
          .filter((source): source is string => !!source)
      )
    ];
    if (sources.length === 0) {
      return;
    }

    forkJoin(
      sources.map(source =>
        this.api.get<PagedResult<Record<string, any>>>(
          `/admin/${source}`,
          { pageNumber: 1, pageSize: 100 }
        )
      )
    ).subscribe({
      next: responses => {
        const map: Record<string, AdminFieldOption[]> = {};
        sources.forEach((source, index) => {
          const definition = this.fields().find(
            field => field.source === source
          );
          const labelKey = definition?.optionLabel ?? 'name';
          map[source] = (responses[index].data.items ?? []).map(item => ({
            value: item['id'],
            label: item[labelKey]
              || item['fullName']
              || item['code']
              || item['id']
          }));
        });
        this.optionMap.set(map);
      },
      error: error => {
        this.optionMap.set({});
        this.toast.show(
          error.error?.message
          || 'Không thể tải dữ liệu cho các ô lựa chọn.',
          'error'
        );
      }
    });
  }

  private defaultValue(): Record<string, any> {
    const value: Record<string, any> = {};
    for (const field of this.fields()) {
      if (field.type === 'checkbox') {
        value[field.key] = false;
      } else if (field.type === 'permissions') {
        value[field.key] = [];
      } else if (field.options?.length) {
        value[field.key] = field.options[0].value;
      } else {
        value[field.key] = '';
      }
    }
    if (this.resource() === 'users') {
      value['permissions'] = ADMIN_PERMISSIONS.map(option => option.value);
    }
    return value;
  }

  private normalizeForForm(item: Record<string, any>) {
    const value = { ...item };
    for (const field of this.fields()) {
      if (
        (field.type === 'date' || field.type === 'datetime-local')
        && value[field.key]
      ) {
        const date = new Date(value[field.key]);
        const local = new Date(
          date.getTime() - date.getTimezoneOffset() * 60000
        ).toISOString();
        value[field.key] = field.type === 'date'
          ? local.slice(0, 10)
          : local.slice(0, 16);
      }
    }
    value['permissions'] = Array.isArray(value['permissions'])
      ? value['permissions']
      : [];
    if (
      this.resource() === 'users'
      && value['role'] === 'Admin'
      && value['permissionsConfigured'] !== true
    ) {
      value['permissions'] = ADMIN_PERMISSIONS.map(
        option => option.value
      );
    }
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
