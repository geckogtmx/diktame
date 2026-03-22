'use client';

import { useHeroScroll } from '@/lib/animations/useHeroScroll';
import Link from 'next/link';
import { useTranslations } from 'next-intl';

export function HeroSection() {
  const t = useTranslations('HeroSection');
  const { translateY } = useHeroScroll();

  return (
    <div id="hero-track" className="relative h-[200vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden pt-16">
        {/* Background */}
        <div className="absolute inset-0 bg-background z-0" aria-hidden="true"></div>
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-primary/20 rounded-full blur-[120px] pointer-events-none" aria-hidden="true"></div>

        <div className="section-container relative z-10 text-center">
          {/* Badge */}
          <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-white/5 border border-white/10 mb-8 animate-fade-in-up">
            <span className="w-2 h-2 rounded-full bg-cta animate-pulse"></span>
            <span className="text-sm font-medium text-white font-mono tracking-widest">
              {t('badge')}
            </span>
          </div>

          {/* Title with Word Carousel */}
          <div className="w-full mb-6">
            <h1 className="flex flex-col items-center text-5xl md:text-8xl font-bold tracking-tight leading-[0.9] text-white text-center">
              {/* Row 1 */}
              <div>{t('titleLine1')}</div>

              {/* Row 2 */}
              <div className="flex justify-center gap-[0.2em] relative left-[16px] md:left-[29px]">
                {/* START text */}
                <span>{t('titleLine2Start')}</span>

                {/* Word Carousel */}
                <div className="h-[0.9em] overflow-hidden text-primary">
                  <div
                    id="hero-words"
                    className="will-change-transform flex flex-col"
                    style={{ transform: `translateY(${translateY}em)` }}
                  >
                    <div className="h-[0.9em] mb-[1em] flex items-center whitespace-nowrap">{t('carouselWord1')}</div>
                    <div className="h-[0.9em] mb-[1em] flex items-center whitespace-nowrap">{t('carouselWord2')}</div>
                    <div className="h-[0.9em] mb-[1em] flex items-center whitespace-nowrap">{t('carouselWord3')}</div>
                    <div className="h-[0.9em] mb-[1em] flex items-center whitespace-nowrap">{t('carouselWord4')}</div>
                  </div>
                </div>
              </div>
            </h1>
          </div>

          {/* Subtitle */}
          <p className="text-xl text-muted max-w-2xl mx-auto mb-10 leading-relaxed italic">
            {t('subtitle1')} <br />
            {t('subtitle2')} <br />
            <span className="text-primary not-italic font-mono text-sm tracking-tight mt-4 block">
              {t('subtitle3')}
            </span>
          </p>

          {/* CTAs */}
          <div className="flex flex-col items-center gap-8">
            {/* Primary Action: Windows */}
            <Link 
              href="/waitlist"
              className="btn-primary justify-center w-full sm:w-auto text-lg px-12 py-5 shadow-glow hover:scale-105 transition-all"
            >
              {t('ctaDownload')}
              <span className="text-xs font-normal opacity-80 block ml-2 border-l border-white/20 pl-2">
                {t('ctaVersion')}
              </span>
            </Link>

            {/* Platform Badges */}
            <div className="flex flex-wrap items-center justify-center gap-8 text-xs font-medium text-muted/60 uppercase tracking-widest">
              <div className="flex items-center gap-2 group hover:text-white transition-colors cursor-default">
                <span className="opacity-50">{t('platformMacos')}</span>
                <span className="px-1.5 py-0.5 rounded border border-white/10 text-[8px] group-hover:border-white/20">
                  {t('platformSoon')}
                </span>
              </div>
              <div className="flex items-center gap-2 group hover:text-white transition-colors cursor-default">
                <span className="opacity-50">{t('platformIos')}</span>
                <span className="px-1.5 py-0.5 rounded border border-white/10 text-[8px] group-hover:border-white/20">
                  {t('platformSoon')}
                </span>
              </div>
              <div className="flex items-center gap-2 group hover:text-white transition-colors cursor-default">
                <span className="opacity-50">{t('platformAndroid')}</span>
                <span className="px-1.5 py-0.5 rounded border border-white/10 text-[8px] group-hover:border-white/20">
                  {t('platformSoon')}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
