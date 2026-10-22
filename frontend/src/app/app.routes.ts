import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { chiefLeaderGuard } from './core/auth/chief-leader.guard';
import { leaderGuard } from './core/auth/leader.guard';
import { HomeComponent } from './pages/home.component';
import { SignInComponent } from './pages/sign-in.component';
import { AccessDeniedComponent } from './pages/access-denied.component';
import { pendingChangesGuard } from './core/routing/pending-changes.guard';
import { authenticatedGuard } from './core/auth/authenticated.guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'sign-in', component: SignInComponent },
  { path: 'register', loadComponent: () => import('./features/accounts/register.component').then(m => m.RegisterComponent) },
  { path: 'admin/accounts', canActivate: [adminGuard], loadComponent: () => import('./features/accounts/account-admin.component').then(m => m.AccountAdminComponent) },
  { path: 'admin/feedback', canActivate: [adminGuard], loadComponent: () => import('./features/engagement/feedback-admin.component').then(m => m.FeedbackAdminComponent) },
  { path: 'admin/incidents', canActivate: [adminGuard], loadComponent: () => import('./features/engagement/incident-admin.component').then(m => m.IncidentAdminComponent) },
  { path: 'events/:eventId/feedback', canActivate: [authenticatedGuard], loadComponent: () => import('./features/engagement/event-feedback.component').then(m => m.EventFeedbackComponent) },
  { path: 'events/:eventId/incidents', canActivate: [authenticatedGuard], loadComponent: () => import('./features/engagement/event-incidents.component').then(m => m.EventIncidentsComponent) },
  { path: 'access-denied', component: AccessDeniedComponent },
  {
    path: 'dashboard',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/operational-dashboard/operational-dashboard.component').then(m => m.OperationalDashboardComponent),
  },
  {
    path: 'events/:eventId/cancel',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-cancellation/event-cancellation.component').then(m => m.EventCancellationComponent),
  },
  {
    path: 'notifications',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./features/notifications/notification-inbox.component').then(m => m.NotificationInboxComponent),
  },
  {
    path: 'profile',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/leader-history/leader-history.component').then(m => m.LeaderHistoryComponent),
  },
  {
    path: 'events/:eventId/leaders/:leaderId/history',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/leader-history/leader-history.component').then(m => m.LeaderHistoryComponent),
  },
  {
    path: 'events/:eventId/availability',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/availability/components/availability-declaration/availability-declaration.component').then(m => m.AvailabilityDeclarationComponent),
  },
  {
    path: 'events/:eventId/my-assignment',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/volunteer-assignment/components/volunteer-assignment.component').then(m => m.VolunteerAssignmentComponent),
  },
  {
    path: 'events/:eventId/team-selection',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/team-selection/components/team-selection.component').then(m => m.TeamSelectionComponent),
  },
  {
    path: 'events/:eventId/replacement-assignment',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/replacement-assignment/components/replacement-assignment.component').then(m => m.ReplacementAssignmentComponent),
  },
  {
    path: 'events/:eventId/reschedule',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-reschedule/components/reschedule-dialog/reschedule-dialog.component').then(m => m.RescheduleDialogComponent),
  },
  {
    path: 'events/:eventId/complete',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-completion/components/complete-event-dialog/complete-event-dialog.component').then(m => m.CompleteEventDialogComponent),
  },
  {
    path: 'events/:eventId/attendance',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/attendance/components/attendance-roll-sheet/attendance-roll-sheet.component').then(m => m.AttendanceRollSheetComponent),
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
