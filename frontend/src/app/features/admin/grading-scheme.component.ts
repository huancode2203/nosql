import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { PagedResult } from '../../core/models/api.models';
import { CloDefinition, CourseDesign, GradingComponentDefinition, GradingScheme } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

@Component({ standalone: true, imports: [FormsModule, PageHeaderComponent], template: `
<app-page-header title="Cấu trúc điểm và CLO" subtitle="Tạo phiên bản cấu trúc điểm theo năm học, không làm thay đổi dữ liệu học kỳ cũ."><button class="primary-button" [disabled]="!design()||saving()" (click)="save()"><span class="material-symbols-outlined">save</span> Lưu phiên bản mới</button></app-page-header>
<div class="filter-bar"><label>Môn học<select [(ngModel)]="courseId" (ngModelChange)="loadDesign()"><option value="">Chọn môn học</option>@for(course of courses();track course['id']){<option [value]="course['id']">{{course['courseCode']}} - {{course['courseName']}}</option>}</select></label><label>Năm học<input [(ngModel)]="scheme.academicYear" placeholder="2026-2027"/></label><label>Ngưỡng đạt môn<input type="number" [(ngModel)]="scheme.passingScore"/></label><label>Làm tròn<select [(ngModel)]="scheme.roundingMode"><option>Normal</option><option>None</option><option>Floor</option><option>Ceiling</option></select></label></div>
@if(design();as current){
<div class="dashboard-grid">
  <article class="panel span-2"><div class="panel-heading"><div><h3>Chuẩn đầu ra môn học</h3><p>{{current.courseCode}} - {{current.courseName}}</p></div><button class="secondary-button" (click)="addClo()"><span class="material-symbols-outlined">add</span> Thêm CLO</button></div>
    <div class="table-wrap"><table><thead><tr><th>Mã CLO</th><th>Tên</th><th>Mô tả</th><th>Bloom</th><th>Ngưỡng %</th><th>Trọng số %</th><th></th></tr></thead><tbody>@for(clo of clos;track $index;let index=$index){<tr><td><input class="cell-input" [(ngModel)]="clo.cloCode"/></td><td><input class="cell-input wide" [(ngModel)]="clo.name"/></td><td><input class="cell-input wide" [(ngModel)]="clo.description"/></td><td><select class="cell-input" [(ngModel)]="clo.bloomLevel"><option>Remember</option><option>Understand</option><option>Apply</option><option>Analyze</option><option>Evaluate</option><option>Create</option></select></td><td><input class="cell-input small" type="number" [(ngModel)]="clo.threshold"/></td><td><input class="cell-input small" type="number" [(ngModel)]="clo.weight"/></td><td><button class="icon-button danger" (click)="clos.splice(index,1);touch()"><span class="material-symbols-outlined">delete</span></button></td></tr>}</tbody></table></div>
  </article>
  <article class="panel span-2"><div class="panel-heading"><div><h3>Thành phần điểm</h3><p>Tổng trọng số hiện tại: <b [class.danger-text]="totalWeight()!==100">{{totalWeight()}}%</b></p></div><button class="secondary-button" (click)="addComponent()"><span class="material-symbols-outlined">add</span> Thêm cột điểm</button></div>
    <div class="table-wrap"><table><thead><tr><th>Mã</th><th>Tên cột</th><th>Loại</th><th>Trọng số</th><th>Điểm tối đa</th><th>Bắt buộc</th><th>Điểm tối thiểu</th><th>Ánh xạ CLO</th><th></th></tr></thead><tbody>@for(component of scheme.components;track $index;let index=$index){<tr><td><input class="cell-input small" [(ngModel)]="component.componentId"/></td><td><input class="cell-input wide" [(ngModel)]="component.name"/></td><td><select class="cell-input" [(ngModel)]="component.type"><option>Attendance</option><option>Assignment</option><option>Practice</option><option>Midterm</option><option>Project</option><option>Final</option></select></td><td><input class="cell-input small" type="number" [(ngModel)]="component.weight"/></td><td><input class="cell-input small" type="number" [(ngModel)]="component.maxScore"/></td><td><input type="checkbox" [(ngModel)]="component.isRequired"/></td><td><input class="cell-input small" type="number" [(ngModel)]="component.minimumScore"/></td><td><div class="mapping-list">@for(mapping of component.cloMappings;track $index;let mapIndex=$index){<span><select [(ngModel)]="mapping.cloCode">@for(clo of clos;track clo.cloCode){<option [value]="clo.cloCode">{{clo.cloCode}}</option>}</select><input type="number" [(ngModel)]="mapping.mappingWeight"/><button (click)="component.cloMappings.splice(mapIndex,1);touch()">×</button></span>}<button class="text-button" (click)="addMapping(component)">+ CLO</button></div></td><td><button class="icon-button danger" (click)="scheme.components.splice(index,1);touch()"><span class="material-symbols-outlined">delete</span></button></td></tr>}</tbody></table></div>
  </article>
  <article class="panel"><h3>Lịch sử phiên bản</h3><div class="activity-list">@for(version of current.gradingSchemes;track version.version){<div><span class="activity-icon material-symbols-outlined">history</span><div><b>Phiên bản {{version.version}} - {{version.academicYear}}</b><p>{{version.components.length}} thành phần · Ngưỡng đạt {{version.passingScore}}</p></div><span class="badge" [class.success]="version.active">{{version.active?'Đang áp dụng':'Lưu trữ'}}</span></div>}</div></article>
  <article class="panel"><h3>Kiểm tra cấu hình</h3><div class="alert-list"><div class="alert-item" [class.success]="totalWeight()===100"><span class="material-symbols-outlined">{{totalWeight()===100?'check_circle':'warning'}}</span><div><b>Tổng trọng số</b><p>{{totalWeight()===100?'Đã bằng 100%':'Phải bằng 100%'}}</p></div></div><div class="alert-item" [class.success]="clos.length>0"><span class="material-symbols-outlined">radar</span><div><b>CLO môn học</b><p>{{clos.length}} chuẩn đầu ra</p></div></div></div></article>
</div>}
` })
export class GradingSchemeComponent implements OnInit {
  courses = signal<Record<string, any>[]>([]);
  design = signal<CourseDesign | null>(null);
  courseId = '';
  clos: CloDefinition[] = [];
  scheme: GradingScheme = this.emptyScheme();
  saving = signal(false);
  private revision = signal(0);
  constructor(private api: ApiService, private toast: ToastService) {}
  ngOnInit() { this.api.get<PagedResult<Record<string, any>>>('/admin/courses', { pageNumber: 1, pageSize: 100 }).subscribe(response => this.courses.set(response.data.items)); }
  loadDesign() {
    if (!this.courseId) { this.design.set(null); return; }
    this.api.get<CourseDesign>(`/admin/courses/${this.courseId}/design`).subscribe({
      next: response => {
        this.design.set(response.data);
        this.clos = structuredClone(response.data.clos);
        const latest = response.data.gradingSchemes[0];
        this.scheme = latest ? { ...structuredClone(latest), version: 0, active: true, academicYear: this.nextYear(latest.academicYear) } : this.emptyScheme();
        this.touch();
      },
      error: () => this.toast.show('Không thể tải cấu trúc điểm', 'error')
    });
  }
  addClo() { const number = this.clos.length + 1; this.clos.push({ cloCode: `CLO${number}`, name: `Chuẩn đầu ra ${number}`, description: '', bloomLevel: 'Apply', threshold: 50, weight: 0, active: true }); this.touch(); }
  addComponent() { this.scheme.components.push({ componentId: `TP${this.scheme.components.length + 1}`, name: 'Thành phần mới', type: 'Assignment', weight: 0, maxScore: 10, isRequired: false, isFinalCondition: false, cloMappings: [] }); this.touch(); }
  addMapping(component: GradingComponentDefinition) { component.cloMappings.push({ cloCode: this.clos[0]?.cloCode || 'CLO1', mappingWeight: 100 }); this.touch(); }
  totalWeight() { this.revision(); return Math.round(this.scheme.components.reduce((sum, item) => sum + Number(item.weight || 0), 0) * 100) / 100; }
  touch() { this.revision.update(value => value + 1); }
  save() {
    if (!this.design()) return;
    if (this.totalWeight() !== 100) { this.toast.show('Tổng trọng số phải bằng 100%', 'error'); return; }
    this.saving.set(true);
    this.api.put<CourseDesign>(`/admin/courses/${this.courseId}/design`, { clos: this.clos, scheme: this.scheme }).subscribe({
      next: response => { this.design.set(response.data); this.saving.set(false); this.toast.show('Đã tạo phiên bản cấu trúc điểm mới', 'success'); this.loadDesign(); },
      error: error => { this.saving.set(false); this.toast.show(error.error?.message || 'Không thể lưu cấu trúc điểm', 'error'); }
    });
  }
  private emptyScheme(): GradingScheme { return { version: 0, academicYear: '2026-2027', components: [], passingScore: 4, roundingMode: 'Normal', decimalPlaces: 2, effectiveFrom: new Date().toISOString(), active: true }; }
  private nextYear(value: string) { const parts = value.split('-').map(Number); return parts.length === 2 && parts.every(Number.isFinite) ? `${parts[0] + 1}-${parts[1] + 1}` : value; }
}
