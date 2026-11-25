import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { TravelApi } from './api';
import { AuthPage } from './auth-page';
import { PlannerPage } from './planner-page';

const signedIn: CanActivateFn = async () => {
  const api = inject(TravelApi);
  const router = inject(Router);
  return await api.currentUser() ? true : router.createUrlTree(['/login']);
};

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: AuthPage, title: 'Sign in | Travel Planner' },
  { path: 'signup', component: AuthPage, data: { signup: true }, title: 'Create an account | Travel Planner' },
  { path: 'dashboard', component: PlannerPage, canActivate: [signedIn], title: 'Dashboard | Travel Planner' },
  { path: 'preview', component: PlannerPage, data: { preview: true }, title: 'Explore the planner | Travel Planner' },
  { path: '**', redirectTo: 'login' },
];
