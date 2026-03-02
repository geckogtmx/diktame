'use client';

import { useEffect } from 'react';

interface FeaturesModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function FeaturesModal({ isOpen, onClose }: FeaturesModalProps) {
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 md:p-8">
      <div className="absolute inset-0 bg-black/80 backdrop-blur-xl" onClick={onClose}></div>
      <div className="relative w-full max-w-4xl bg-[#0a0a0a] border border-white/10 rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Modal Header */}
        <div className="p-6 border-b border-white/5 flex justify-between items-center bg-black/20">
          <div>
            <h3 className="text-2xl font-bold text-white">The Comprehensive Arsenal</h3>
            <p className="text-sm text-[#94a3b8]">A deep dive into everything dIKta.me can do.</p>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-white/5 rounded-full transition-colors group">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="w-6 h-6 text-[#94a3b8] group-hover:text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 md:p-10 overflow-y-auto">
          <div className="grid md:grid-cols-2 gap-12">
            {/* Column 1 */}
            <div className="space-y-10">
              {/* Core */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Core Functionality</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Push-to-Talk</strong> — Global hotkey (Ctrl+Alt+D)
                  </li>
                  <li>
                    <strong className="text-white">Whisper V3 Turbo</strong> — 100% Local GPU STT
                  </li>
                  <li>
                    <strong className="text-white">Local Intelligence</strong> — Integrated Gemma 3 4B
                  </li>
                  <li>
                    <strong className="text-white">Auto-Injection</strong> — Directly types into any window
                  </li>
                  <li>
                    <strong className="text-white">+Key (Auto-Enter)</strong> — Configurable spacing & keys (Enter/Tab)
                  </li>
                  <li>
                    <strong className="text-white">Offline Operation</strong> — Zero cloud dependencies
                  </li>
                  <li>
                    <strong className="text-white">Smart Fallback</strong> — Never lose a transcription
                  </li>
                  <li>
                    <strong className="text-white">Oops Mode</strong> — Re-inject last dictation (Ctrl+Alt+V)
                  </li>
                </ul>
              </section>

              {/* Modes */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Voice Modes</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Standard Mode</strong> — Direct text injection
                  </li>
                  <li>
                    <strong className="text-white">Professional Mode</strong> — Polished output with formatting
                  </li>
                  <li>
                    <strong className="text-white">Prompt Mode</strong> — AI instruction processing
                  </li>
                  <li>
                    <strong className="text-white">RAW Mode</strong> — Unfiltered stream for maximum speed
                  </li>
                  <li>
                    <strong className="text-white">Ask Mode (Ctrl+Alt+A)</strong> — Query local LLM instantly
                  </li>
                  <li>
                    <strong className="text-white">Refine Mode (Ctrl+Alt+R)</strong> — AI-powered editing
                  </li>
                  <li>
                    <strong className="text-white">Translate Mode (Ctrl+Alt+T)</strong> — Real-time ES ↔ EN
                  </li>
                  <li>
                    <strong className="text-white">Structured Notes (Ctrl+Alt+N)</strong> — Timestamped capture
                  </li>
                </ul>
              </section>

              {/* Intelligence */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">AI Intelligence</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Local Model Brain</strong> — Gemma 3 4B integrated
                  </li>
                  <li>
                    <strong className="text-white">Cloud Bridge</strong> — Seamless Google OAuth for Gemini 2.0
                  </li>
                  <li>
                    <strong className="text-white">API Freedom</strong> — OpenAI, Anthropic, DeepSeek support
                  </li>
                  <li>
                    <strong className="text-white">Model Agnostic</strong> — Switch between Llama, Mistral, Gemma
                  </li>
                  <li>
                    <strong className="text-white">Context-Aware</strong> — Understands previous dictations
                  </li>
                </ul>
              </section>
            </div>

            {/* Column 2 */}
            <div className="space-y-10">
              {/* Privacy & Security */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Privacy & Security</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">100% Air-Gapped</strong> — No telemetry, no tracking
                  </li>
                  <li>
                    <strong className="text-white">PII Scrubbing</strong> — Automatic sensitive data removal from logs
                  </li>
                  <li>
                    <strong className="text-white">Encrypted Storage</strong> — OS-level AES-256 for API keys
                  </li>
                  <li>
                    <strong className="text-white">Zero Cloud Deps</strong> — Works completely offline
                  </li>
                  <li>
                    <strong className="text-white">No Markup</strong> — BYO API keys, no hidden costs
                  </li>
                </ul>
              </section>

              {/* Translation */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Translation</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Bilingual Engine</strong> — English ↔ Spanish real-time
                  </li>
                  <li>
                    <strong className="text-white">No API Keys</strong> — Translation runs locally
                  </li>
                  <li>
                    <strong className="text-white">Context-Aware</strong> — Understands idioms and phrases
                  </li>
                  <li>
                    <strong className="text-white">Zero-Latency</strong> — Model-level integration
                  </li>
                </ul>
              </section>

              {/* Technical */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Technical</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Self-Contained Binary</strong> — 150MB portable .exe
                  </li>
                  <li>
                    <strong className="text-white">Embedded Ollama</strong> — Python runtime included
                  </li>
                  <li>
                    <strong className="text-white">Floating Pill UI</strong> — Minimal, unobtrusive overlay
                  </li>
                  <li>
                    <strong className="text-white">Hackable Core</strong> — Built on ZeroMQ, plugin-ready
                  </li>
                  <li>
                    <strong className="text-white">Source-Available</strong> — Full code access (Power tier)
                  </li>
                </ul>
              </section>

              {/* Future */}
              <section>
                <h4 className="text-[#2563eb] font-mono text-xs uppercase tracking-widest mb-4">Coming Soon</h4>
                <ul className="space-y-2 text-sm text-[#94a3b8]">
                  <li>
                    <strong className="text-white">Voice Macros (v2.0)</strong> — Programmable snippets
                  </li>
                  <li>
                    <strong className="text-white">More Languages</strong> — French, German, etc.
                  </li>
                  <li>
                    <strong className="text-white">macOS & Linux</strong> — Cross-platform support
                  </li>
                </ul>
              </section>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
