import { Routes } from '@angular/router';
import { BILLING_ROUTES } from '@georgevictorkamal/kinevor-payments-billing-web';

export const appRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'billing' },
  { path: '', children: BILLING_ROUTES },
  { path: '**', redirectTo: 'billing' },
];
