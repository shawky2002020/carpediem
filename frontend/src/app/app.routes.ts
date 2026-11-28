import { isDevMode } from '@angular/core';
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
  { path: '', title: 'Home · Carpediem', component: HomeComponent },
  { path: 'sign-in', title: 'Sign in · Carpediem', component: SignInComponent },
  { path: 'register', title: 'Register as a Leader · Carpediem', loadComponent: () => import('./features/accounts/register.component').then(m => m.RegisterComponent) },
  {
    path: 'admin/accounts', canActivate: [adminGuard], children: [
      { path: '', title: 'Accounts · Carpediem', loadComponent: () => import('./features/accounts/account-admin.component').then(m => m.AccountAdminComponent) },
      { path: ':userId', title: 'Account details · Carpediem', canDeactivate: [pendingChangesGuard],
        loadComponent: () => import('./features/accounts/account-detail.component').then(m => m.AccountDetailComponent) },
    ],
  },
  { path: 'admin/feedback', title: 'Feedback review · Carpediem', canActivate: [adminGuard], loadComponent: () => import('./features/engagement/feedback-admin.component').then(m => m.FeedbackAdminComponent) },
  { path: 'admin/incidents', title: 'Incident review · Carpediem', canActivate: [adminGuard], loadComponent: () => import('./features/engagement/incident-admin.component').then(m => m.IncidentAdminComponent) },
  { path: 'events/:eventId/feedback', title: 'Event feedback · Carpediem', canActivate: [authenticatedGuard], loadComponent: () => import('./features/engagement/event-feedback.component').then(m => m.EventFeedbackComponent) },
  { path: 'events/:eventId/incidents', title: 'Event incidents · Carpediem', canActivate: [authenticatedGuard], loadComponent: () => import('./features/engagement/event-incidents.component').then(m => m.EventIncidentsComponent) },
  { path: 'access-denied', title: 'Access restricted · Carpediem', component: AccessDeniedComponent },
  {
    path: 'dashboard',
    title: 'Operational dashboard · Carpediem',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/operational-dashboard/operational-dashboard.component').then(m => m.OperationalDashboardComponent),
  },
  {
    path: 'events/:eventId/cancel',
    title: 'Cancel event · Carpediem',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-cancellation/event-cancellation.component').then(m => m.EventCancellationComponent),
  },
  {
    path: 'notifications',
    title: 'Notifications · Carpediem',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./features/notifications/notification-inbox.component').then(m => m.NotificationInboxComponent),
  },
  {
    path: 'profile',
    title: 'My profile · Carpediem',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/leader-history/leader-history.component').then(m => m.LeaderHistoryComponent),
  },
  {
    path: 'events/:eventId/leaders/:leaderId/history',
    title: 'Leader history · Carpediem',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/leader-history/leader-history.component').then(m => m.LeaderHistoryComponent),
  },
  {
    path: 'events/:eventId/availability',
    title: 'Event availability · Carpediem',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/availability/components/availability-declaration/availability-declaration.component').then(m => m.AvailabilityDeclarationComponent),
  },
  {
    path: 'events/:eventId/my-assignment',
    title: 'My event assignment · Carpediem',
    canActivate: [leaderGuard],
    loadComponent: () => import('./features/volunteer-assignment/components/volunteer-assignment.component').then(m => m.VolunteerAssignmentComponent),
  },
  {
    path: 'events/:eventId/team-selection',
    title: 'Team selection · Carpediem',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/team-selection/components/team-selection.component').then(m => m.TeamSelectionComponent),
  },
  {
    path: 'events/:eventId/replacement-assignment',
    title: 'Replacement assignment · Carpediem',
    canActivate: [chiefLeaderGuard],
    loadComponent: () => import('./features/replacement-assignment/components/replacement-assignment.component').then(m => m.ReplacementAssignmentComponent),
  },
  {
    path: 'events/:eventId/reschedule',
    title: 'Reschedule event · Carpediem',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-reschedule/components/reschedule-dialog/reschedule-dialog.component').then(m => m.RescheduleDialogComponent),
  },
  {
    path: 'events/:eventId/complete',
    title: 'Complete event · Carpediem',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/event-completion/components/complete-event-dialog/complete-event-dialog.component').then(m => m.CompleteEventDialogComponent),
  },
  {
    path: 'events/:eventId/attendance',
    title: 'Attendance · Carpediem',
    canActivate: [chiefLeaderGuard],
    canDeactivate: [pendingChangesGuard],
    loadComponent: () => import('./features/attendance/components/attendance-roll-sheet/attendance-roll-sheet.component').then(m => m.AttendanceRollSheetComponent),
  },
  {
    path: 'my-appointments',
    title: 'My appointments · Carpediem',
    canActivate: [chiefLeaderGuard],
    children: [
      { path: '', loadComponent: () => import('./features/chief-leader-response/chief-leader-response.component').then(m => m.ChiefLeaderResponseComponent) },
      { path: ':id', title: 'Review appointment · Carpediem', loadComponent: () => import('./features/chief-leader-response/chief-leader-response.component').then(m => m.ChiefLeaderResponseComponent) },
    ],
  },
  {
    path: 'draft-events',
    title: 'Draft events · Carpediem',
    canActivate: [adminGuard],
    children: [
      { path: '', loadComponent: () => import('./features/draft-events/pages/draft-list/draft-list.component').then(m => m.DraftListComponent) },
      { path: 'new', title: 'Create draft event · Carpediem', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/draft-events/pages/draft-create/draft-create.component').then(m => m.DraftCreateComponent) },
      { path: ':id/chief-leader', title: 'Appoint Chief Leader · Carpediem', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/chief-leader-appointment/pages/appointment/appointment.component').then(m => m.AppointmentComponent) },
      { path: ':id', title: 'Edit draft event · Carpediem', canDeactivate: [pendingChangesGuard], loadComponent: () => import('./features/draft-events/pages/draft-edit/draft-edit.component').then(m => m.DraftEditComponent) },
    ],
  },
  ...(isDevMode() ? [{ path: 'design-lab', title: 'Component field guide', loadComponent: () => import('./design-lab/design-lab.component').then(m => m.DesignLabComponent) }] : []),
  { path: '**', redirectTo: '' },
];
