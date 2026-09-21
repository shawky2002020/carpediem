import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { SessionService } from './session.service';

export const adminGuard: CanActivateFn = () => {
  const session = inject(SessionService);
  const router = inject(Router);
  return session.ensureLoaded().pipe(
    map(() => session.isAdmin()
      ? true
      : router.createUrlTree([session.isAuthenticated() ? '/access-denied' : '/sign-in'])),
  );
};
