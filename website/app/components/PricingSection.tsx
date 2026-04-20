'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import Link from 'next/link';
import { FeaturesModal } from './FeaturesModal';
import { trackCtaClick, trackFeaturesModalOpen, trackExternalLink } from '@/lib/analytics';

export function PricingSection({ asH1 = false }: { asH1?: boolean }) {
  const t = useTranslations('PricingSection');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const Heading = asH1 ? 'h1' : 'h2';

  return (
    <>
      <section id="pricing" className="py-32 px-4 relative bg-background scroll-mt-20">
        <div className="section-container text-center">
          <Heading className="text-4xl md:text-5xl font-bold mb-6 text-white">{t('title')}</Heading>
          <p className="text-xl text-muted mb-20  delay-100">
            {t('subtitle')}
          </p>

          <div className="grid md:grid-cols-2 gap-6 md:gap-8 max-w-4xl mx-auto w-full mb-12">
            {/* Free Trial Version ($0) */}
            <div className="card !overflow-visible p-5 md:p-8 border-2 border-white/10 relative text-left flex flex-col  delay-200">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-background border border-white/20 px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest text-white">
                {t('freeTrialBadge')}
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">{t('freeTrialTitle')}</h3>
              <div className="text-5xl font-bold mb-6 text-white">{t('freeTrialPrice')}</div>
              <p className="text-sm text-muted mb-8">{t('freeTrialDesc')}</p>
              <ul className="space-y-3 text-sm text-muted mb-8">
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> {t('freeTrialF1')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('freeTrialF2Prefix')}</strong>{t('freeTrialF2Mid')}<strong>{t('freeTrialF2Suffix')}</strong>{t('freeTrialF2End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('freeTrialF3Prefix')}</strong>{t('freeTrialF3End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('freeTrialF4Prefix')}</strong>{t('freeTrialF4End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('freeTrialF5Prefix')}</strong>{t('freeTrialF5End')}
                </li>
                <li className="flex gap-2 opacity-60">
                  <span className="text-red-500">✕</span> {t('freeTrialF6')}
                </li>
              </ul>
              <Link
                href="/waitlist"
                onClick={() => trackCtaClick('free_trial', 'pricing')}
                className="w-full py-3 rounded-lg bg-white/10 text-white font-bold hover:scale-105 transition-transform mt-auto hover:bg-white/20 border border-white/20 text-center"
              >
                {t('freeTrialCta')}
              </Link>
              <p className="text-xs text-muted mt-4 text-center">{t('freeTrialNote')}</p>
            </div>
            {/* Full Version ($20) */}
            <div className="card !overflow-visible p-5 md:p-8 border-2 border-primary relative text-left flex flex-col  delay-300 shadow-glow">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-cta text-white border border-cta px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest animate-pulse">
                {t('earlyBirdBadge')}
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">{t('powerTitle')}</h3>
              <div className="flex items-baseline gap-3 mb-6">
                <span className="text-3xl font-bold text-white/40 line-through">{t('powerOldPrice')}</span>
                <span className="text-5xl font-bold text-white">{t('powerPrice')}</span>
              </div>
              <p className="text-sm text-muted mb-8">{t('powerDesc')}</p>
              <ul className="space-y-3 text-sm text-muted mb-8">
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('powerF1Prefix')}</strong>{t('powerF1End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('powerF2Prefix')}</strong>{t('powerF2End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('powerF3Prefix')}</strong>{t('powerF3End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('powerF4Prefix')}</strong>{t('powerF4End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>{t('powerF5Prefix')}</strong>{t('powerF5End')}
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> {t('powerF6')}
                </li>
              </ul>
              <button
                onClick={() => { setIsModalOpen(true); trackFeaturesModalOpen(); }}
                className="text-xs text-primary/60 hover:text-primary transition-colors mb-6 text-center block w-full cursor-pointer"
              >
                {t('powerFeaturesLink')}
              </button>
              <Link
                href="/waitlist"
                onClick={() => trackCtaClick('power_license', 'pricing')}
                className="w-full py-3 rounded-lg bg-orange-400 text-black font-bold hover:scale-105 transition-transform mt-auto hover:bg-orange-300 text-center"
              >
                {t('powerCta')}
              </Link>
              <p className="text-xs text-muted mt-4 text-center">{t('powerNote')}</p>
            </div>
          </div>

          {/* Bottom Banner: Build It Yourself ($0) */}
          <div className="w-full max-w-4xl mx-auto p-5 md:p-12 rounded-2xl border border-white/10 bg-white/5 hover:bg-white/10 transition-colors flex flex-col md:flex-row items-center gap-6 md:gap-8 text-left  delay-300">
            <div className="flex-1">
              <h3 className="text-2xl font-bold mb-2 text-white">{t('buildTitle')}</h3>
              <div className="text-muted mb-4 text-sm">
                {t('buildDesc')}
              </div>
              <ul className="flex flex-wrap gap-4 text-sm text-gray-300">
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📚</span> <strong>{t('buildF1')}</strong>
                </li>
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📖</span> {t('buildF2')}
                </li>
              </ul>
            </div>

            <div className="text-center md:text-right flex flex-col items-center md:items-end md:min-w-[200px]">

              <Link
                href="/waitlist"
                onClick={() => trackCtaClick('build_it_yourself', 'pricing')}
                className="w-full md:w-auto px-8 py-3 rounded-xl border border-white/20 hover:bg-white hover:text-black transition-all whitespace-nowrap text-white text-center"
              >
                {t('buildCta')}
              </Link>
            </div>
          </div>

          {/* Support the Dev */}
          <div className="text-center mt-12">
            <a
              href="https://ko-fi.com/geckogtmx"
              target="_blank"
              rel="noopener noreferrer"
              onClick={() => trackExternalLink('https://ko-fi.com/geckogtmx', 'kofi')}
              className="inline-block bg-white/5 border border-white/20 text-white px-6 py-3 rounded-full font-bold text-sm hover:scale-105 hover:bg-white hover:text-black transition-all"
            >
              {t('supportCta')}
              <span className="block text-[10px] font-normal opacity-60 mt-0.5 font-mono">
                {t('supportNote')}
              </span>
            </a>
          </div>
        </div>

        <FeaturesModal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
      </section>
    </>
  );
}
