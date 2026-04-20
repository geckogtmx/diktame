'use client';

import { useCoreArsenalScroll } from '@/lib/animations/useCoreArsenalScroll';
import { useTranslations } from 'next-intl';

export function CoreArsenalSection() {
  const { activePair } = useCoreArsenalScroll();
  const t = useTranslations('CoreArsenalSection');

  return (
    <div id="core-track" className="relative h-[220vh] scroll-mt-20">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        {/* Background Effects */}
        <div className="absolute inset-0 bg-background z-0" aria-hidden="true"></div>
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-primary/5 rounded-full blur-[160px] pointer-events-none" aria-hidden="true"></div>

        <div className="section-container relative z-10 w-full">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-5xl font-bold mb-4 text-white">{t('title')}</h2>
            <p className="text-muted">{t('subtitle')}</p>
          </div>

          <div className="flex flex-col gap-6 max-w-5xl mx-auto px-4">
            {/* Pillars: Dictate, Refine & Vision — the three core inputs */}
            <div
              id="core-pair-1"
              className={`flex flex-col md:flex-row gap-6 transition-all duration-1000 ease-out will-change-transform ${
                activePair >= 1 ? 'opacity-100 scale-100' : 'opacity-0 scale-[0.98]'
              }`}
            >
              {/* Pillar 1: Dictate (Voice as Input) */}
              <div id="core-card-1" className="card flex-1 p-5 md:p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Dictate Mode — voice input icon">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4M12 15a3 3 0 003-3V5a3 3 0 00-6 0v7a3 3 0 003 3z"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>{t('pillar01Label')}</span>
                  <span>{t('pillar01Hotkey')}</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">{t('pillar01Title')}</h3>
                <p className="text-sm text-muted leading-relaxed">
                  {t('pillar01Description')}
                </p>
              </div>

              {/* Pillar 2: Refine (Text Selection as Input) */}
              <div id="core-card-2" className="card flex-1 p-5 md:p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Refine Mode — text selection input icon">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>{t('pillar02Label')}</span>
                  <span>{t('pillar02Hotkey')}</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">{t('pillar02Title')}</h3>
                <p className="text-sm text-muted leading-relaxed">
                  {t('pillar02Description')}
                </p>
              </div>

              {/* Pillar 3: Vision (Screen as Input) */}
              <div id="core-card-3" className="card flex-1 p-5 md:p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Vision Mode — screen capture input icon">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>{t('pillar03Label')}</span>
                  <span>{t('pillar03Hotkey')}</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">{t('pillar03Title')}</h3>
                <p className="text-sm text-muted leading-relaxed">
                  {t('pillar03Description')}
                </p>
              </div>
            </div>

            {/* Flavors: Ask, Notes, Connectors teaser — compact row */}
            <div
              id="core-pair-2"
              className={`grid grid-cols-1 sm:grid-cols-3 gap-4 transition-all duration-1000 ease-out will-change-transform ${
                activePair >= 2 ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-6'
              }`}
            >
              <div className="flex items-start gap-3 bg-white/5 border border-white/10 rounded-xl p-4">
                <div className="w-8 h-8 bg-primary/10 rounded-lg flex items-center justify-center text-primary shrink-0 mt-0.5">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Ask Mode icon">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0012 18.75c-1.03 0-1.9.4-2.593 1.02l-.547.547z" />
                  </svg>
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-white">{t('flavor01Title')}</span>
                    <span className="text-[9px] font-mono text-muted border border-white/10 px-1.5 py-0.5 rounded">{t('flavor01Hotkey')}</span>
                  </div>
                  <p className="text-xs text-muted leading-relaxed">{t('flavor01Desc')}</p>
                </div>
              </div>

              <div className="flex items-start gap-3 bg-white/5 border border-white/10 rounded-xl p-4">
                <div className="w-8 h-8 bg-primary/10 rounded-lg flex items-center justify-center text-primary shrink-0 mt-0.5">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Notes Mode icon">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01" />
                  </svg>
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-white">{t('flavor02Title')}</span>
                    <span className="text-[9px] font-mono text-muted border border-white/10 px-1.5 py-0.5 rounded">{t('flavor02Hotkey')}</span>
                  </div>
                  <p className="text-xs text-muted leading-relaxed">{t('flavor02Desc')}</p>
                </div>
              </div>

              <div className="flex items-start gap-3 bg-white/5 border border-white/10 border-dashed rounded-xl p-4">
                <div className="w-8 h-8 bg-white/5 rounded-lg flex items-center justify-center text-muted shrink-0 mt-0.5">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24" role="img" aria-label="Connectors — coming soon">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2}
                      d="M10.172 13.828a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.102 1.101" />
                  </svg>
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2 mb-1">
                    <span className="text-sm font-bold text-white/40">{t('comingSoonTeaser')}</span>
                    <span className="text-[9px] font-mono text-muted/60 border border-white/5 px-1.5 py-0.5 rounded">Soon</span>
                  </div>
                  <p className="text-xs text-muted/60 leading-relaxed">{t('comingSoonDesc')}</p>
                </div>
              </div>
            </div>
          </div>

          {/* Progress Steps */}
          <div className="mt-10 flex justify-center gap-4">
            <div
              id="core-dot-1"
              className={`h-1 rounded-full transition-all duration-300 ${
                activePair >= 1 ? 'w-32 bg-primary' : 'w-24 bg-white/10'
              }`}
            ></div>
            <div
              id="core-dot-2"
              className={`h-1 rounded-full transition-all duration-300 ${
                activePair >= 2 ? 'w-32 bg-primary' : 'w-24 bg-white/10'
              }`}
            ></div>
          </div>
        </div>
      </div>
    </div>
  );
}
