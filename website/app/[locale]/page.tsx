'use client';

import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { HeroSection } from '@/app/components/HeroSection';
import { CoreArsenalSection } from '@/app/components/CoreArsenalSection';
import { VersusSection } from '@/app/components/VersusSection';
import { SpecsSection } from '@/app/components/SpecsSection';
import { BilingualSection } from '@/app/components/BilingualSection';
import { AskModeSection } from '@/app/components/AskModeSection';
import { QuickChatSection } from '@/app/components/QuickChatSection';
import { TokensSection } from '@/app/components/TokensSection';
import { VoiceMacrosSection } from '@/app/components/VoiceMacrosSection';
import { TTSSection } from '@/app/components/TTSSection';
import { LogoScroll } from '@/app/components/LogoScroll';
import { PricingSection } from '@/app/components/PricingSection';
import { CtaSeparator } from '@/app/components/CtaSeparator';
import { FaqCard } from '@/app/components/FaqCard';
import SectionTracker from '@/app/components/SectionTracker';
import { useTranslations } from 'next-intl';

export default function Home() {
  const t = useTranslations('CtaSeparators');

  return (
    <main id="main-content" className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Hero */}
      <SectionTracker sectionId="hero">
        <HeroSection />
      </SectionTracker>

      {/* Features */}
      <SectionTracker sectionId="features">
        <CoreArsenalSection />
      </SectionTracker>

      {/* Comparison */}
      <SectionTracker sectionId="versus">
        <VersusSection />
      </SectionTracker>

      {/* CTA: After comparison */}
      <CtaSeparator text={t('afterVersus')} ctaLabel={t('afterVersusBtn')} ctaHref="/waitlist" variant="primary" />

      {/* Specs */}
      <SectionTracker sectionId="specs">
        <SpecsSection />
      </SectionTracker>

      {/* CTA: After specs */}
      <CtaSeparator text={t('afterSpecs')} ctaLabel={t('afterSpecsBtn')} ctaHref="/waitlist" />

      {/* Bilingual Demo */}
      <BilingualSection />

      {/* Ask Mode Demo */}
      <AskModeSection />

      {/* Quick Chat */}
      <QuickChatSection />

      {/* Tokens Demo */}
      <TokensSection />

      {/* Voice Macros */}
      <VoiceMacrosSection />

      {/* TTS Demo */}
      <TTSSection />

      {/* CTA: After demos */}
      <CtaSeparator text={t('afterDemos')} ctaLabel={t('afterDemosBtn')} ctaHref="#pricing" variant="primary" />

      {/* Logo Scroll */}
      <LogoScroll />

      {/* FAQ */}
      <FaqCard />

      {/* Pricing */}
      <SectionTracker sectionId="pricing">
        <PricingSection />
      </SectionTracker>

      {/* Footer */}
      <Footer />
    </main>
  );
}
