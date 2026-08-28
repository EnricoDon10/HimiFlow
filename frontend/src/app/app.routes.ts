import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';
import { passwordChangeGuard } from './core/guards/password-change.guard';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { LoginComponent } from './features/login/login.component';
import { AllSavingsComponent } from './features/savings/all-savings/all-savings.component';
import { MySavingsComponent } from './features/savings/my-savings/my-savings.component';
import { SavingsCreateComponent } from './features/savings/savings-create/savings-create.component';
import { StatisticsComponent } from './features/statistics/statistics.component';
import { AppLayoutComponent } from './layout/app-layout/app-layout.component';
import { ChangePasswordComponent } from './features/change-password/change-password.component';

export const routes: Routes = [
  {
    path: 'legal',
    loadComponent: () => import('./features/legal/legal.component').then((m) => m.LegalComponent)
  },
  {
    path: 'login',
    component: LoginComponent,
    canActivate: [guestGuard]
  },
  {
    path: 'change-password',
    component: ChangePasswordComponent,
    canActivate: [passwordChangeGuard]
  },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        component: DashboardComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mitarbeiter', 'FachAdmin'] }
      },
      {
        path: 'savings/new',
        component: SavingsCreateComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mitarbeiter', 'FachAdmin'] }
      },
      {
        path: 'savings/my',
        component: MySavingsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mitarbeiter', 'FachAdmin'] }
      },
      {
        path: 'savings/all',
        component: AllSavingsComponent,
        canActivate: [roleGuard],
        data: {
          roles: ['FachAdmin']
        }
      },
      {
        path: 'admin/users',
        canActivate: [roleGuard],
        data: { roles: ['SystemAdmin'] },
        loadComponent: () => import('./features/admin/user-management/user-management.component').then((m) => m.UserManagementComponent)
      },
      {
        path: 'admin/license',
        canActivate: [roleGuard],
        data: { roles: ['SystemAdmin'] },
        loadComponent: () => import('./features/admin/license/license.component').then((m) => m.LicenseComponent)
      },
      {
        path: 'admin/product-groups',
        canActivate: [roleGuard],
        data: { roles: ['FachAdmin'] },
        loadComponent: () => import('./features/admin/product-groups/product-groups.component').then((m) => m.ProductGroupsComponent)
      },

      {
        path: 'statistics',
        component: StatisticsComponent,
        canActivate: [roleGuard],
        data: { roles: ['Mitarbeiter', 'FachAdmin'] }
      },
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard'
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];

