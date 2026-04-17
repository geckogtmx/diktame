// Previous / Next page controls for the paginated blog index.
// Server component — renders <Link>s with the current query params.

import { Link } from '@/i18n/navigation';
import { getTranslations } from 'next-intl/server';

export async function BlogPagination({
  locale,
  currentPage,
  totalPages,
  month,
}: {
  locale: string;
  currentPage: number;
  totalPages: number;
  month: string | null;
}) {
  const t = await getTranslations({ locale, namespace: 'BlogPage' });

  if (totalPages <= 1) return null;

  const buildHref = (page: number): string => {
    const params = new URLSearchParams();
    if (page > 1) params.set('page', String(page));
    if (month) params.set('month', month);
    const qs = params.toString();
    return qs ? `/blog?${qs}` : `/blog`;
  };

  const hasPrev = currentPage > 1;
  const hasNext = currentPage < totalPages;

  return (
    <nav
      className="mt-16 flex items-center justify-between border-t border-gray-800 pt-8"
      aria-label="Pagination"
    >
      <div>
        {hasPrev ? (
          <Link
            href={buildHref(currentPage - 1)}
            className="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors"
          >
            <span aria-hidden>&larr;</span> {t('previous')}
          </Link>
        ) : (
          <span className="text-sm text-gray-600 inline-flex items-center gap-2">
            <span aria-hidden>&larr;</span> {t('previous')}
          </span>
        )}
      </div>

      <div className="text-sm text-gray-500">
        {t('pageOf', { current: currentPage, total: totalPages })}
      </div>

      <div>
        {hasNext ? (
          <Link
            href={buildHref(currentPage + 1)}
            className="inline-flex items-center gap-2 text-sm text-gray-400 hover:text-white transition-colors"
          >
            {t('next')} <span aria-hidden>&rarr;</span>
          </Link>
        ) : (
          <span className="text-sm text-gray-600 inline-flex items-center gap-2">
            {t('next')} <span aria-hidden>&rarr;</span>
          </span>
        )}
      </div>
    </nav>
  );
}
