'use client';

import { useTokensScroll } from '@/lib/animations/useTokensScroll';

const MODELS = [
  { name: 'gemma3:4b', tag: 'Local', activeKey: null },
  { name: 'claude-sonnet-4.5', tag: 'Anthropic', activeKey: 'anthropic' as const },
  { name: 'gemini-2.0-flash', tag: 'Google', activeKey: 'google' as const },
];

const KEY_TEXT: Record<string, string> = {
  anthropic: 'sk-ant-••••••••••••••••',
  google: 'AIzaSy••••••••••••••••',
};

export function TokensSection() {
  const { modelIdx, modelProgress, keyProgress } = useTokensScroll();

  const model = MODELS[modelIdx];
  const modelText = `${model.name} (${model.tag})`;
  const modelChars = Math.floor(modelProgress * modelText.length);
  const isCloud = model.tag !== 'Local';
  const isTypingModel = modelProgress > 0 && modelProgress < 1;

  const activeKeyTarget = model.activeKey ? KEY_TEXT[model.activeKey] : null;
  const keyChars = activeKeyTarget ? Math.floor(keyProgress * activeKeyTarget.length) : 0;
  const isTypingKey = activeKeyTarget && keyProgress > 0 && keyProgress < 1;

  return (
    <div id="tokens-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Card (Left) */}
          <div className="card border-primary/20 bg-black/50 p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4 order-last md:order-first">
            <div className="absolute -top-3 -right-3 w-24 h-24 bg-primary/20 blur-2xl rounded-full"></div>
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">Provider Settings</span>
              <span className={`w-3 h-3 rounded-full transition-colors duration-500 ${
                modelProgress === 0 ? 'bg-white/20' : isCloud ? 'bg-green-500 animate-pulse' : 'bg-yellow-500 animate-pulse'
              }`}></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              {/* Active Model — scroll-driven typewriter */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase">Active Model</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded flex justify-between items-center min-h-[32px]">
                  <span className="text-primary">
                    {modelText.slice(0, modelChars)}
                    {isTypingModel && (
                      <span className="inline-block w-[2px] h-3 bg-primary ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                  <span className="text-muted">▼</span>
                </div>
              </div>

              {/* Anthropic Key — lights up when claude is active */}
              <div className={`flex flex-col gap-2 transition-opacity duration-500 ${
                model.activeKey === 'anthropic' ? 'opacity-100' : 'opacity-40'
              }`}>
                <label className="text-[10px] text-muted uppercase">Anthropic Key</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted min-h-[32px]">
                  {model.activeKey === 'anthropic' ? (
                    <>
                      {KEY_TEXT.anthropic.slice(0, keyChars)}
                      {isTypingKey && (
                        <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                      )}
                    </>
                  ) : (
                    'sk-ant-••••••••••••••••'
                  )}
                </div>
              </div>

              {/* Google Key — lights up when gemini is active */}
              <div className={`flex flex-col gap-2 transition-opacity duration-500 ${
                model.activeKey === 'google' ? 'opacity-100' : 'opacity-40'
              }`}>
                <label className="text-[10px] text-muted uppercase">Google AI Key</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted min-h-[32px]">
                  {model.activeKey === 'google' ? (
                    <>
                      {KEY_TEXT.google.slice(0, keyChars)}
                      {isTypingKey && (
                        <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                      )}
                    </>
                  ) : (
                    'AIzaSy••••••••••••••••'
                  )}
                </div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 text-center text-[10px] text-muted italic">
              &quot;Encryption enabled. Keys never leave this machine.&quot;
            </div>
          </div>

          {/* Text (Right) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
              SOVEREIGNTY
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              Your Tokens.
              <br />
              Your Choice.
            </h2>
            <p className="text-xl text-muted mb-8">
              Use local, use API Keys, its your choice, not theirs. Anthropic, Google, Deepseek, or local Ollama —
              dIKta.me unifies them all.
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>BYO Keys</strong> - No markup, ever.
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>Local First</strong> - Ollama & Llama.cpp
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>Infinite Flexibility</strong> - Switch in seconds
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  );
}
