'use client';

import { useState } from 'react';
import { FeaturesModal } from './FeaturesModal';

export function PricingSection() {
  const [isModalOpen, setIsModalOpen] = useState(false);

  return (
    <>
      <section id="pricing" className="py-32 px-4 relative bg-[#020617]">
        <div className="section-container text-center">
          <h2 className="text-4xl md:text-5xl font-bold mb-6 text-white">Stop Renting Software.</h2>
          <p className="text-xl text-[#94a3b8] mb-20  delay-100">
            No monthly fees. No word limits. Just great tools you own.
          </p>

          <div className="grid md:grid-cols-2 gap-8 max-w-4xl mx-auto w-full mb-12">
            {/* Barebones Local ($10) */}
            <div className="card !overflow-visible p-8 border-2 border-white/10 relative text-left flex flex-col  delay-200">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-[#020617] border border-white/20 px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest text-white">
                Free Trial
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">Barebones Cloud</h3>
              <div className="text-5xl font-bold mb-6 text-white">$0.00</div>
              <p className="text-sm text-[#94a3b8] mb-8">Test the engine.</p>
              <ul className="space-y-3 text-sm text-[#94a3b8] mb-8">
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>No CC</strong> Required
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> 15,000 words or 15 days
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">⚡</span> Instant Access
                </li>
              </ul>
              <button className="w-full py-3 rounded-lg bg-white/10 text-white font-bold hover:scale-105 transition-transform mt-auto hover:bg-white/20 border border-white/20">
                Start Free Trial
              </button>
              <p className="text-xs text-[#94a3b8] mt-4 text-center">No commitment.</p>
            </div>

            {/* Power Version ($25) */}
            <div className="card !overflow-visible p-8 border-2 border-[#2563eb] relative text-left flex flex-col  delay-300 shadow-glow">
              <div className="absolute -top-4 left-1/2 -translate-x-1/2 bg-[#2563eb] text-white border border-[#2563eb] px-4 py-1 rounded-full text-xs font-medium uppercase tracking-widest">
                Best Value
              </div>
              <h3 className="text-xl font-bold mb-2 mt-2 text-white">Power Version</h3>
              <div className="text-5xl font-bold mb-6 text-white">$25</div>
              <p className="text-sm text-[#94a3b8] mb-8">The complete arsenal.</p>
              <ul className="space-y-3 text-sm text-[#94a3b8] mb-8">
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>Everything</strong> in Barebones
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>Ask Mode</strong> UI
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>Bilingual</strong> Bridge (ES ↔ EN)
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>Context Modes</strong> & Macros
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> <strong>+Key</strong> (Auto-Enter/Tab)
                </li>
                <li className="flex gap-2">
                  <span className="text-[#2563eb]">✓</span> Source-Available License
                </li>
              </ul>
              <button
                onClick={() => setIsModalOpen(true)}
                className="text-xs text-[#2563eb]/60 hover:text-[#2563eb] transition-colors mb-6 text-center block w-full cursor-pointer"
              >
                + See all 45+ features
              </button>
              <button className="w-full py-3 rounded-lg bg-orange-400 text-black font-bold hover:scale-105 transition-transform mt-auto hover:bg-orange-300">
                Get Power
              </button>
              <p className="text-xs text-[#94a3b8] mt-4 text-center">One-time payment. Forever.</p>
            </div>
          </div>

          {/* Bottom Banner: Build It Yourself ($0) */}
          <div className="w-full max-w-4xl mx-auto p-8 md:p-12 rounded-2xl border border-white/10 bg-white/5 hover:bg-white/10 transition-colors flex flex-col md:flex-row items-center gap-8 text-left  delay-300">
            <div className="flex-1">
              <h3 className="text-2xl font-bold mb-2 text-white">Build It Yourself</h3>
              <div className="text-[#94a3b8] mb-4 text-sm">
                Don&apos;t want to pay? Great. You shouldn&apos;t have to if you have the skills.
              </div>
              <ul className="flex flex-wrap gap-4 text-sm text-gray-300">
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📚</span> <strong>Full Source Code</strong>
                </li>
                <li className="flex gap-2 items-center">
                  <span className="text-xl">🎓</span> Learn Electron + Python
                </li>
                <li className="flex gap-2 items-center">
                  <span className="text-xl">📖</span> In-Depth Build Guide
                </li>
              </ul>
            </div>

            <div className="text-center md:text-right flex flex-col items-center md:items-end min-w-[200px]">

              <button className="w-full md:w-auto px-8 py-3 rounded-xl border border-white/20 hover:bg-white hover:text-black transition-all whitespace-nowrap text-white">
                Start Learning
              </button>
            </div>
          </div>

          {/* Keep It Updated Button */}
          <div className="text-center mt-12  delay-300">
            <a
              href="#"
              className="inline-block bg-white/5 border border-white/20 text-white px-6 py-3 rounded-full font-bold text-sm hover:scale-105 hover:bg-white hover:text-black transition-all"
            >
              Keep It All Updated
              <span className="block text-[10px] font-normal opacity-60 mt-0.5 font-mono">
                Support the dev & get updates via Ko-Fi
              </span>
            </a>
          </div>
        </div>

        <FeaturesModal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
      </section>
    </>
  );
}
