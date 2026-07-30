import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  message: string;
  kind: 'success' | 'error' | 'info';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private seq = 0;
  readonly toasts = signal<Toast[]>([]);

  show(message: string, kind: Toast['kind'] = 'info') {
    const normalizedMessage = message.trim();
    if (!normalizedMessage) return;

    const duplicate = this.toasts().some(
      toast =>
        toast.message === normalizedMessage
        && toast.kind === kind
    );

    if (duplicate) return;

    const toast: Toast = {
      id: ++this.seq,
      message: normalizedMessage,
      kind
    };

    this.toasts.update(current => [
      ...current.slice(-2),
      toast
    ]);

    window.setTimeout(
      () => this.dismiss(toast.id),
      3500
    );
  }

  dismiss(id: number) {
    this.toasts.update(current =>
      current.filter(toast => toast.id !== id)
    );
  }
}
