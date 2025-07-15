import { Routes } from '@angular/router';
import { LoginComponent } from './login/login';
import { SignupComponent } from './signup/signup';
import { AdminDashboardComponent } from './admin/admin-dashboard/admin-dashboard';
import { StaffDashboardComponent } from './staff/staff-dashboard/staff-dashboard';


export const routes: Routes = [
    { path: '', redirectTo: 'login', pathMatch: 'full' },
    { path: 'login', component: LoginComponent },
    { path: 'signup', component: SignupComponent },
    { path: 'admin/dashboard', component: AdminDashboardComponent },
    { path: 'staff/dashboard', component: StaffDashboardComponent },
];
