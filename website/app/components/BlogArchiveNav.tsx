'use client';

// Month archive navigation for the blog sidebar. Groups posts by
// year → month with counts; each year is collapsible. Clicking a
// month sets `?month=YYYY-MM` on the blog index, which server-side
// filters to that month.

import { useState } from 'react';
import { Link } from '@/i18n/navigation';
import { useTranslations, useLocale } from 'next-intl';

export type ArchiveMonth = {
  key: string; // YYYY-MM
  label: string; // e.g. "April 2026" / "abril 2026"
  count: number;
};

export type ArchiveYear = {
  year: string;
  total: number;
  months: ArchiveMonth[];
};

export function BlogArchiveNav({
  years,
  activeMonth,
}: {
  years: ArchiveYear[];
  activeMonth: string | null;
}) {
  const t = useTranslations('BlogPage');
  const locale = useLocale();
  const currentYear = activeMonth ? activeMonth.slice(0, 4) : years[0]?.year;
  const [open, setOpen] = useState<Record<string, boolean>>(() => {
    const initial: Record<string, boolean> = {};
    if (currentYear) initial[currentYear] = true;
    return initial;
  });

  if (years.length === 0) return null;

  return (
    <div>
      <h4 className="text-xs text-gray-500 uppercase tracking-wider mb-3">
        {t('archive')}
      </h4>
      <ul className="space-y-1">
        {years.map((y) => {
          const isOpen = !!open[y.year];
          return (
            <li key={y.year}>
              <button
                type="button"
                onClick={() => setOpen((prev) => ({ ...prev, [y.year]: !prev[y.year] }))}
                className="flex w-full items-center justify-between text-sm text-gray-300 hover:text-white transition-colors py-1"
                aria-expanded={isOpen}
              >
                <span className="font-semibold">{y.year}</span>
                <span className="text-xs text-gray-500">{isOpen ? '−' : '+'} {y.total}</span>
              </button>
              {isOpen && (
                <ul className="ml-3 mt-1 space-y-1 border-l border-gray-800 pl-3">
                  {y.months.map((m) => {
                    const active = activeMonth === m.key;
                    return (
                      <li key={m.key}>
                        <Link
                          href={`/blog?month=${m.key}`}
                          locale={locale}
                          className={`flex items-center justify-between text-sm py-1 transition-colors ${
                            active
                              ? 'text-orange-400 font-semibold'
                              : 'text-gray-400 hover:text-white'
                          }`}
                        >
                          <span>{m.label}</span>
                          <span className="text-xs text-gray-500">{m.count}</span>
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
