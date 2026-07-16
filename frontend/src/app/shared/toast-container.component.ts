import { Component } from '@angular/core'; import { ToastService } from '../core/services/toast.service';
@Component({selector:'app-toast-container',standalone:true,template:`<div class="toast-stack">@for(t of toast.toasts();track t.id){<button class="toast" [class]="t.kind" (click)="toast.dismiss(t.id)">{{t.message}}</button>}</div>`})
export class ToastContainerComponent{constructor(public toast:ToastService){}}
