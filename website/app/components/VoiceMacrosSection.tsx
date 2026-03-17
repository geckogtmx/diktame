'use client';

import { useVoiceMacrosScroll } from '@/lib/animations/useVoiceMacrosScroll';

const MACROS = [
  {
    trigger: 'Inject sig',
    output: 'Eduardo R.\nProduct Lead · dIKta.me\nhello@dikta.me',
  },
  {
    trigger: 'Inject email',
    output: 'Hi [Name],\n\nThanks for reaching out.\nI\'ll review and follow up shortly.\n\nBest,\nEduardo',
  },
];

export function VoiceMacrosSection() {
  const { triggerProgress, outputProgress, arrowActive, macroIdx } = useVoiceMacrosScroll();

  const macro = MACROS[macroIdx];
  const triggerChars = Math.floor(triggerProgress * macro.trigger.length);
  const outputChars = Math.floor(outputProgress * macro.output.length);
  const showTriggerCursor = triggerProgress > 0 && triggerProgress < 1;
  const showOutputCursor = outputProgress > 0 && outputProgress < 1;

  return (
    <div id="macros-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-l from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Text (Left) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
              PRODUCTIVITY
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              Say Less.
              <br />
              Type More.
            </h2>
            <p className="text-xl text-muted mb-8">
              Define voice-triggered snippets that expand into full text blocks instantly.
              Signatures, templates, addresses — speak the trigger, get the output.
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>Voice Triggers</strong> — &quot;Inject [name]&quot; pattern
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>Instant Expansion</strong> — Multi-line output in milliseconds
              </li>
              <li className="flex items-center gap-3">
                <span className="text-orange-400">✓</span> <strong>Unlimited Snippets</strong> — Create as many as you need
              </li>
            </ul>
          </div>

          {/* Card (Right) */}
          <div className="card border-primary/20 bg-black/50 p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4">
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">Voice Macros</span>
              <span className={`w-3 h-3 rounded-full transition-colors duration-300 ${
                triggerProgress > 0 ? 'bg-green-500 animate-pulse' : 'bg-white/20'
              }`}></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              {/* Voice Input */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-primary">●</span> Voice Input
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded min-h-[36px]">
                  <span className="text-primary">
                    {macro.trigger.slice(0, triggerChars)}
                    {showTriggerCursor && (
                      <span className="inline-block w-[2px] h-3 bg-primary ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                </div>
              </div>

              {/* Arrow */}
              <div className={`text-center text-muted transition-opacity duration-300 ${
                arrowActive ? 'opacity-100' : 'opacity-30'
              }`}>
                ↓ expands to
              </div>

              {/* Output */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-green-400">●</span> Injected Text
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded min-h-[100px]">
                  <span className="text-muted whitespace-pre-line">
                    {macro.output.slice(0, outputChars)}
                    {showOutputCursor && (
                      <span className="inline-block w-[2px] h-3 bg-muted ml-px animate-pulse align-middle"></span>
                    )}
                  </span>
                </div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 text-center text-[10px] text-muted italic">
              &quot;{MACROS.length} snippets configured&quot;
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
