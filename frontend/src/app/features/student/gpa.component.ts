import { Component, OnInit, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatCardComponent } from '../../shared/stat-card.component';
interface Gpa { gpa:number; average10:number; totalCredits:number; passedCredits:number; classification:string; }
@Component({standalone:true,imports:[PageHeaderComponent,StatCardComponent],template:`
<app-page-header title="Điểm trung bình và tiến độ" subtitle="GPA được tính theo tín chỉ trực tiếp bằng MongoDB Aggregation Pipeline."></app-page-header>
@if(data();as g){<section class="stats-grid"><app-stat-card label="GPA tích lũy" [value]="g.gpa" icon="insights" [trend]="g.classification"/><app-stat-card label="Trung bình hệ 10" [value]="g.average10" icon="calculate" tone="success"/><app-stat-card label="Tổng tín chỉ tính GPA" [value]="g.totalCredits" icon="menu_book" tone="warning"/><app-stat-card label="Tín chỉ đã đạt" [value]="g.passedCredits" icon="workspace_premium" tone="success"/></section><article class="panel"><div class="panel-heading"><div><h3>Tiến độ chương trình đào tạo</h3><p>{{g.passedCredits}} / 130 tín chỉ</p></div><b>{{(g.passedCredits/130*100).toFixed(1)}}%</b></div><div class="progress"><i [style.width.%]="g.passedCredits/130*100"></i></div></article>}
`}) export class GpaComponent implements OnInit{data=signal<Gpa|null>(null);constructor(private api:ApiService){}ngOnInit(){this.api.get<Gpa>('/student/gpa').subscribe(r=>this.data.set(r.data));}}
