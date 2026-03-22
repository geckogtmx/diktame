'use client';

import { useTranslations } from 'next-intl';
import { useQuickChatScroll } from '@/lib/animations/useQuickChatScroll';

export function QuickChatSection() {
  const t = useTranslations('QuickChatSection');
  const {
    userMsg1Progress,
    aiMsg1Progress,
    userMsg2Progress,
    thinkingVisible,
    aiMsg2Progress,
  } = useQuickChatScroll();

  const USER_MSG_1 = t('userMsg1');
  const USER_MSG_2 = t('userMsg2');

  const user1Chars = Math.floor(userMsg1Progress * USER_MSG_1.length);
  const user2Chars = Math.floor(userMsg2Progress * USER_MSG_2.length);

  return (
    <div id="quickchat-track" className="relative h-[220vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-r from-primary/5 to-transparent opacity-30" aria-hidden="true"></div>
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
                <span className="text-primary">✓</span> <strong>{t('chip1Label')}</strong> — {t('chip1Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip2Label')}</strong> — {t('chip2Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip3Label')}</strong> — {t('chip3Desc')}
              </li>
            </ul>
          </div>

          {/* Chat Window (Right) */}
          <div>
            <div className="card border-primary/20 bg-black/50 shadow-2xl relative overflow-hidden rounded-xl flex flex-col" style={{ maxHeight: '480px' }}>
              {/* Title Bar */}
              <div className="flex items-center justify-between px-3 py-2 bg-gray-900/80 border-b border-white/10">
                <div className="flex items-center gap-2">
                  <div className="w-4 h-4 rounded-full bg-primary flex items-center justify-center text-[8px] font-bold text-white">K</div>
                  <span className="text-xs text-white font-medium">{t('chatTitle')}</span>
                </div>
                <div className="flex items-center gap-2 text-white/40 text-xs">
                  <span>—</span>
                  <span>□</span>
                  <span>✕</span>
                </div>
              </div>

              {/* Toolbar */}
              <div className="flex items-center justify-between px-3 py-1.5 bg-gray-900/60 border-b border-white/10">
                <div className="flex items-center gap-1 text-[10px] text-white/70 bg-white/5 px-2 py-1 rounded">
                  <span>{t('chatModel')}</span>
                  <span className="text-white/40">▾</span>
                </div>
                <div className="flex items-center gap-2 text-white/40 text-[10px]">
                  <span>◷</span>
                  <span>⚙</span>
                  <span className="text-sky-400">⊕</span>
                  <span>⊡</span>
                  <span>+</span>
                </div>
              </div>

              {/* Chat Area */}
              <div className="flex-1 px-3 py-3 space-y-3 overflow-hidden text-xs min-h-[320px]">
                {/* User Message 1 */}
                {userMsg1Progress > 0 && (
                  <div className="flex justify-end transition-all duration-300" style={{
                    opacity: Math.min(userMsg1Progress * 5, 1),
                    transform: `translateY(${(1 - Math.min(userMsg1Progress * 5, 1)) * 8}px)`,
                  }}>
                    <div className="bg-sky-500 text-white rounded-2xl rounded-br-sm px-3 py-2 max-w-[85%]">
                      {USER_MSG_1.slice(0, user1Chars)}
                      {userMsg1Progress < 1 && userMsg1Progress > 0 && (
                        <span className="inline-block w-[2px] h-3 bg-white/80 ml-px animate-pulse align-middle"></span>
                      )}
                    </div>
                  </div>
                )}

                {/* AI Response 1 */}
                {aiMsg1Progress > 0 && (
                  <div className="transition-all duration-500" style={{
                    opacity: aiMsg1Progress,
                    transform: `translateY(${(1 - aiMsg1Progress) * 12}px)`,
                  }}>
                    <div className="bg-white/5 border border-white/10 rounded-lg p-3 text-white/80 space-y-1.5">
                      <p className="text-white/60 text-[10px] leading-relaxed">
                        {t('aiIntro')}
                      </p>
                      <ol className="list-decimal list-inside space-y-1 text-[10px] leading-relaxed">
                        <li><strong className="text-white">{t('aiItem1')}</strong> <span className="text-white/60">{t('aiItem1Desc')}</span></li>
                        <li><strong className="text-white">{t('aiItem2')}</strong> <span className="text-white/60">{t('aiItem2Desc')}</span></li>
                        <li><strong className="text-white">{t('aiItem3')}</strong> <span className="text-white/60">{t('aiItem3Desc')}</span></li>
                        <li><strong className="text-white">{t('aiItem4')}</strong> <span className="text-white/60">{t('aiItem4Desc')}</span></li>
                        <li><strong className="text-white">{t('aiItem5')}</strong> <span className="text-white/60">{t('aiItem5Desc')}</span></li>
                      </ol>
                      <p className="text-white/40 text-[10px]">
                        {t('aiConclusion')}
                      </p>
                    </div>
                  </div>
                )}

                {/* User Message 2 */}
                {userMsg2Progress > 0 && (
                  <div className="flex justify-end transition-all duration-300" style={{
                    opacity: Math.min(userMsg2Progress * 5, 1),
                    transform: `translateY(${(1 - Math.min(userMsg2Progress * 5, 1)) * 8}px)`,
                  }}>
                    <div className="bg-sky-500 text-white rounded-2xl rounded-br-sm px-3 py-2 max-w-[85%]">
                      {USER_MSG_2.slice(0, user2Chars)}
                      {userMsg2Progress < 1 && userMsg2Progress > 0 && (
                        <span className="inline-block w-[2px] h-3 bg-white/80 ml-px animate-pulse align-middle"></span>
                      )}
                    </div>
                  </div>
                )}

                {/* Thinking Indicator */}
                {thinkingVisible && (
                  <div className="flex items-center gap-2 text-white/50 text-[10px] py-1 transition-opacity duration-300">
                    <span className="inline-block w-3 h-3 border-2 border-primary border-t-transparent rounded-full animate-spin"></span>
                    {t('thinking')}
                  </div>
                )}

                {/* AI Response 2 */}
                {aiMsg2Progress > 0 && (
                  <div className="relative transition-all duration-500" style={{
                    opacity: aiMsg2Progress,
                    transform: `translateY(${(1 - aiMsg2Progress) * 12}px)`,
                  }}>
                    <div className="bg-white/5 border border-white/10 rounded-lg p-3 text-white/80 space-y-1.5">
                      <p className="text-[10px] leading-relaxed text-white/60">
                        {t('aiRlIntro')}
                      </p>
                      <p className="text-[10px] font-bold text-white">{t('aiRlSection1')}</p>
                      <p className="text-[10px] leading-relaxed text-white/60">
                        {t('aiRlDesc1')}
                      </p>
                      <p className="text-[10px] font-bold text-white">{t('aiRlSection2')}</p>
                      <ol className="list-decimal list-inside space-y-0.5 text-[10px] leading-relaxed text-white/60">
                        <li><strong className="text-white/80">{t('aiRlItem1')}</strong> {t('aiRlItem1Desc')}</li>
                        <li><strong className="text-white/80">{t('aiRlItem2')}</strong> {t('aiRlItem2Desc')}</li>
                      </ol>
                    </div>
                    {/* Bottom fade — implies more content below */}
                    <div className="absolute bottom-0 left-0 right-0 h-8 bg-gradient-to-t from-black/80 to-transparent rounded-b-lg"></div>
                  </div>
                )}
              </div>

              {/* Input Bar */}
              <div className="flex items-center gap-2 px-3 py-2 border-t border-white/10 bg-gray-900/40">
                <span className="text-white/30 text-xs">📎</span>
                <div className="flex-1 bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-[10px] text-white/30">
                  {t('inputPlaceholder')}
                </div>
                <div className="w-6 h-6 bg-sky-500 rounded flex items-center justify-center text-white text-[10px]">
                  ▶
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
