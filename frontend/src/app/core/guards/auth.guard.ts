import { inject } from '@angular/core'; import { CanActivateFn, Router } from '@angular/router'; import { AuthService } from '../services/auth.service';
export const authGuard:CanActivateFn=()=>{const a=inject(AuthService),r=inject(Router);return a.isAuthenticated()?true:r.createUrlTree(['/login']);};
