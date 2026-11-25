import { registerLocaleData } from '@angular/common';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';
import localeEnIn from '@angular/common/locales/en-IN';
import { LOCALE_ID } from '@angular/core';
import { provideRouter } from '@angular/router';
import { App } from './app/app';
import { routes } from './app/routes';

registerLocaleData(localeEnIn);
bootstrapApplication(App, {
  providers: [provideHttpClient(), provideRouter(routes), { provide: LOCALE_ID, useValue: 'en-IN' }],
}).catch(console.error);
