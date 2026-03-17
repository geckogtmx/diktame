'use client';

import { Navbar } from './components/Navbar';
import { Footer } from './components/Footer';
import { HeroSection } from './components/HeroSection';
import { CoreArsenalSection } from './components/CoreArsenalSection';
import { VersusSection } from './components/VersusSection';
import { SpecsSection } from './components/SpecsSection';
import { BilingualSection } from './components/BilingualSection';
import { AskModeSection } from './components/AskModeSection';
import { QuickChatSection } from './components/QuickChatSection';
import { TokensSection } from './components/TokensSection';
import { VoiceMacrosSection } from './components/VoiceMacrosSection';
import { TTSSection } from './components/TTSSection';
import { LogoScroll } from './components/LogoScroll';
import { PricingSection } from './components/PricingSection';

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
