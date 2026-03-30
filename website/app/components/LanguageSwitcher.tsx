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

  function toggleLocale() {
    const nextLocale = locale === 'en' ? 'es' : 'en';
    trackLanguageSwitch(locale, nextLocale);
    startTransition(() => {
      router.replace(pathname, { locale: nextLocale });
    });
  }

  return (
    <button
      onClick={toggleLocale}
      disabled={isPending}
      className="text-xs font-medium px-2.5 py-1.5 rounded-lg border border-white/10 hover:border-white/30 text-muted hover:text-white transition-all disabled:opacity-50"
      aria-label={locale === 'en' ? 'Cambiar a español' : 'Switch to English'}
    >
      {locale === 'en' ? 'ES' : 'EN'}
    </button>
  );
}
