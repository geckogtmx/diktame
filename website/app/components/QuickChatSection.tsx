'use client';

import { useQuickChatScroll } from '@/lib/animations/useQuickChatScroll';

const USER_MSG_1 = 'Who would you consider the most prominent five on AI in the last decade or two?';
const USER_MSG_2 = 'Tell me more about reinforcement learning.';

export function QuickChatSection() {
  const {
    userMsg1Progress,
    aiMsg1Progress,
    userMsg2Progress,
    thinkingVisible,
    aiMsg2Progress,
  } = useQuickChatScroll();

  const user1Chars = Math.floor(userMsg1Progress * USER_MSG_1.length);
  const user2Chars = Math.floor(userMsg2Progress * USER_MSG_2.length);

  return (
    <div id="quickchat-track" className="relative h-[300vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-r from-primary/5 to-transparent opacity-30"></div>
        <div className="section-container grid md:grid-cols-2 gap-12 items-center relative z-10">
          {/* Text (Left) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
              QUICK CHAT
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              Your AI,
              <br />
              Always On Call.
            </h2>
            <p className="text-xl text-muted mb-8">
              A floating chat overlay powered by any model — local or cloud.
              Rich Markdown responses, conversation history, and zero context switching.
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>Floating Overlay</strong> — Stays above every app
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>Model Agnostic</strong> — GPT, Claude, Gemini, Ollama
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>Rich Output</strong> — Markdown, code blocks, links
              </li>
            </ul>
          </div>

          {/* Chat Window (Right) */}
          <div>
            <div className="card border-primary/20 bg-black/50 shadow-2xl relative overflow-hidden rounded-xl flex flex-col" style={{ maxHeight: '480px' }}>
              {/* Title Bar */}
              <div className="flex items-center justify-between px-3 py-2 bg-gray-900/80 border-b border-white/10">
                <div className="flex items-center gap-2">
                  <div className="w-4 h-4 rounded-full bg-primary flex items-center justify-center text-[8px] font-bold text-white">K</div>
                  <span className="text-xs text-white font-medium">Quick Chat</span>
                </div>
                <div className="flex items-center gap-2 text-white/40 text-xs">
                  <span>—</span>
                  <span>□</span>
                  <span>✕</span>
                </div>
              </div>

              {/* Toolbar */}
              <div className="flex items-center justify-between px-3 py-1.5 bg-gray-900/60 border-b border-white/10">
                <div className="flex items-center gap-1 text-[10px] text-white/70 bg-white/5 px-2 py-1 rounded">
                  <span>gpt-5.2-pro</span>
                  <span className="text-white/40">▾</span>
                </div>
                <div className="flex items-center gap-2 text-white/40 text-[10px]">
                  <span>◷</span>
                  <span>⚙</span>
                  <span className="text-sky-400">⊕</span>
                  <span>⊡</span>
                  <span>+</span>
                </div>
              </div>

              {/* Chat Area */}
              <div className="flex-1 px-3 py-3 space-y-3 overflow-hidden text-xs min-h-[320px]">
                {/* User Message 1 */}
                {userMsg1Progress > 0 && (
                  <div className="flex justify-end transition-all duration-300" style={{
                    opacity: Math.min(userMsg1Progress * 5, 1),
                    transform: `translateY(${(1 - Math.min(userMsg1Progress * 5, 1)) * 8}px)`,
                  }}>
                    <div className="bg-sky-500 text-white rounded-2xl rounded-br-sm px-3 py-2 max-w-[85%]">
                      {USER_MSG_1.slice(0, user1Chars)}
                      {userMsg1Progress < 1 && userMsg1Progress > 0 && (
                        <span className="inline-block w-[2px] h-3 bg-white/80 ml-px animate-pulse align-middle"></span>
                      )}
                    </div>
                  </div>
                )}

                {/* AI Response 1 */}
                {aiMsg1Progress > 0 && (
                  <div className="transition-all duration-500" style={{
                    opacity: aiMsg1Progress,
                    transform: `translateY(${(1 - aiMsg1Progress) * 12}px)`,
                  }}>
                    <div className="bg-white/5 border border-white/10 rounded-lg p-3 text-white/80 space-y-1.5">
                      <p className="text-white/60 text-[10px] leading-relaxed">
                        Here&apos;s a list of five prominent AI developments in the last decade or two, considering impact and influence:
                      </p>
                      <ol className="list-decimal list-inside space-y-1 text-[10px] leading-relaxed">
                        <li><strong className="text-white">Deep Learning:</strong> <span className="text-white/60">Revolutionized many fields with neural networks.</span></li>
                        <li><strong className="text-white">Natural Language Processing (NLP):</strong> <span className="text-white/60">Significant advancements in chatbots, translation, and text analysis.</span></li>
                        <li><strong className="text-white">Computer Vision:</strong> <span className="text-white/60">Improved image and video recognition.</span></li>
                        <li><strong className="text-white">Reinforcement Learning:</strong> <span className="text-white/60">Enabled breakthroughs in robotics and game playing.</span></li>
                        <li><strong className="text-white">Generative AI (Large Language Models):</strong> <span className="text-white/60">Like GPT, creating new content and automating tasks.</span></li>
                      </ol>
                      <p className="text-white/40 text-[10px]">
                        It&apos;s a complex field, and this is a general consensus based on widespread impact.
                      </p>
                    </div>
                  </div>
                )}

                {/* User Message 2 */}
                {userMsg2Progress > 0 && (
                  <div className="flex justify-end transition-all duration-300" style={{
                    opacity: Math.min(userMsg2Progress * 5, 1),
                    transform: `translateY(${(1 - Math.min(userMsg2Progress * 5, 1)) * 8}px)`,
                  }}>
                    <div className="bg-sky-500 text-white rounded-2xl rounded-br-sm px-3 py-2 max-w-[85%]">
                      {USER_MSG_2.slice(0, user2Chars)}
                      {userMsg2Progress < 1 && userMsg2Progress > 0 && (
                        <span className="inline-block w-[2px] h-3 bg-white/80 ml-px animate-pulse align-middle"></span>
                      )}
                    </div>
                  </div>
                )}

                {/* Thinking Indicator */}
                {thinkingVisible && (
                  <div className="flex items-center gap-2 text-white/50 text-[10px] py-1 transition-opacity duration-300">
                    <span className="inline-block w-3 h-3 border-2 border-primary border-t-transparent rounded-full animate-spin"></span>
                    Thinking...
                  </div>
                )}

                {/* AI Response 2 */}
                {aiMsg2Progress > 0 && (
                  <div className="relative transition-all duration-500" style={{
                    opacity: aiMsg2Progress,
                    transform: `translateY(${(1 - aiMsg2Progress) * 12}px)`,
                  }}>
                    <div className="bg-white/5 border border-white/10 rounded-lg p-3 text-white/80 space-y-1.5">
                      <p className="text-[10px] leading-relaxed text-white/60">
                        Okay, let&apos;s dive into Reinforcement Learning (RL). Here&apos;s a breakdown:
                      </p>
                      <p className="text-[10px] font-bold text-white">What is it?</p>
                      <p className="text-[10px] leading-relaxed text-white/60">
                        At its core, Reinforcement Learning is a type of machine learning where an <em className="text-white/80">agent</em> learns to make decisions in an <em className="text-white/80">environment</em> to maximize a <em className="text-white/80">reward</em>. Think of it like training a dog — you don&apos;t tell the dog <em className="text-white/80">exactly</em> what to do, you reward it for good behavior and discourage it for bad.
                      </p>
                      <p className="text-[10px] font-bold text-white">Here&apos;s a simplified explanation:</p>
                      <ol className="list-decimal list-inside space-y-0.5 text-[10px] leading-relaxed text-white/60">
                        <li><strong className="text-white/80">Agent:</strong> This is the learner — a robot, a program, or even a game-playing AI.</li>
                        <li><strong className="text-white/80">Environment:</strong> The world the agent interacts with — a game, a robot&apos;s surroundings, or a simulated scenario.</li>
                      </ol>
                    </div>
                    {/* Bottom fade — implies more content below */}
                    <div className="absolute bottom-0 left-0 right-0 h-8 bg-gradient-to-t from-black/80 to-transparent rounded-b-lg"></div>
                  </div>
                )}
              </div>

              {/* Input Bar */}
              <div className="flex items-center gap-2 px-3 py-2 border-t border-white/10 bg-gray-900/40">
                <span className="text-white/30 text-xs">📎</span>
                <div className="flex-1 bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-[10px] text-white/30">
                  Type a message...
                </div>
                <div className="w-6 h-6 bg-sky-500 rounded flex items-center justify-center text-white text-[10px]">
                  ▶
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </div>
  );
}
