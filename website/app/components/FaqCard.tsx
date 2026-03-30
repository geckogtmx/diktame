'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { trackFaqExpand } from '@/lib/analytics';

export function FaqCard() {
  const t = useTranslations('FaqCard');
  const [openIndex, setOpenIndex] = useState<number | null>(null);

  const faqs = [
    { q: t('q1'), a: t('a1') },
    { q: t('q2'), a: t('a2') },
    { q: t('q3'), a: t('a3') },
    { q: t('q4'), a: t('a4') },
    { q: t('q5'), a: t('a5') },
  ];

  return (
    <div className="w-full py-16 md:py-24">
      <div className="max-w-3xl mx-auto px-4">
        <div className="text-center mb-10">
          <h2 className="text-2xl md:text-4xl font-bold text-white mb-3">{t('title')}</h2>
          <p className="text-muted text-sm">{t('subtitle')}</p>
        </div>

        <div className="space-y-2">
          {faqs.map((faq, i) => (
            <button
              key={i}
              onClick={() => { const opening = openIndex !== i; setOpenIndex(opening ? i : null); if (opening) trackFaqExpand(faq.q); }}
              className="w-full text-left"
            >
              <div className="rounded-lg border border-white/5 bg-white/[0.02] hover:bg-white/[0.04] transition-colors">
                <div className="flex items-center justify-between px-5 py-4">
                  <span className="text-sm md:text-base font-medium text-white pr-4">{faq.q}</span>
                  <span className={`text-muted text-lg transition-transform ${openIndex === i ? 'rotate-45' : ''}`}>+</span>
                </div>
                {openIndex === i && (
                  <div className="px-5 pb-4 text-sm text-muted leading-relaxed">
                    {faq.a}
                  </div>
                )}
              </div>
            </button>
          ))}
        </div>

        <div className="text-center mt-8">
          <Link href="/faq" className="text-sm text-primary hover:underline">
            {t('moreLink')}
          </Link>
        </div>
      </div>
    </div>
  );
}
