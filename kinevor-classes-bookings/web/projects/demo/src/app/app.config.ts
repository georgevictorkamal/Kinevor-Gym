import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { provideClassesBookings } from '@georgevictorkamal/kinevor-classes-bookings-web';
import { appRoutes } from './app.routes';
import { apiBaseUrl } from './runtime-config';
import { demoApiInterceptor } from './demo-api.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([demoApiInterceptor])),
    provideClassesBookings({
      apiBaseUrl: apiBaseUrl,
      memberName: 'Maya Thompson',
    }),
  ],
};
