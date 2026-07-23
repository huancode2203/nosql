import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';

interface ForgotPasswordResult { demoCode?: string; expiresInMinutes: number; }

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  template: `
  <div class="auth-page">
    <section class="auth-visual">
      <div class="visual-content"><span class="eyebrow">KHÔI PHỤC TÀI KHOẢN</span><h1>Đặt lại mật khẩu an toàn.</h1><p>Mã xác nhận chỉ có hiệu lực trong thời gian ngắn và mọi phiên đăng nhập cũ sẽ bị thu hồi sau khi đổi mật khẩu.</p></div>
    </section>
    <section class="auth-panel">
      <form class="login-card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="login-brand"><div class="brand-mark">E</div><div><b>EduManage LMS</b><span>Cổng thông tin đào tạo</span></div></div>
        <div><h2>{{ step() === 1 ? 'Quên mật khẩu' : 'Nhập mã xác nhận' }}</h2><p>{{ step() === 1 ? 'Nhập email tài khoản để nhận mã xác nhận.' : 'Mã có hiệu lực trong 10 phút.' }}</p></div>
        @if (message()) { <div class="alert" [class.error]="isError()">{{ message() }}</div> }
        <label>Email<input type="email" formControlName="email" autocomplete="email" /></label>
        @if (step() === 2) {
          <label>Mã xác nhận<input inputmode="numeric" maxlength="6" formControlName="code" autocomplete="one-time-code" /></label>
          <label>Mật khẩu mới<input type="password" formControlName="newPassword" autocomplete="new-password" /></label>
          <label>Xác nhận mật khẩu<input type="password" formControlName="confirmPassword" autocomplete="new-password" /></label>
        }
        <button class="primary-button full" [disabled]="loading()">{{ loading() ? 'Đang xử lý...' : step() === 1 ? 'Gửi mã xác nhận' : 'Đặt lại mật khẩu' }}</button>
        <a class="text-button" routerLink="/login">Quay lại đăng nhập</a>
      </form>
    </section>
  </div>`
})
export class ForgotPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly step = signal<1 | 2>(1);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly isError = signal(false);
  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    code: [''],
    newPassword: [''],
    confirmPassword: ['']
  });

  submit(): void {
    this.message.set('');
    this.isError.set(false);
    if (this.step() === 1) {
      if (this.form.controls.email.invalid) return;
      this.loading.set(true);
      this.api.post<ForgotPasswordResult>('/auth/forgot-password', { email: this.form.controls.email.value }).subscribe({
        next: response => {
          this.step.set(2);
          if (response.data.demoCode) this.form.controls.code.setValue(response.data.demoCode);
          this.message.set(response.data.demoCode ? `Mã demo: ${response.data.demoCode}` : response.message);
          this.loading.set(false);
        },
        error: error => this.fail(error)
      });
      return;
    }

    const { email, code, newPassword, confirmPassword } = this.form.getRawValue();
    if (!code || !newPassword || newPassword !== confirmPassword) {
      this.message.set(newPassword !== confirmPassword ? 'Mật khẩu xác nhận không khớp.' : 'Vui lòng nhập đầy đủ thông tin.');
      this.isError.set(true);
      return;
    }
    this.loading.set(true);
    this.api.post('/auth/reset-password', { email, code, newPassword }).subscribe({
      next: () => {
        this.message.set('Đặt lại mật khẩu thành công. Đang chuyển về trang đăng nhập...');
        this.loading.set(false);
        setTimeout(() => this.router.navigateByUrl('/login'), 1200);
      },
      error: error => this.fail(error)
    });
  }

  private fail(error: any): void {
    this.loading.set(false);
    this.isError.set(true);
    this.message.set(error?.error?.message || 'Không thể xử lý yêu cầu.');
  }
}
