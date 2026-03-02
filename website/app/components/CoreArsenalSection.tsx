'use client';

import { useCoreArsenalScroll } from '@/lib/animations/useCoreArsenalScroll';

export function CoreArsenalSection() {
  const { activePair } = useCoreArsenalScroll();

  return (
    <div id="core-track" className="relative h-[400vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        {/* Background Effects */}
        <div className="absolute inset-0 bg-background z-0"></div>
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-primary/5 rounded-full blur-[160px] pointer-events-none"></div>

        <div className="section-container relative z-10 w-full">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-5xl font-bold mb-4 text-white">The Core Arsenal.</h2>
            <p className="text-muted">Four modes. Infinite productivity. Zero latency.</p>
          </div>

          <div className="flex flex-col gap-8 max-w-5xl mx-auto px-4">
            {/* Pair 1: Dictate & Ask */}
            <div
              id="core-pair-1"
              className={`flex flex-col md:flex-row gap-6 transition-all duration-1000 ease-out will-change-transform ${
                activePair >= 1 ? 'opacity-100 scale-100' : 'opacity-0 scale-[0.98]'
              }`}
            >
              {/* Card 1: Dictate */}
              <div id="core-card-1" className="card flex-1 p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>Mode 01</span>
                  <span>Ctrl+Alt+D</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">Dictate</h3>
                <p className="text-sm text-muted leading-relaxed">
                  The Input. 1,000 WPM local transcription with RAW Mode for instant, unfiltered injection into any
                  application.
                </p>
              </div>

              {/* Card 2: Ask */}
              <div id="core-card-2" className="card flex-1 p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0012 18.75c-1.03 0-1.9.4-2.593 1.02l-.547.547z"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>Mode 02</span>
                  <span>Ctrl+Alt+A</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">Ask</h3>
                <p className="text-sm text-muted leading-relaxed">
                  The Brain. Query your local LLM instantly for definitions, math, or quick facts without ever leaving
                  your active window.
                </p>
              </div>
            </div>

            {/* Pair 2: Refine & Notes */}
            <div
              id="core-pair-2"
              className={`flex flex-col md:flex-row gap-6 transition-all duration-1000 ease-out will-change-transform ${
                activePair >= 2 ? 'opacity-100 scale-100' : 'opacity-0 scale-[0.98]'
              }`}
            >
              {/* Card 3: Refine */}
              <div id="core-card-3" className="card flex-1 p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>Mode 03</span>
                  <span>Ctrl+Alt+R</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">Refine</h3>
                <p className="text-sm text-muted leading-relaxed">
                  The Finish. Professional-grade AI editing. Transform rough transcripts into polished emails or
                  documentation instantly.
                </p>
              </div>

              {/* Card 4: Notes */}
              <div id="core-card-4" className="card flex-1 p-8 border-primary/20 backdrop-blur-2xl">
                <div className="w-12 h-12 bg-primary/10 rounded-xl flex items-center justify-center mb-6 text-primary">
                  <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"
                    />
                  </svg>
                </div>
                <div className="text-primary mb-2 font-mono text-[10px] uppercase tracking-widest flex justify-between">
                  <span>Mode 04</span>
                  <span>Ctrl+Alt+N</span>
                </div>
                <h3 className="text-xl font-bold text-white mb-3">Structured Notes</h3>
                <p className="text-sm text-muted leading-relaxed">
                  The Memory. Capture thoughts with automatic timestamps and metatags. Sequential formatting ready for
                  batch processing.
                </p>
              </div>
            </div>
          </div>

          {/* Progress Steps (2 pairs) */}
          <div className="mt-12 flex justify-center gap-4">
            <div
              id="core-dot-1"
              className={`h-1 rounded-full transition-all duration-300 ${
                activePair >= 1 ? 'w-32 bg-primary' : 'w-24 bg-white/10'
              }`}
            ></div>
            <div
              id="core-dot-2"
              className={`h-1 rounded-full transition-all duration-300 ${
                activePair >= 2 ? 'w-32 bg-primary' : 'w-24 bg-white/10'
              }`}
            ></div>
          </div>
        </div>
      </div>
    </div>
  );
}
