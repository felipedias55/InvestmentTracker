import { LOCALE_ID } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localePt from '@angular/common/locales/pt';

registerLocaleData(localePt, 'pt-BR');

export const brazilianLocaleProvider = { provide: LOCALE_ID, useValue: 'pt-BR' };
