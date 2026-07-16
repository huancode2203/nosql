import { Component, OnInit, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { PageHeaderComponent } from '../../shared/page-header.component';
interface NotificationItem{id:string;title:string;content:string;type:string;priority:string;createdAt:string;isRead:boolean;}
@Component({standalone:true,imports:[DatePipe,PageHeaderComponent],template:`
<app-page-header title="Thông báo" subtitle="Thông báo học vụ và hoạt động liên quan đến tài khoản của bạn."><button class="secondary-button" (click)="readAll()">Đánh dấu tất cả đã đọc</button></app-page-header>
<article class="panel"><div class="activity-list">@for(n of items();track n.id){<div [style.opacity]="n.isRead ? 0.65 : 1"><span class="activity-icon material-symbols-outlined">{{n.priority==='High'?'priority_high':'notifications'}}</span><div><b>{{n.title}}</b><p>{{n.content}}</p><span class="badge">{{n.type}}</span></div><div style="text-align:right"><time>{{n.createdAt|date:'dd/MM HH:mm'}}</time>@if(!n.isRead){<button class="text-button" (click)="read(n)">Đã đọc</button>}</div></div>}@empty{<div class="empty">Không có thông báo</div>}</div></article>
`}) export class NotificationsComponent implements OnInit{items=signal<NotificationItem[]>([]);constructor(private api:ApiService){}ngOnInit(){this.load()}load(){this.api.get<NotificationItem[]>('/notifications').subscribe(r=>this.items.set(r.data))}read(n:NotificationItem){this.api.put(`/notifications/${n.id}/read`,{}).subscribe(()=>{n.isRead=true;this.items.set([...this.items()])})}readAll(){this.api.put('/notifications/read-all',{}).subscribe(()=>this.items.update(x=>x.map(n=>({...n,isRead:true}))))}}
