import { brazilianLocaleProvider } from './app/core/locale';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { App } from './app/app';
import { routes } from './app/app.routes';

bootstrapApplication(App, {
  providers: [brazilianLocaleProvider, provideHttpClient(), provideRouter(routes)],
}).catch((err) => console.error(err));
