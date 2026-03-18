'use client';

import { useTranslations } from 'next-intl';
import { useTokensScroll } from '@/lib/animations/useTokensScroll';

const KEY_TEXT: Record<string, string> = {
  anthropic: 'sk-ant-••••••••••••••••',
  google: 'AIzaSy••••••••••••••••',
};

export function TokensSection() {
  const t = useTranslations('TokensSection');
  const { modelIdx, modelProgress, keyProgress } = useTokensScroll();

  const MODELS = [
    { name: t('model1'), tag: t('model1Tag'), activeKey: null },
    { name: t('model2'), tag: t('model2Tag'), activeKey: 'anthropic' as const },
    { name: t('model3'), tag: t('model3Tag'), activeKey: 'google' as const },
  ];

  const model = MODELS[modelIdx];
  const modelText = `${model.name} (${model.tag})`;
  const modelChars = Math.floor(modelProgress * modelText.length);
  const isCloud = model.tag !== t('model1Tag');
  const isTypingModel = modelProgress > 0 && modelProgress < 1;

  const activeKeyTarget = model.activeKey ? KEY_TEXT[model.activeKey] : null;
  const keyChars = activeKeyTarget ? Math.floor(keyProgress * activeKeyTarget.length) : 0;
  const isTypingKey = activeKeyTarget && keyProgress > 0 && keyProgress < 1;

  return (
    <div id="tokens-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30" aria-hidden="true"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Card (Left) */}
          <div className="card border-primary/20 bg-black/50 p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4 order-last md:order-first">
            <div className="absolute -top-3 -right-3 w-24 h-24 bg-primary/20 blur-2xl rounded-full" aria-hidden="true"></div>
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">{t('cardLabel')}</span>
              <span className={`w-3 h-3 rounded-full transition-colors duration-500 ${
                modelProgress === 0 ? 'bg-white/20' : isCloud ? 'bg-green-500 animate-pulse' : 'bg-yellow-500 animate-pulse'
              }`}></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              {/* Active Model — scroll-driven typewriter */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase">{t('activeModel')}</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded flex justify-between items-center min-h-[32px]">
                  <span className="text-primary">
                    {modelText.slice(0, modelChars)}
                    {isTypingModel && (
                      <span className="inline-block w-[2px] h-3 bg-primary ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                  <span className="text-muted">▼</span>
                </div>
              </div>

              {/* Anthropic Key — lights up when claude is active */}
              <div className={`flex flex-col gap-2 transition-opacity duration-500 ${
                model.activeKey === 'anthropic' ? 'opacity-100' : 'opacity-40'
              }`}>
                <label className="text-[10px] text-muted uppercase">{t('anthropicKey')}</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted min-h-[32px]">
                  {model.activeKey === 'anthropic' ? (
                    <>
                      {KEY_TEXT.anthropic.slice(0, keyChars)}
                      {isTypingKey && (
                        <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                      )}
                    </>
                  ) : (
                    t('anthropicPlaceholder')
                  )}
                </div>
              </div>

              {/* Google Key — lights up when gemini is active */}
              <div className={`flex flex-col gap-2 transition-opacity duration-500 ${
                model.activeKey === 'google' ? 'opacity-100' : 'opacity-40'
              }`}>
                <label className="text-[10px] text-muted uppercase">{t('googleKey')}</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted min-h-[32px]">
                  {model.activeKey === 'google' ? (
                    <>
                      {KEY_TEXT.google.slice(0, keyChars)}
                      {isTypingKey && (
                        <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                      )}
                    </>
                  ) : (
                    t('googlePlaceholder')
                  )}
                </div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 text-center text-[10px] text-muted italic">
              &quot;{t('encryptionNote')}&quot;
            </div>
          </div>

          {/* Text (Right) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
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
                <span className="text-orange-400">✓</span> <strong>{t('chip1Label')}</strong> - {t('chip1Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>{t('chip2Label')}</strong> - {t('chip2Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>{t('chip3Label')}</strong> - {t('chip3Desc')}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
