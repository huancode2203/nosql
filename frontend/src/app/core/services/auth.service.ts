import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, CurrentUser, LoginResponse, Role } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly userSignal = signal<CurrentUser | null>(this.readUser());
  private refreshInFlight?: Observable<string>;

  readonly user = this.userSignal.asReadonly();
  readonly isAuthenticated = computed(() => !!this.userSignal() && !!this.accessToken());

  constructor(private readonly http: HttpClient, private readonly router: Router) {}

  login(identifier: string, password: string, rememberMe = false): Observable<ApiResponse<LoginResponse>> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/login`, { identifier, password, rememberMe })
      .pipe(tap(response => { if (response.success) this.store(response.data, rememberMe); }));
  }

  refreshSession(): Observable<string> {
    if (this.refreshInFlight) return this.refreshInFlight;
    const refreshToken = this.refreshToken();
    if (!refreshToken) throw new Error('Không có refresh token');
    const rememberMe = !!localStorage.getItem('refresh_token');

    this.refreshInFlight = this.http
      .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/refresh-token`, { refreshToken })
      .pipe(
        tap(response => this.store(response.data, rememberMe)),
        map(response => response.data.accessToken),
        finalize(() => { this.refreshInFlight = undefined; }),
        shareReplay(1)
      );
    return this.refreshInFlight;
  }

  logout(): void {
    const refreshToken = this.refreshToken();
    if (refreshToken) {
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => void 0 });
    }
    this.clearSession();
    this.router.navigateByUrl('/login');
  }

  clearSession(): void {
    ['access_token', 'refresh_token', 'current_user'].forEach(key => {
      localStorage.removeItem(key);
      sessionStorage.removeItem(key);
    });
    this.userSignal.set(null);
  }

  accessToken(): string | null {
    return localStorage.getItem('access_token') || sessionStorage.getItem('access_token');
  }

  refreshToken(): string | null {
    return localStorage.getItem('refresh_token') || sessionStorage.getItem('refresh_token');
  }

  hasRole(roles: Role[]): boolean {
    const user = this.userSignal();
    return !!user && roles.includes(user.role);
  }

  hasPermission(permission: string): boolean {
    const user = this.userSignal();
    return !!user && (
      user.permissions?.includes('admin.full_access')
      || user.permissions?.includes(permission)
    );
  }

  homeFor(role: Role): string {
    return role === 'Admin' ? '/admin/dashboard' : role === 'Lecturer' ? '/lecturer/dashboard' : '/student/dashboard';
  }

  private store(data: LoginResponse, rememberMe: boolean): void {
    const target = rememberMe ? localStorage : sessionStorage;
    const other = rememberMe ? sessionStorage : localStorage;
    ['access_token', 'refresh_token', 'current_user'].forEach(key => other.removeItem(key));
    target.setItem('access_token', data.accessToken);
    target.setItem('refresh_token', data.refreshToken);
    target.setItem('current_user', JSON.stringify(data.user));
    this.userSignal.set(data.user);
  }

  private readUser(): CurrentUser | null {
    try {
      const raw = localStorage.getItem('current_user') || sessionStorage.getItem('current_user');
      return raw ? JSON.parse(raw) as CurrentUser : null;
    } catch {
      return null;
    }
  }
}
