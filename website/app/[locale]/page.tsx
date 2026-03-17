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

export default function Home() {
  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Hero */}
      <HeroSection />

      {/* Features */}
      <CoreArsenalSection />

      {/* Comparison */}
      <VersusSection />

      {/* Specs */}
      <SpecsSection />

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

      {/* Logo Scroll */}
      <LogoScroll />

      {/* Pricing */}
      <PricingSection />

      {/* Footer */}
      <Footer />
    </main>
  );
}
