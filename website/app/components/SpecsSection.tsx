'use client';

import { useState } from 'react';
import { useSpecsScroll } from '@/lib/animations/useSpecsScroll';
import { useTranslations } from 'next-intl';
import { FeaturesModal } from './FeaturesModal';

export function SpecsSection() {
  const { activeGroup } = useSpecsScroll();
  const t = useTranslations('SpecsSection');
  const [isModalOpen, setIsModalOpen] = useState(false);

  return (
    <div id="specs-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute top-0 right-0 w-1/3 h-full bg-primary/5 blur-[120px]" aria-hidden="true"></div>

        <div className="section-container relative z-10 w-full">
          <div className="text-center mb-8 md:mb-16">
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white">{t('title')}</h2>
            <p className="text-muted max-w-2xl mx-auto">
              {t('subtitle')}
            </p>
          </div>

          <div className="relative h-[520px] md:h-[550px]">
            {/* Group 1: Features 1-8 */}
            <div
              id="specs-group-1"
              className={`absolute inset-0 grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-6 transition-all duration-700 ease-out will-change-transform ${
                activeGroup === 1 ? 'opacity-100 translate-y-0 scale-100' : 'opacity-0 translate-y-10 scale-95'
              }`}
            >
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f1Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f1Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f1Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f2Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f2Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f2Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f3Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f3Title')}</h3>
                <div className="text-xs text-muted leading-relaxed space-y-1 hidden md:block">
                  <p>{t('g1f3Key1')}</p>
                  <p>{t('g1f3Key2')}</p>
                  <p>{t('g1f3Key3')}</p>
                  <p>{t('g1f3Key4')}</p>
                  <p className="text-white/40">{t('g1f3Key5')}</p>
                </div>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f4Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f4Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f4Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f5Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f5Title')}</h3>
                <div className="text-xs text-muted leading-relaxed space-y-1 hidden md:block">
                  <p>{t('g1f5Mode1')}</p>
                  <p>{t('g1f5Mode2')}</p>
                  <p>{t('g1f5Mode3')}</p>
                  <p>{t('g1f5Mode4')}</p>
                </div>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f6Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f6Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f6Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f7Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f7Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f7Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g1f8Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g1f8Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g1f8Desc')}
                </p>
              </div>
            </div>

            {/* Group 2: Features 9-16 */}
            <div
              id="specs-group-2"
              className={`absolute inset-0 grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-6 transition-all duration-700 ease-out will-change-transform ${
                activeGroup === 2 ? 'opacity-100 translate-y-0 scale-100' : 'opacity-0 translate-y-10 scale-95 pointer-events-none'
              }`}
            >
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f1Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f1Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f1Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f2Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f2Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f2Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f3Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f3Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f3Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f4Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f4Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f4Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f5Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f5Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f5Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f6Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f6Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f6Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f7Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f7Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f7Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g2f8Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g2f8Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g2f8Desc')}
                </p>
              </div>
            </div>

            {/* Group 3: Features 17-24 */}
            <div
              id="specs-group-3"
              className={`absolute inset-0 grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-6 transition-all duration-700 ease-out will-change-transform ${
                activeGroup === 3 ? 'opacity-100 translate-y-0 scale-100' : 'opacity-0 translate-y-10 scale-95 pointer-events-none'
              }`}
            >
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f1Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f1Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f1Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f2Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f2Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f2Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f3Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f3Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f3Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f4Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f4Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f4Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f5Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f5Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f5Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f6Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f6Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f6Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f7Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f7Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f7Desc')}
                </p>
              </div>
              <div className="card p-3 md:p-6">
                <div className="text-primary mb-2 md:mb-4 font-mono text-[10px] md:text-xs uppercase tracking-widest">{t('g3f8Label')}</div>
                <h3 className="text-sm md:text-lg font-bold text-text mb-1 md:mb-2 text-white">{t('g3f8Title')}</h3>
                <p className="text-xs md:text-sm text-muted hidden md:block">
                  {t('g3f8Desc')}
                </p>
              </div>
            </div>
          </div>

          <div className="mt-12 flex justify-center gap-2">
            <div
              id="specs-dot-1"
              className={`h-1 rounded-full transition-all duration-300 ${
                activeGroup === 1 ? 'w-16 bg-primary' : 'w-12 bg-white/20'
              }`}
            ></div>
            <div
              id="specs-dot-2"
              className={`h-1 rounded-full transition-all duration-300 ${
                activeGroup === 2 ? 'w-16 bg-primary' : 'w-12 bg-white/20'
              }`}
            ></div>
            <div
              id="specs-dot-3"
              className={`h-1 rounded-full transition-all duration-300 ${
                activeGroup === 3 ? 'w-16 bg-primary' : 'w-12 bg-white/20'
              }`}
            ></div>
          </div>

          <div className="mt-6 flex justify-center">
            <button
              onClick={() => setIsModalOpen(true)}
              className="text-sm text-primary/70 hover:text-primary transition-colors cursor-pointer font-mono tracking-wide flex items-center gap-2"
            >
              {t('seeAllFeatures')} {t('seeAllFeaturesArrow')}
            </button>
          </div>
        </div>
      </div>

      <FeaturesModal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
    </div>
  );
}
