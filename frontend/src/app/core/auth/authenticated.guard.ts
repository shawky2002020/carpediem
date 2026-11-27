import { inject } from '@angular/core';
import { CanActivateFn, RedirectCommand, Router } from '@angular/router';
import { map } from 'rxjs';
import { SessionService } from './session.service';

export const authenticatedGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);
  return session.ensureLoaded().pipe(
    map(() => session.isAuthenticated() ? true : new RedirectCommand(router.createUrlTree(['/sign-in']), { state: { returnUrl: state.url } })),
  );
};
