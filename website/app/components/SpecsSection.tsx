'use client';

import { useSpecsScroll } from '@/lib/animations/useSpecsScroll';

export function SpecsSection() {
  const { activeGroup } = useSpecsScroll();

  return (
    <div id="specs-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute top-0 right-0 w-1/3 h-full bg-primary/5 blur-[120px]"></div>

        <div className="section-container relative z-10 w-full">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white">The Specs.</h2>
            <p className="text-muted max-w-2xl mx-auto">
              Everything you need to make your voice heard. Nothing you don&apos;t.
            </p>
          </div>

          <div className="relative h-[650px] md:h-[550px]">
            {/* Group 1: Features 1-8 */}
            <div
              id="specs-group-1"
              className={`absolute inset-0 grid md:grid-cols-4 gap-6 transition-all duration-700 ease-out will-change-transform ${
                activeGroup === 1 ? 'opacity-100 translate-y-0 scale-100' : 'opacity-0 translate-y-10 scale-95'
              }`}
            >
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Core</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Whisper V3 Turbo</h3>
                <p className="text-sm text-muted">
                  State-of-the-art accuracy running locally. Use it in RAW Mode for instant transcription and injection
                  with good tolerance for filler and punctuation correction.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Intelligence</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Local Small Model Brain</h3>
                <p className="text-sm text-muted">
                  Intelligent formatting, filler removal, excellent math, and general knowledge. Utilize different models
                  for various simple tasks, all local, all private, and all exceptionally fast.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Speed</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Global Configurable Hotkeys</h3>
                <div className="text-xs text-muted leading-relaxed space-y-1">
                  <p>Ctrl+Alt+D (Dictate)</p>
                  <p>Ctrl+Alt+A (Ask)</p>
                  <p>Ctrl+Alt+R (Refine Auto & Freeform)</p>
                  <p>Ctrl+Alt+T (Translate)</p>
                  <p>Ctrl+Alt+V (Re-inject Last)</p>
                </div>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Integration</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Precision Injection</h3>
                <p className="text-sm text-muted">
                  Zero-latency, 1,000 WPM text injection with native window emulation across every application.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Workflow</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Intelligent Modes</h3>
                <div className="text-xs text-muted leading-relaxed space-y-1">
                  <p>Standard - Direct Injection</p>
                  <p>Professional - Polished Output</p>
                  <p>Prompt - AI Instruction mode</p>
                  <p>Raw - Unfiltered Stream</p>
                </div>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Cloud</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Hybrid Cloud Bridge</h3>
                <p className="text-sm text-muted">
                  Seamlessly transition from local execution to Gemini 2.0 via one-click Google OAuth.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Power User</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">API Freedom</h3>
                <p className="text-sm text-muted">
                  No markup, no middleman. Plug in your own OpenAI, Anthropic, or DeepSeek keys directly.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Translation</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Cross-Lingual Engine</h3>
                <p className="text-sm text-muted">
                  Real-time ES ↔ EN translation integrated at the model level for zero-latency bridging.
                </p>
              </div>
            </div>

            {/* Group 2: Features 9-16 */}
            <div
              id="specs-group-2"
              className={`absolute inset-0 grid md:grid-cols-4 gap-6 transition-all duration-700 ease-out will-change-transform ${
                activeGroup === 2 ? 'opacity-100 translate-y-0 scale-100' : 'opacity-0 translate-y-10 scale-95 pointer-events-none'
              }`}
            >
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Privacy</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Local Privacy Guard</h3>
                <p className="text-sm text-muted">
                  100% local processing with automatic PII and sensitive data scrubbing from all logs.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Security</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Encrypted SafeStorage</h3>
                <p className="text-sm text-muted">
                  Secrets and keys are managed via OS-level AES-256 encryption. Never exposed, never raw.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">UI</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Floating Pill</h3>
                <p className="text-sm text-muted">Unobtrusive overlay. Out of your way.</p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Deploy</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Self-Contained Binary</h3>
                <p className="text-sm text-muted">
                  Single 150MB portable .exe includes a full Python runtime and embedded Ollama engine.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Productivity</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Voice Macros (Coming v2.0)</h3>
                <p className="text-sm text-muted">Expand snippets and signatures instantly.</p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Flexibility</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Model Agnostic</h3>
                <p className="text-sm text-muted">Switch between Llama, Mistral, or Gemma.</p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Reliability</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Smart Fallback</h3>
                <p className="text-sm text-muted">
                  Intelligent session recovery—never lose a word of dictation if a model hangs or crashes.
                </p>
              </div>
              <div className="card p-6">
                <div className="text-primary mb-4 font-mono text-xs uppercase tracking-widest">Architecture</div>
                <h3 className="text-lg font-bold text-text mb-2 text-white">Hackable Core</h3>
                <p className="text-sm text-muted">Built on ZeroMQ. Exposes a plugin stream.</p>
              </div>
            </div>
          </div>

          <div className="mt-12 flex justify-center gap-2">
            <div
              id="specs-dot-1"
              className={`h-1 rounded-full transition-all duration-300 ${
                activeGroup === 1 ? 'w-16 bg-primary' : 'w-12 bg-white/20'
              }`}
            ></div>
            <div
              id="specs-dot-2"
              className={`h-1 rounded-full transition-all duration-300 ${
                activeGroup === 2 ? 'w-16 bg-primary' : 'w-12 bg-white/20'
              }`}
            ></div>
          </div>
        </div>
      </div>
    </div>
  );
}
