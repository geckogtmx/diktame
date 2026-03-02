'use client';

export function TokensSection() {
  return (
    <div id="tokens-track" className="relative h-[200vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-r from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Text (Left) */}
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

          {/* Card (Right) */}
          <div className="card border-primary/20 bg-black/50 p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4">
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">Provider Settings</span>
              <span className="w-3 h-3 rounded-full bg-green-500 animate-pulse"></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase">Active Model</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded flex justify-between" id="token-model-display">
                  <span className="text-primary">gemma3:4b (Local)</span>
                  <span className="text-muted">▼</span>
                </div>
              </div>

              <div className="flex flex-col gap-2 opacity-40 transition-opacity" id="token-anthropic">
                <label className="text-[10px] text-muted uppercase">Anthropic Key</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted">sk-ant-••••••••••••••••</div>
              </div>

              <div className="flex flex-col gap-2 opacity-40 transition-opacity" id="token-google">
                <label className="text-[10px] text-muted uppercase">Google AI Key</label>
                <div className="bg-white/5 border border-white/10 p-2 rounded text-muted">AIzaSy••••••••••••••••</div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 text-center text-[10px] text-muted italic">
              &quot;Encryption enabled. Keys never leave this machine.&quot;
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
