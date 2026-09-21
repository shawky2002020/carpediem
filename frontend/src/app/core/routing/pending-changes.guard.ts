import { CanDeactivateFn } from '@angular/router';

export interface HasPendingChanges { canDeactivate(): boolean; }

export const pendingChangesGuard: CanDeactivateFn<HasPendingChanges> = component => component.canDeactivate();
