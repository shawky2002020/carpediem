import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { chiefLeaderGuard } from './core/auth/chief-leader.guard';
import { leaderGuard } from './core/auth/leader.guard';
import { HomeComponent } from './pages/home.component';
import { SignInComponent } from './pages/sign-in.component';
import { AccessDeniedComponent } from './pages/access-denied.component';
import { pendingChangesGuard } from './core/routing/pending-changes.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'sign-in', component: SignInComponent },
  { path: 'access-denied', component: AccessDeniedComponent },
  {
    path: 'events/:eventId/availability',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/availability/components/availability-declaration/availability-declaration.component').then(m => m.AvailabilityDeclarationComponent),
  },
  {
    path: 'events/:eventId/team-selection',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/team-selection/components/team-selection.component').then(m => m.TeamSelectionComponent),
  },
  {
    path: 'my-appointments',
    canActivate: [chiefLeaderGuard],
    children: [
      { path: '', loadComponent: () => import('./features/chief-leader-response/chief-leader-response.component').then(m => m.ChiefLeaderResponseComponent) },
      { path: ':id', loadComponent: () => import('./features/chief-leader-response/chief-leader-response.component').then(m => m.ChiefLeaderResponseComponent) },
    ],
  },
  {
    path: 'draft-events',
    canActivate: [adminGuard],
    children: [
      { path: '', loadComponent: () => import('./features/draft-events/pages/draft-list/draft-list.component').then(m => m.DraftListComponent) },
      { path: 'new', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/draft-events/pages/draft-create/draft-create.component').then(m => m.DraftCreateComponent) },
      { path: ':id/chief-leader', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/chief-leader-appointment/pages/appointment/appointment.component').then(m => m.AppointmentComponent) },
      { path: ':id', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/draft-events/pages/draft-edit/draft-edit.component').then(m => m.DraftEditComponent) },
    ],
  },
  { path: '**', redirectTo: '' },
];
