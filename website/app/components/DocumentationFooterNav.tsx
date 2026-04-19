import Link from 'next/link';
import { getTranslations } from 'next-intl/server';
import { getAdjacentDocs } from '@/lib/docs-navigation';

interface Props {
  slugPath: string;
  locale: string;
}

export async function DocumentationFooterNav({ slugPath, locale }: Props) {
  const { prev, next } = getAdjacentDocs(slugPath);
  if (!prev && !next) return null;

  const t = await getTranslations({ locale, namespace: 'DocumentationSidebar' });
  const tNav = await getTranslations({ locale, namespace: 'DocsFooterNav' });

  return (
    <nav
      aria-label={tNav('ariaLabel')}
      className="mt-12 pt-6 border-t border-white/10 grid grid-cols-1 sm:grid-cols-2 gap-3"
    >
      {prev ? (
        <Link
          href={`/${locale}${prev.href}`}
          className="group flex flex-col gap-1 p-4 rounded-lg border border-white/10 bg-gray-900/30 hover:bg-gray-900/60 hover:border-blue-500/40 transition-colors"
        >
          <span className="text-xs text-gray-500 flex items-center gap-1">
            <svg className="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
            {tNav('previous')}
          </span>
          <span className="text-base font-semibold text-gray-200 group-hover:text-blue-400 transition-colors">
            {t(prev.labelKey)}
          </span>
        </Link>
      ) : (
        <div />
      )}
      {next ? (
        <Link
          href={`/${locale}${next.href}`}
          className="group flex flex-col gap-1 p-4 rounded-lg border border-white/10 bg-gray-900/30 hover:bg-gray-900/60 hover:border-blue-500/40 transition-colors text-right"
        >
          <span className="text-xs text-gray-500 flex items-center gap-1 justify-end">
            {tNav('next')}
            <svg className="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
            </svg>
          </span>
          <span className="text-base font-semibold text-gray-200 group-hover:text-blue-400 transition-colors">
            {t(next.labelKey)}
          </span>
        </Link>
      ) : (
        <div />
      )}
    </nav>
  );
}
