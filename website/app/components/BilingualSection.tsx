'use client';

import { useBilingualScroll } from '@/lib/animations/useBilingualScroll';

export function BilingualSection() {
  const { inputWidth, outputWidth, showInputCursor, showOutputCursor } = useBilingualScroll();

  return (
    <div id="biling-track" className="relative h-[200vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-r from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-xs font-mono">
              NEW FEATURE
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              We Speak Spanish.
              <br />
              Hablamos Inglés.
            </h2>
            <p className="text-xl text-muted mb-8">
              Real-time translation at the edge. Dictate freely in your native tongue and let the engine bridge the
              gap instantly.
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-green-400">✓</span> No API keys required
              </li>
              <li className="flex items-center gap-3">
                <span className="text-green-400">✓</span> English → Spanish supported
              </li>
              <li className="flex items-center gap-3">
                <span className="text-green-400">✓</span> Context-aware translation
              </li>
            </ul>
          </div>
          <div className="card border-primary/20 bg-black/50 p-8 font-mono text-sm leading-loose shadow-2xl">
            <div className="text-muted mb-4 border-b border-white/10 pb-2">Input (Microphone)</div>
            <div
              className={`text-secondary mb-8 overflow-hidden whitespace-nowrap ${
                showInputCursor ? 'border-r-2 border-secondary' : ''
              }`}
              id="biling-input"
              style={{ width: `${inputWidth}%` }}
            >
              &quot;Hola, necesito enviar el reporte financiero antes del mediodía, ¿puedes revisarlo?&quot;
            </div>
            <div className="text-muted mb-4 border-b border-white/10 pb-2">Output (Application)</div>
            <div
              className={`text-primary overflow-hidden whitespace-nowrap ${
                showOutputCursor ? 'border-r-2 border-primary' : ''
              }`}
              id="biling-output"
              style={{ width: `${outputWidth}%` }}
            >
              &quot;Hi, I need to send the financial report before noon, can you review it?&quot;
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
