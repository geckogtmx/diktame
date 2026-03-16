'use client';

import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { WaitingListForm } from '../components/WaitingListForm';
import { Container } from '../components/Container';

export default function WaitlistPage() {
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20 overflow-hidden">
        {/* Ambient background glow */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[400px] bg-primary/10 rounded-full blur-[120px] pointer-events-none"></div>

        <Container>
          <div className="max-w-3xl mx-auto text-center relative z-10">
            {/* Tag/Badge */}
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/5 border border-white/10 mb-8 animate-fade-in-up">
              <span className="w-1.5 h-1.5 rounded-full bg-cta"></span>
              <span className="text-[10px] font-bold text-white/60 uppercase tracking-[0.2em]">
                V2 Early Access
              </span>
            </div>

            <h1 className="text-4xl md:text-6xl font-bold mb-6 tracking-tight animate-fade-in-up animation-delay-100">
              The wait is <span className="text-primary">almost</span> over.
            </h1>

            <div className="space-y-6 text-lg md:text-xl text-muted mb-12 animate-fade-in-up animation-delay-200 leading-relaxed italic">
              <p>
                &quot;We&apos;re putting the finishing touches on the most powerful, 
                privacy-first voice engine for Windows.&quot;
              </p>
              <p className="not-italic text-base md:text-lg text-white/80">
                Join our exclusive waiting list to be the first to know when we ship. 
                Get early access to the Power Version and contribute to the future of 
                human-computer interaction.
              </p>
            </div>

            {/* The Form */}
            <div className="animate-fade-in-up animation-delay-300">
                <WaitingListForm />
            </div>

            {/* Why join? */}
            <div className="mt-20 grid grid-cols-1 md:grid-cols-3 gap-8 text-left animate-fade-in-up animation-delay-400">
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">🚀</div>
                    <h3 className="text-white font-bold mb-2">First in Line</h3>
                    <p className="text-sm text-muted">Be notified the second v0.1 drops for public download.</p>
                </div>
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">💎</div>
                    <h3 className="text-white font-bold mb-2">Founder Perks</h3>
                    <p className="text-sm text-muted">Special pricing considerations for our very first batch of users.</p>
                </div>
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">🛡️</div>
                    <h3 className="text-white font-bold mb-2">Private Updates</h3>
                    <p className="text-sm text-muted">Get behind-the-scenes progress on local LLM and STT integration.</p>
                </div>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
