import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const token = auth.accessToken();
  const authenticatedRequest = token
    ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : request;

  return next(authenticatedRequest).pipe(
    catchError((error: HttpErrorResponse) => {
      const isAuthEndpoint = request.url.includes('/auth/login') || request.url.includes('/auth/refresh-token');
      if (error.status === 401 && !isAuthEndpoint && auth.refreshToken()) {
        return auth.refreshSession().pipe(
          switchMap(newToken => next(request.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } }))),
          catchError(refreshError => {
            auth.clearSession();
            router.navigateByUrl('/login');
            return throwError(() => refreshError);
          })
        );
      }
      if (error.status === 401) {
        auth.clearSession();
        router.navigateByUrl('/login');
      } else if (error.status === 403) {
        router.navigateByUrl('/unauthorized');
      }
      return throwError(() => error);
    })
  );
};
