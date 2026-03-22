'use client';

import { useTranslations } from 'next-intl';
import { useVoiceMacrosScroll } from '@/lib/animations/useVoiceMacrosScroll';

export function VoiceMacrosSection() {
  const t = useTranslations('VoiceMacrosSection');
  const { triggerProgress, outputProgress, arrowActive, macroIdx } = useVoiceMacrosScroll();

  const MACROS = [
    {
      trigger: t('macro1Trigger'),
      output: t('macro1Output'),
    },
    {
      trigger: t('macro2Trigger'),
      output: t('macro2Output'),
    },
  ];

  const macro = MACROS[macroIdx];
  const triggerChars = Math.floor(triggerProgress * macro.trigger.length);
  const outputChars = Math.floor(outputProgress * macro.output.length);
  const showTriggerCursor = triggerProgress > 0 && triggerProgress < 1;
  const showOutputCursor = outputProgress > 0 && outputProgress < 1;

  return (
    <div id="macros-track" className="relative h-[220vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30" aria-hidden="true"></div>
        <div className="section-container grid md:grid-cols-2 gap-6 md:gap-12 items-center relative z-10">
          {/* Text (Left) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
              {t('badge')}
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              {t('title')}
            </h2>
            <p className="text-lg md:text-xl text-muted mb-6 md:mb-8">
              {t('subtitle')}
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>{t('chip1Label')}</strong> — {t('chip1Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>{t('chip2Label')}</strong> — {t('chip2Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>{t('chip3Label')}</strong> — {t('chip3Desc')}
              </li>
            </ul>
          </div>

          {/* Card (Right) */}
          <div className="card border-primary/20 bg-black/50 p-4 md:p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4">
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">{t('cardLabel')}</span>
              <span className={`w-3 h-3 rounded-full transition-colors duration-300 ${
                triggerProgress > 0 ? 'bg-green-500 animate-pulse' : 'bg-white/20'
              }`}></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              {/* Voice Input */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-primary">●</span> {t('voiceInput')}
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded min-h-[36px]">
                  <span className="text-primary">
                    {macro.trigger.slice(0, triggerChars)}
                    {showTriggerCursor && (
                      <span className="inline-block w-[2px] h-3 bg-primary ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                </div>
              </div>

              {/* Arrow */}
              <div className={`text-center text-muted transition-opacity duration-300 ${
                arrowActive ? 'opacity-100' : 'opacity-30'
              }`}>
                ↓ {t('expandsTo')}
              </div>

              {/* Output */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-green-400">●</span> {t('injectedText')}
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded min-h-[100px]">
                  <span className="text-muted whitespace-pre-line">
                    {macro.output.slice(0, outputChars)}
                    {showOutputCursor && (
                      <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                </div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 text-center text-[10px] text-muted italic">
              &quot;{t('snippetsNote', { count: MACROS.length })}&quot;
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
