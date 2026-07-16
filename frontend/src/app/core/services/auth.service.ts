import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, CurrentUser, LoginResponse, Role } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly userSignal = signal<CurrentUser | null>(this.readUser());
  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => !!this.userSignal() && !!localStorage.getItem('access_token'));
  constructor(private http: HttpClient, private router: Router) {}
  login(identifier: string, password: string, rememberMe = false): Observable<ApiResponse<LoginResponse>> {
    return this.http.post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/login`, { identifier, password, rememberMe }).pipe(
      tap(r => { if (r.success) this.store(r.data); })
    );
  }
  logout(): void {
    const refreshToken = localStorage.getItem('refresh_token');
    if (refreshToken) this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => void 0 });
    localStorage.clear(); sessionStorage.clear(); this.userSignal.set(null); this.router.navigateByUrl('/login');
  }
  hasRole(roles: Role[]): boolean { const u=this.userSignal(); return !!u && roles.includes(u.role); }
  homeFor(role: Role): string { return role === 'Admin' ? '/admin/dashboard' : role === 'Lecturer' ? '/lecturer/dashboard' : '/student/dashboard'; }
  private store(data: LoginResponse): void {
    localStorage.setItem('access_token', data.accessToken); localStorage.setItem('refresh_token', data.refreshToken);
    localStorage.setItem('current_user', JSON.stringify(data.user)); this.userSignal.set(data.user);
  }
  private readUser(): CurrentUser | null { try { return JSON.parse(localStorage.getItem('current_user') || 'null'); } catch { return null; } }
}
