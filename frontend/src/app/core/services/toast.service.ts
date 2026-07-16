import { Injectable, signal } from '@angular/core';
export interface Toast { id:number; message:string; kind:'success'|'error'|'info'; }
@Injectable({providedIn:'root'}) export class ToastService {
  private seq=0; readonly toasts=signal<Toast[]>([]);
  show(message:string,kind:Toast['kind']='info'){const t={id:++this.seq,message,kind};this.toasts.update(x=>[...x,t]);setTimeout(()=>this.dismiss(t.id),3500)}
  dismiss(id:number){this.toasts.update(x=>x.filter(t=>t.id!==id))}
}
