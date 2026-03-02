'use client';

import { useAskModeScroll } from '@/lib/animations/useAskModeScroll';

export function AskModeSection() {
  const { inputWidth, outputWidth, showInputCursor, showOutputCursor } = useAskModeScroll();

  return (
    <div id="ask-track" className="relative h-[200vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Card (Left) */}
          <div className="card border-primary/20 bg-black/50 p-8 font-mono text-sm leading-loose shadow-2xl order-last md:order-first relative overflow-hidden">
            <div className="absolute -top-3 -right-3 w-24 h-24 bg-primary/20 blur-2xl rounded-full"></div>
            <div className="text-muted mb-4 border-b border-white/10 pb-2 flex justify-between">
              <span>Input (Microphone)</span>
              <span className="text-xs border border-white/20 px-2 py-0.5 rounded text-muted">Ctrl+Alt+A</span>
            </div>
            <div
              className={`text-secondary mb-8 overflow-hidden whitespace-nowrap ${
                showInputCursor ? 'border-r-2 border-secondary' : ''
              }`}
              id="ask-input"
              style={{ width: `${inputWidth}%` }}
            >
              &quot;What is the capital of madagascar and its population&quot;
            </div>
            <div className="text-muted mb-4 border-b border-white/10 pb-2">Output (Clipboard/Type)</div>
            <div
              className={`text-primary overflow-hidden whitespace-nowrap ${
                showOutputCursor ? 'border-r-2 border-primary' : ''
              }`}
              id="ask-output"
              style={{ width: `${outputWidth}%` }}
            >
              &quot;Antananarivo, 4,413,000 as of early 2026&quot;
            </div>
          </div>

          {/* Text (Right) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-primary/10 rounded-full text-primary text-xs font-mono">
              JUST ADDED
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              Don&apos;t Search.
              <br />
              Just Ask.
            </h2>
            <p className="text-xl text-muted mb-8">
              Skip the browser tab. Query your local LLM instantly for definitions, math, or quick facts without ever
              leaving your flow.
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>Instant Q&A</strong> - Math, quotes, refs
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>No Tracking</strong> - Local execution
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>Zero Context Switch</strong> - Stay in flow
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
