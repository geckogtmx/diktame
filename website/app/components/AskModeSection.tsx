'use client';

import { useTranslations } from 'next-intl';
import { useAskModeScroll } from '@/lib/animations/useAskModeScroll';

export function AskModeSection() {
  const t = useTranslations('AskModeSection');
  const { inputWidth, outputWidth, showInputCursor, showOutputCursor } = useAskModeScroll();

  return (
    <div id="ask-track" className="relative h-[200vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30" aria-hidden="true"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Card (Left) */}
          <div className="card border-primary/20 bg-black/50 p-8 font-mono text-sm leading-loose shadow-2xl order-last md:order-first relative overflow-hidden">
            <div className="absolute -top-3 -right-3 w-24 h-24 bg-primary/20 blur-2xl rounded-full" aria-hidden="true"></div>
            <div className="text-muted mb-4 border-b border-white/10 pb-2 flex justify-between">
              <span>{t('inputLabel')}</span>
              <span className="text-xs border border-white/20 px-2 py-0.5 rounded text-muted">{t('inputHotkey')}</span>
            </div>
            <div
              className={`text-secondary mb-8 overflow-hidden whitespace-nowrap ${
                showInputCursor ? 'border-r-2 border-secondary' : ''
              }`}
              id="ask-input"
              style={{ width: `${inputWidth}%` }}
            >
              &quot;{t('inputExample')}&quot;
            </div>
            <div className="text-muted mb-4 border-b border-white/10 pb-2">{t('outputLabel')}</div>
            <div
              className={`text-primary overflow-hidden whitespace-nowrap ${
                showOutputCursor ? 'border-r-2 border-primary' : ''
              }`}
              id="ask-output"
              style={{ width: `${outputWidth}%` }}
            >
              &quot;{t('outputExample')}&quot;
            </div>
          </div>

          {/* Text (Right) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-primary/10 rounded-full text-primary text-xs font-mono">
              {t('badge')}
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              {t('title')}
            </h2>
            <p className="text-xl text-muted mb-8">
              {t('subtitle')}
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip1Label')}</strong> - {t('chip1Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip2Label')}</strong> - {t('chip2Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip3Label')}</strong> - {t('chip3Desc')}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
