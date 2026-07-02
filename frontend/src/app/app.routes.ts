import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { roleGuard } from './core/guards/role.guard';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { LoginComponent } from './features/login/login.component';
import { AllSavingsComponent } from './features/savings/all-savings/all-savings.component';
import { MySavingsComponent } from './features/savings/my-savings/my-savings.component';
import { SavingsCreateComponent } from './features/savings/savings-create/savings-create.component';
import { StatisticsComponent } from './features/statistics/statistics.component';
import { AppLayoutComponent } from './layout/app-layout/app-layout.component';

export const routes: Routes = [
  {
    path: 'login',
    component: LoginComponent,
    canActivate: [guestGuard]
  },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        component: DashboardComponent
      },
      {
        path: 'savings/new',
        component: SavingsCreateComponent
      },
      {
        path: 'savings/my',
        component: MySavingsComponent
      },
      {
        path: 'savings/all',
        component: AllSavingsComponent,
        canActivate: [roleGuard],
        data: {
          roles: ['Fuehrungskraft', 'Admin']
        }
      },
      {
        path: 'statistics',
        component: StatisticsComponent
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
