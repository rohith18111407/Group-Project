import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideClientHydration, withEventReplay } from '@angular/platform-browser';
import { AuthService } from './services/auth.service';
import { AdminService } from './services/admin.service';

import { provideHttpClient, withInterceptorsFromDi, HttpInterceptor } from '@angular/common/http';
import { DashboardService } from './services/dashboard.service';
import { NotificationService } from './services/notification.service';
import { StaffService } from './services/staff.service';
import { StatisticsService } from './services/statistics.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes), provideClientHydration(withEventReplay()),
    provideHttpClient(),
    AuthService,
    AdminService, 
    DashboardService,
    NotificationService,
    StaffService,
    StatisticsService
  ]
};
