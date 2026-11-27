import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { map } from 'rxjs';
import { SessionService } from './session.service';

export const chiefLeaderGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);
  return session.ensureLoaded().pipe(
    map(() => (session.isChiefLeader() || session.isAdmin())
      ? true
      : session.isAuthenticated()
        ? router.createUrlTree(['/access-denied'])
        : new RedirectCommand(router.createUrlTree(['/sign-in']), { state: { returnUrl: state.url } })),
  );
};
