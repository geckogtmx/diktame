'use client';

import { useState } from 'react';
import Link from 'next/link';
import { FeaturesModal } from './FeaturesModal';

export function PricingSection() {
  const [isModalOpen, setIsModalOpen] = useState(false);

  return (
    <>
      <section id="pricing" className="py-32 px-4 relative bg-background">
        <div className="section-container text-center">
          <h2 className="text-4xl md:text-5xl font-bold mb-6 text-white">Stop Renting Software.</h2>
          <p className="text-xl text-muted mb-20  delay-100">
            No monthly fees. No word limits. Just great tools you own.
          </p>

          <div className="grid md:grid-cols-2 gap-8 max-w-4xl mx-auto w-full mb-12">
            {/* Free Trial Version ($0) */}
            <div className="card !overflow-visible p-8 border-2 border-white/10 relative text-left flex flex-col  delay-200">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-background border border-white/20 px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest text-white">
                Free Trial
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">Full App Trial</h3>
              <div className="text-5xl font-bold mb-6 text-white">$0.00</div>
              <p className="text-sm text-muted mb-8">Test the cloud engine with complimentary credits.</p>
              <ul className="space-y-3 text-sm text-muted mb-8">
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> Minimum friction, high control
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Deepgram</strong> STT & <strong>Gemini Flash</strong> LLM
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Instant Access</strong> to Core Modes
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Unlimited</strong> Custom Presets
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Quick Chat</strong> Access included
                </li>
                <li className="flex gap-2 opacity-60">
                  <span className="text-red-500">✕</span> No Offline AI (Cloud Only)
                </li>
              </ul>
              <Link 
                href="/waitlist"
                className="w-full py-3 rounded-lg bg-white/10 text-white font-bold hover:scale-105 transition-transform mt-auto hover:bg-white/20 border border-white/20 text-center"
              >
                Start Free Trial
              </Link>
              <p className="text-xs text-muted mt-4 text-center">Complimentary credits included.</p>
            </div>
            {/* Power Version ($25) */}
            <div className="card !overflow-visible p-8 border-2 border-primary relative text-left flex flex-col  delay-300 shadow-glow">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-cta text-white border border-cta px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest animate-pulse">
                Early Bird Sale
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">Power Version</h3>
              <div className="flex items-baseline gap-3 mb-6">
                <span className="text-3xl font-bold text-white/40 line-through">$25</span>
                <span className="text-5xl font-bold text-white">$20</span>
              </div>
              <p className="text-sm text-muted mb-8">The complete arsenal.</p>
              <ul className="space-y-3 text-sm text-muted mb-8">
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Unrestricted</strong> Full App access
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Ask Mode</strong> UI
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Bilingual</strong> Bridge (ES ↔ EN)
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>Context Modes</strong> & Macros
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> <strong>+Key</strong> (Auto-Enter/Tab)
                </li>
                <li className="flex gap-2">
                  <span className="text-primary">✓</span> Source-Available License
                </li>
              </ul>
              <button
                onClick={() => setIsModalOpen(true)}
                className="text-xs text-primary/60 hover:text-primary transition-colors mb-6 text-center block w-full cursor-pointer"
              >
                + See all 45+ features
              </button>
              <Link 
                href="/waitlist"
                className="w-full py-3 rounded-lg bg-orange-400 text-black font-bold hover:scale-105 transition-transform mt-auto hover:bg-orange-300 text-center"
              >
                Get Power
              </Link>
              <p className="text-xs text-muted mt-4 text-center">One-time payment. Forever.</p>
            </div>
          </div>

          {/* Bottom Banner: Build It Yourself ($0) */}
          <div className="w-full max-w-4xl mx-auto p-8 md:p-12 rounded-2xl border border-white/10 bg-white/5 hover:bg-white/10 transition-colors flex flex-col md:flex-row items-center gap-8 text-left  delay-300">
            <div className="flex-1">
              <h3 className="text-2xl font-bold mb-2 text-white">Build It Yourself</h3>
              <div className="text-muted mb-4 text-sm">
                Don&apos;t want to pay? Great. You shouldn&apos;t have to if you have the skills.
              </div>
              <ul className="flex flex-wrap gap-4 text-sm text-gray-300">
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📚</span> <strong>Full Source Code</strong>
                </li>
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📖</span> In-Depth Build Guide
                </li>
              </ul>
            </div>

            <div className="text-center md:text-right flex flex-col items-center md:items-end min-w-[200px]">

              <Link 
                href="/waitlist"
                className="w-full md:w-auto px-8 py-3 rounded-xl border border-white/20 hover:bg-white hover:text-black transition-all whitespace-nowrap text-white text-center"
              >
                Start Learning
              </Link>
            </div>
          </div>

          {/* Support the Dev */}
          <div className="text-center mt-12">
            <a
              href="https://ko-fi.com/geckogtmx"
              target="_blank"
              rel="noopener noreferrer"
              className="inline-block bg-white/5 border border-white/20 text-white px-6 py-3 rounded-full font-bold text-sm hover:scale-105 hover:bg-white hover:text-black transition-all"
            >
              Support the Developer
              <span className="block text-[10px] font-normal opacity-60 mt-0.5 font-mono">
                Keep dIKta.me updated &amp; independent via Ko-Fi
              </span>
            </a>
          </div>
        </div>

        <FeaturesModal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
      </section>
    </>
  );
}
