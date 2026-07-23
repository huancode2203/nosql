import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { LecturerClass } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({ standalone: true, imports: [RouterLink, DatePipe, PageHeaderComponent], template: `
<app-page-header title="Lớp học phần được phân công" subtitle="Chỉ hiển thị các lớp thuộc phạm vi giảng dạy của tài khoản hiện tại."></app-page-header>
<div class="course-grade-grid">@for(item of classes();track item.id){<article class="panel class-card"><div class="panel-heading"><div><span class="eyebrow">{{item.courseCode}}</span><h3>{{item.courseName}}</h3><p>{{item.classSectionCode}} · {{item.semesterName}} · {{item.academicYearName}}</p></div><span class="badge" [class.success]="item.gradeStatus==='Published'">{{item.gradeStatus}}</span></div><div class="class-meta"><span><i class="material-symbols-outlined">group</i>{{item.studentCount}} sinh viên</span><span><i class="material-symbols-outlined">calendar_month</i>{{item.startDate|date:'dd/MM'}} - {{item.endDate|date:'dd/MM/yyyy'}}</span>@for(slot of item.schedule;track $index){<span><i class="material-symbols-outlined">schedule</i>{{slot.dayOfWeek}} {{slot.startTime}} · {{slot.room}}</span>}</div><div class="class-actions"><a class="secondary-button" [routerLink]="['/lecturer/classes',item.id,'students']">Sinh viên</a><a class="primary-button" [routerLink]="['/lecturer/classes',item.id,'grades']">Nhập điểm</a><a class="text-button" [routerLink]="['/lecturer/classes',item.id,'statistics']">Thống kê</a><a class="text-button" [routerLink]="['/lecturer/classes',item.id,'materials']">Tài liệu</a><a class="text-button" [routerLink]="['/lecturer/classes',item.id,'assignments']">Bài tập</a></div></article>}@empty{<article class="panel empty-state"><span class="material-symbols-outlined">class</span><h3>Chưa được phân công lớp</h3></article>}</div>
` })
export class LecturerClassesComponent implements OnInit { classes = signal<LecturerClass[]>([]); constructor(private api: ApiService) {} ngOnInit() { this.api.get<LecturerClass[]>('/lecturer/classes').subscribe(response => this.classes.set(response.data)); } }
