import { Routes } from '@angular/router';
import { adminGuard } from './core/auth/admin.guard';
import { HomeComponent } from './pages/home.component';
import { SignInComponent } from './pages/sign-in.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'sign-in', component: SignInComponent },
  {
    path: 'draft-events',
    canActivate: [adminGuard],
    loadComponent: () => import('./pages/draft-events-placeholder.component').then(m => m.DraftEventsPlaceholderComponent),
  },
  { path: '**', redirectTo: '' },
];
