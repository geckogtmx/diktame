'use client';

import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { trackExternalLink } from '@/lib/analytics';

export function Footer() {
  const t = useTranslations('Footer');
  return (
    <footer className="py-12 border-t border-white/5 bg-surface/30">
      <div className="section-container flex flex-col md:flex-row justify-between items-center gap-6">
        <div className="flex flex-col gap-2">
          <div className="text-muted text-sm">{t('copyright')}</div>
          <div className="text-[10px] text-muted/50 font-mono uppercase tracking-[0.2em]">
            {t('credit')}
          </div>
        </div>
        <nav aria-label="Footer navigation" className="flex gap-6 text-sm">
          <Link href="/about" className="text-muted hover:text-white transition-colors">
            {t('about')}
          </Link>
          <Link href="/privacy" className="text-muted hover:text-white transition-colors">
            {t('privacy')}
          </Link>
          <Link href="/terms" className="text-muted hover:text-white transition-colors">
            {t('terms')}
          </Link>
          <Link href="/roadmap" className="text-muted hover:text-white transition-colors">
            {t('roadmap')}
          </Link>
          <a
            href="https://github.com/geckogtmx/diktame"
            target="_blank"
            rel="noopener noreferrer"
            onClick={() => trackExternalLink('https://github.com/geckogtmx/diktame', 'github')}
            className="text-muted hover:text-white transition-colors"
          >
            {t('github')}
          </a>
        </nav>
      </div>
    </footer>
  );
}
