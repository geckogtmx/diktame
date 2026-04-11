'use client';

import { useLocale } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';
import { useTransition } from 'react';
import { trackLanguageSwitch } from '@/lib/analytics';

export function LanguageSwitcher() {
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const [isPending, startTransition] = useTransition();

  function switchLocale(nextLocale: 'en' | 'es') {
    if (nextLocale === locale) return;
    trackLanguageSwitch(locale, nextLocale);
    document.cookie = `NEXT_LOCALE=${nextLocale}; path=/; max-age=31536000; SameSite=Lax`;
    startTransition(() => {
      router.replace(pathname, { locale: nextLocale });
    });
  }

  return (
    <div
      className="flex items-center gap-0 text-xs font-medium rounded-lg border border-white/10 overflow-hidden"
      aria-label="Language / Idioma"
    >
      <button
        onClick={() => switchLocale('en')}
        disabled={isPending}
        className={`px-2.5 py-1.5 transition-all disabled:opacity-50 ${
          locale === 'en'
            ? 'bg-white/10 text-white'
            : 'text-muted hover:text-white hover:bg-white/5'
        }`}
        aria-label="Switch to English"
        aria-pressed={locale === 'en'}
      >
        EN
      </button>
      <span className="w-px h-3 bg-white/10" aria-hidden="true" />
      <button
        onClick={() => switchLocale('es')}
        disabled={isPending}
        className={`px-2.5 py-1.5 transition-all disabled:opacity-50 ${
          locale === 'es'
            ? 'bg-white/10 text-white'
            : 'text-muted hover:text-white hover:bg-white/5'
        }`}
        aria-label="Cambiar a español"
        aria-pressed={locale === 'es'}
      >
        ES
      </button>
    </div>
  );
}
