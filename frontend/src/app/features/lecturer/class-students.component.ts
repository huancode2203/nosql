import { Component, OnInit, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ClassStudent } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({ standalone: true, imports: [FormsModule, PageHeaderComponent], template: `
<app-page-header title="Danh sách sinh viên" subtitle="Tra cứu sinh viên thuộc lớp được phân công."><button class="secondary-button" (click)="print()"><span class="material-symbols-outlined">print</span> In danh sách</button></app-page-header><article class="panel table-panel"><div class="table-toolbar"><div class="search-box"><span class="material-symbols-outlined">search</span><input [(ngModel)]="search" placeholder="Mã sinh viên hoặc họ tên..."/></div></div><div class="table-wrap"><table><thead><tr><th>STT</th><th>Mã sinh viên</th><th>Họ tên</th><th>Email</th><th>Lớp hành chính</th><th>Trạng thái</th></tr></thead><tbody>@for(item of filtered();track item.id;let index=$index){<tr><td>{{index+1}}</td><td><b>{{item.studentCode}}</b></td><td>{{item.fullName}}</td><td>{{item.email}}</td><td>{{item.administrativeClass}}</td><td><span class="badge" [class.success]="item.status==='Studying'">{{item.status}}</span></td></tr>}@empty{<tr><td colspan="6" class="empty">Không có sinh viên</td></tr>}</tbody></table></div></article>
` })
export class ClassStudentsComponent implements OnInit { id = input.required<string>(); students = signal<ClassStudent[]>([]); search = ''; constructor(private api: ApiService) {} ngOnInit() { this.api.get<ClassStudent[]>(`/lecturer/classes/${this.id()}/students`).subscribe(response => this.students.set(response.data)); } filtered() { const term = this.search.toLowerCase(); return this.students().filter(x => !term || x.studentCode.toLowerCase().includes(term) || x.fullName.toLowerCase().includes(term)); } print() { window.print(); } }
