import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const roleGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.initialize().pipe(
    map(() => {
      if (!authService.isLoggedIn()) {
        return router.createUrlTree(['/login']);
      }

      if (authService.isPasswordChangeRequired()) {
        return router.createUrlTree(['/change-password']);
      }

      const allowedRoles = route.data?.['roles'] as string[] | undefined;

      if (!allowedRoles || allowedRoles.length === 0) {
        return true;
      }

      if (allowedRoles.some((role) => authService.hasRole(role))) {
        return true;
      }

      return router.createUrlTree([authService.getHomeUrl()]);
    })
  );
};
