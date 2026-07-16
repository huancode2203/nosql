import { inject } from '@angular/core'; import { CanActivateFn, Router } from '@angular/router'; import { AuthService } from '../services/auth.service'; import { Role } from '../models/api.models';
export const roleGuard=(roles:Role[]):CanActivateFn=>()=>{const a=inject(AuthService),r=inject(Router);return a.hasRole(roles)?true:r.createUrlTree(['/unauthorized']);};
