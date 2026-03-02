'use client';

import { useVersusScroll } from '@/lib/animations/useVersusScroll';

export function VersusSection() {
  const { visibleRows } = useVersusScroll();

  return (
    <div id="versus-track" className="relative h-[250vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="section-container relative z-10 w-full max-w-5xl">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white">dIKta.me Versus The Cloud.</h2>
            <p className="text-muted">Why settle for rental when you can own the factory?</p>
          </div>

          <div className="card overflow-hidden bg-black/40 backdrop-blur-xl border-white/5 p-0">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-white/10 bg-white/5">
                  <th className="py-4 px-6 text-sm font-mono uppercase tracking-widest text-muted">Feature</th>
                  <th className="py-4 px-6 text-sm font-mono uppercase tracking-widest text-muted">The Others</th>
                  <th className="py-4 px-6 text-sm font-mono uppercase tracking-widest text-primary">dIKta.me</th>
                </tr>
              </thead>
              <tbody className="text-sm md:text-base">
                <tr
                  id="vs-row-1"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 1 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Privacy</td>
                  <td className="py-4 px-6 text-muted">Sent to servers</td>
                  <td className="py-4 px-6 text-primary font-bold">100% Local (Air-Gapped)</td>
                </tr>
                <tr
                  id="vs-row-2"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 2 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Speed</td>
                  <td className="py-4 px-6 text-[#94a3b8]">800-1200ms Latency/Inference</td>
                  <td className="py-4 px-6 text-[#2563eb] font-bold">~400ms (Local GPU Inference - gemma3:1b *)</td>
                </tr>
                <tr
                  id="vs-row-3"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 3 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Consistency</td>
                  <td className="py-4 px-6 text-muted">Varies by Web Traffic</td>
                  <td className="py-4 px-6 text-primary font-bold">Hardware-Bound (Stable)</td>
                </tr>
                <tr
                  id="vs-row-4"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 4 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Censorship</td>
                  <td className="py-4 px-6 text-muted">Aggressive Filtering</td>
                  <td className="py-4 px-6 text-primary font-bold">Zero Restrictions</td>
                </tr>
                <tr
                  id="vs-row-5"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 5 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Cost</td>
                  <td className="py-4 px-6 text-muted">$240 / year</td>
                  <td className="py-4 px-6 text-primary font-bold">$10 - $25 (Once)</td>
                </tr>
                <tr
                  id="vs-row-6"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 6 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Word Limits</td>
                  <td className="py-4 px-6 text-[#94a3b8]">Capped</td>
                  <td className="py-4 px-6 text-[#2563eb] font-bold">Unlimited while on Local</td>
                </tr>
                <tr
                  id="vs-row-7"
                  className={`transition-all duration-500 ${visibleRows >= 7 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-4 px-6 text-white font-medium">Offline Use</td>
                  <td className="py-4 px-6 text-muted">Requires WiFi</td>
                  <td className="py-4 px-6 text-primary font-bold">Works in a Bunker</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
