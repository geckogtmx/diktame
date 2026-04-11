import { defineRouting } from 'next-intl/routing';
import { locales, defaultLocale } from './config';

export const routing = defineRouting({
  locales,
  defaultLocale,
  localePrefix: 'always',
  localeCookie: { name: 'NEXT_LOCALE', maxAge: 31536000 },
});
