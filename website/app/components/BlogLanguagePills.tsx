'use client';

import { useLocale } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';
import { useTransition } from 'react';
import { trackLanguageSwitch } from '@/lib/analytics';

export function BlogLanguagePills() {
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const [isPending, startTransition] = useTransition();

  function switchTo(target: 'en' | 'es') {
    if (target === locale) return;
    trackLanguageSwitch(locale, target);
    startTransition(() => {
      router.replace(pathname, { locale: target });
    });
  }

  return (
    <div className="flex items-center gap-1">
      <button
        onClick={() => switchTo('en')}
        disabled={isPending}
        className={`px-3 py-1.5 rounded-full text-xs font-medium transition-colors disabled:opacity-50 ${
          locale === 'en'
            ? 'bg-orange-500 text-white'
            : 'bg-gray-800 text-gray-400 hover:text-white'
        }`}
      >
        EN
      </button>
      <button
        onClick={() => switchTo('es')}
        disabled={isPending}
        className={`px-3 py-1.5 rounded-full text-xs font-medium transition-colors disabled:opacity-50 ${
          locale === 'es'
            ? 'bg-orange-500 text-white'
            : 'bg-gray-800 text-gray-400 hover:text-white'
        }`}
      >
        ES
      </button>
    </div>
  );
}
