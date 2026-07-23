import { Component, OnInit, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ImportPreview } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

interface ComponentDef { componentId: string; componentName: string; weight: number; maxScore: number; }
interface StudentRow { studentId: string; studentCode: string; fullName: string; scores: Record<string, number | null>; finalScore: number; letterGrade: string; passed: boolean; dirty?: boolean; }
interface Gradebook { classSectionId: string; classSectionCode: string; courseName: string; status: string; components: ComponentDef[]; students: StudentRow[]; }

@Component({ standalone: true, imports: [FormsModule, PageHeaderComponent], templateUrl: './gradebook.component.html' })
export class GradebookComponent implements OnInit {
  id = input<string>('default');
  book = signal<Gradebook | null>(null);
  loading = signal(true);
  saving = signal(false);
  importModal = signal(false);
  importPreview = signal<ImportPreview | null>(null);
  importFile: File | null = null;
  constructor(private api: ApiService, private toast: ToastService) {}
  ngOnInit() { this.load(); }
  load() { this.loading.set(true); this.api.get<Gradebook>(`/lecturer/classes/${this.id() || 'default'}/gradebook`).subscribe({ next: response => { this.book.set(response.data); this.loading.set(false); }, error: () => this.loading.set(false) }); }
  change(row: StudentRow, key: string, value: any, max: number) { const number = value === '' ? null : Number(value); row.scores[key] = number === null ? null : Math.max(0, Math.min(max, number)); row.dirty = true; this.recalculate(row); }
  recalculate(row: StudentRow) { const book = this.book(); if (!book) return; let sum = 0; for (const component of book.components) sum += ((row.scores[component.componentId] || 0) / component.maxScore) * 10 * (component.weight / 100); row.finalScore = Math.round(sum * 100) / 100; row.letterGrade = sum >= 8.5 ? 'A' : sum >= 8 ? 'B+' : sum >= 7 ? 'B' : sum >= 6.5 ? 'C+' : sum >= 5.5 ? 'C' : sum >= 5 ? 'D+' : sum >= 4 ? 'D' : 'F'; row.passed = sum >= 4; }
  save(publish = false) { const book = this.book(); if (!book) return; const changed = book.students.filter(x => x.dirty); if (!changed.length) { this.toast.show('Chưa có ô điểm nào thay đổi', 'info'); return; } this.saving.set(true); this.api.put(`/lecturer/classes/${book.classSectionId}/grades`, { students: changed, publish }).subscribe({ next: () => { this.toast.show(publish ? 'Đã công bố điểm' : 'Đã lưu nháp', 'success'); book.students.forEach(x => x.dirty = false); if (publish) book.status = 'Published'; this.saving.set(false); }, error: error => { this.toast.show(error.error?.message || 'Lưu điểm thất bại', 'error'); this.saving.set(false); } }); }
  exportData() { const book = this.book(); if (!book) return; this.api.getBlob(`/lecturer/classes/${book.classSectionId}/gradebook/export`).subscribe(blob => this.download(blob, `bang-diem-${book.classSectionCode}.xlsx`)); }
  chooseImport(files: FileList | null) { this.importFile = files?.item(0) || null; if (!this.importFile || !this.book()) return; const form = new FormData(); form.append('file', this.importFile); this.api.postForm<ImportPreview>(`/lecturer/classes/${this.book()!.classSectionId}/grades/import`, form, { commit: false }).subscribe({ next: response => { this.importPreview.set(response.data); this.importModal.set(true); }, error: error => this.toast.show(error.error?.message || 'Không thể đọc file', 'error') }); }
  commitImport() { if (!this.importFile || !this.book()) return; const form = new FormData(); form.append('file', this.importFile); this.api.postForm<ImportPreview>(`/lecturer/classes/${this.book()!.classSectionId}/grades/import`, form, { commit: true }).subscribe({ next: () => { this.toast.show('Import điểm thành công', 'success'); this.importModal.set(false); this.load(); }, error: error => this.toast.show(error.error?.message || 'Import thất bại', 'error') }); }
  requestReopen() { const book = this.book(); if (!book) return; const reason = prompt('Lý do yêu cầu mở lại bảng điểm', 'Cần điều chỉnh điểm sau rà soát'); if (!reason) return; this.api.post(`/lecturer/classes/${book.classSectionId}/request-reopen`, { reason }).subscribe({ next: () => this.toast.show('Đã gửi yêu cầu đến Admin', 'success'), error: error => this.toast.show(error.error?.message || 'Không thể gửi yêu cầu', 'error') }); }
  private download(blob: Blob, name: string) { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = name; anchor.click(); URL.revokeObjectURL(url); }
}
