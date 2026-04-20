'use client';

import { useScrollReveal } from '@/lib/animations/useScrollReveal';
import { GlassCard } from './GlassCard';

export interface PluginPhase {
  phase: string;
  icon: string;
  title: string;
  subtitle: string;
  items: string[];
  status: string;
  statusColor: string;
}

interface Props {
  title: string;
  subtitle: string;
  phases: PluginPhase[];
  endNote: string;
}

export function PluginRoadmap({ title, subtitle, phases, endNote }: Props) {
  return (
    <section aria-labelledby="plugin-roadmap-heading">
      <div className="text-center mb-16">
        <div className="inline-block mb-4 px-3 py-1 rounded-full bg-primary/10 border border-primary/20">
          <span className="text-xs font-mono uppercase tracking-widest text-primary">The Plugin Roadmap</span>
        </div>
        <h2 id="plugin-roadmap-heading" className="text-3xl md:text-4xl font-bold text-white mb-3">
          {title}
        </h2>
        <p className="text-muted max-w-xl mx-auto">{subtitle}</p>
      </div>

      <div className="relative max-w-3xl mx-auto">
        {/* Spine — base track + animated fill */}
        <div className="absolute left-6 top-0 bottom-0 w-px bg-white/5" aria-hidden="true" />
        <SpineFill count={phases.length} />

        <div className="space-y-10">
          {phases.map((phase, i) => (
            <PhaseNode key={phase.phase} phase={phase} index={i} total={phases.length} />
          ))}
        </div>

        {/* End note */}
        <div className="flex gap-6 mt-10">
          <div className="flex-shrink-0 w-12 flex items-center justify-center">
            <div className="w-3 h-3 rounded-full bg-muted/30 border border-white/10" />
          </div>
          <p className="text-sm text-muted/60 italic self-center">{endNote}</p>
        </div>
      </div>
    </section>
  );
}

/**
 * Tracks which phase nodes are currently revealed and grows a gradient spine
 * that stops just past the last visible node. Feels like the circuit is being
 * completed as the user reads down.
 */
function SpineFill({ count }: { count: number }) {
  // Spine starts filling slightly before the first card reveals so the line
  // appears to lead the cards down the page rather than chase them.
  const { ref, isVisible } = useScrollReveal({
    threshold: 0,
    once: false,
    rootMargin: '0px 0px -25% 0px',
  });
  return (
    <div
      ref={ref}
      className="absolute left-6 top-0 w-px overflow-hidden pointer-events-none"
      style={{ height: isVisible ? '100%' : '0%', transition: `height ${count * 400}ms ease-out 200ms` }}
      aria-hidden="true"
    >
      <div className="w-full h-full bg-gradient-to-b from-primary/60 via-primary/30 to-transparent" />
    </div>
  );
}

/**
 * Single phase card with a "plug in" reveal. Each card observes its own
 * viewport intersection and animates independently, so fast scrollers see
 * them snap in sequence and slow scrollers see them catch up smoothly.
 */
function PhaseNode({ phase, index }: { phase: PluginPhase; index: number; total: number }) {
  // Delay the reveal so the card is well into the viewport before it animates.
  // rootMargin shrinks the bottom of the viewport by 30%, meaning a card must
  // scroll past ~70% of the viewport height before the observer fires.
  const { ref, isVisible } = useScrollReveal({
    threshold: 0,
    once: false,
    rootMargin: '0px 0px -40% 0px',
  });

  return (
    <div
      ref={ref}
      className={`flex gap-6 group transition-all ease-out ${
        isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-12'
      }`}
      style={{ transitionDuration: '700ms', transitionDelay: `${index * 80}ms` }}
    >
      {/* Node circle — lights up when its card enters */}
      <div className="relative flex-shrink-0">
        <div
          className={`w-12 h-12 rounded-full flex items-center justify-center text-xl z-10 relative transition-all duration-500 ${
            isVisible
              ? 'bg-primary/15 border border-primary/50 shadow-[0_0_24px_rgba(59,130,246,0.35)]'
              : 'bg-surface border border-white/10'
          }`}
        >
          {phase.icon}
        </div>
        {/* Subtle pulse ring at reveal */}
        {isVisible && (
          <div
            className="absolute inset-0 rounded-full border border-primary/30 animate-ping"
            style={{ animationDuration: '1400ms', animationIterationCount: 1 }}
            aria-hidden="true"
          />
        )}
      </div>

      {/* Card */}
      <GlassCard className="flex-1 group-hover:border-white/15 transition-all duration-300">
        <div className="flex flex-wrap items-center gap-3 mb-3">
          <span className="text-[10px] font-mono uppercase tracking-widest text-muted">{phase.phase}</span>
          <span
            className={`text-[10px] font-mono uppercase tracking-widest px-2 py-0.5 rounded-full border ${phase.statusColor}`}
          >
            {phase.status}
          </span>
        </div>
        <h3 className="text-xl font-bold text-white mb-1">{phase.title}</h3>
        <p className="text-sm text-muted mb-4 leading-relaxed">{phase.subtitle}</p>
        <ul className="space-y-2">
          {phase.items.map((item) => (
            <li key={item} className="flex items-start gap-2 text-sm text-gray-300">
              <span className="text-primary mt-0.5 flex-shrink-0">›</span>
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </GlassCard>
    </div>
  );
}
