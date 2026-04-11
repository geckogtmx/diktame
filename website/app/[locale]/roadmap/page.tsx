import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { GlassCard } from '@/app/components/GlassCard';
import { setRequestLocale, getTranslations } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('roadmapTitle'),
    description: t('roadmapDescription'),
    alternates: {
      canonical: `https://dikta.me/${locale}/roadmap`,
      languages: {
        en: 'https://dikta.me/roadmap',
        es: 'https://dikta.me/es/roadmap',
      },
    },
  };
}

export default async function RoadmapPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('RoadmapPage');

  const compareRows = [
    { metric: t('cmpArchLabel'), v1: t('cmpArchV1'), v2: t('cmpArchV2') },
    { metric: t('cmpMemoryLabel'), v1: t('cmpMemoryV1'), v2: t('cmpMemoryV2') },
    { metric: t('cmpStartupLabel'), v1: t('cmpStartupV1'), v2: t('cmpStartupV2') },
    { metric: t('cmpInstallerLabel'), v1: t('cmpInstallerV1'), v2: t('cmpInstallerV2') },
    { metric: t('cmpAudioLabel'), v1: t('cmpAudioV1'), v2: t('cmpAudioV2') },
    { metric: t('cmpSttLabel'), v1: t('cmpSttV1'), v2: t('cmpSttV2') },
    { metric: t('cmpLlmLabel'), v1: t('cmpLlmV1'), v2: t('cmpLlmV2') },
    { metric: t('cmpTtsLabel'), v1: t('cmpTtsV1'), v2: t('cmpTtsV2') },
    { metric: t('cmpSecretsLabel'), v1: t('cmpSecretsV1'), v2: t('cmpSecretsV2') },
    { metric: t('cmpTestsLabel'), v1: t('cmpTestsV1'), v2: t('cmpTestsV2') },
  ];

  const newFeatures = [
    { icon: '💬', label: t('f1Label'), title: t('f1Title'), desc: t('f1Desc') },
    { icon: '🔊', label: t('f2Label'), title: t('f2Title'), desc: t('f2Desc') },
    { icon: '🎙️', label: t('f3Label'), title: t('f3Title'), desc: t('f3Desc') },
    { icon: '🔇', label: t('f4Label'), title: t('f4Title'), desc: t('f4Desc') },
    { icon: '🧙', label: t('f5Label'), title: t('f5Title'), desc: t('f5Desc') },
    { icon: '👁️', label: t('f6Label'), title: t('f6Title'), desc: t('f6Desc') },
    { icon: '🔑', label: t('f7Label'), title: t('f7Title'), desc: t('f7Desc') },
    { icon: '🧪', label: t('f8Label'), title: t('f8Title'), desc: t('f8Desc') },
  ];

  const timeline = [
    {
      phase: t('tl2Phase'),
      icon: '🔌',
      title: t('tl2Title'),
      subtitle: t('tl2Subtitle'),
      items: [t('tl2Item1'), t('tl2Item2'), t('tl2Item3')],
      status: t('tl2Status'),
      statusColor: 'bg-blue-500/20 text-blue-300 border-blue-500/30',
    },
    {
      phase: t('tl3Phase'),
      icon: '🎙️',
      title: t('tl3Title'),
      subtitle: t('tl3Subtitle'),
      items: [t('tl3Item1'), t('tl3Item2'), t('tl3Item3')],
      status: t('tl3Status'),
      statusColor: 'bg-blue-500/20 text-blue-300 border-blue-500/30',
    },
    {
      phase: t('tl4Phase'),
      icon: '🧠',
      title: t('tl4Title'),
      subtitle: t('tl4Subtitle'),
      items: [t('tl4Item1'), t('tl4Item2'), t('tl4Item3')],
      status: t('tl4Status'),
      statusColor: 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30',
    },
    {
      phase: t('tl5Phase'),
      icon: '✍️',
      title: t('tl5Title'),
      subtitle: t('tl5Subtitle'),
      items: [t('tl5Item1'), t('tl5Item2'), t('tl5Item3')],
      status: t('tl5Status'),
      statusColor: 'bg-purple-500/20 text-purple-300 border-purple-500/30',
    },
    {
      phase: t('tl6Phase'),
      icon: '🤖',
      title: t('tl6Title'),
      subtitle: t('tl6Subtitle'),
      items: [t('tl6Item1'), t('tl6Item2'), t('tl6Item3')],
      status: t('tl6Status'),
      statusColor: 'bg-orange-500/20 text-orange-300 border-orange-500/30',
    },
  ];

  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-24">
        {/* Background glow */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[600px] h-[600px] bg-primary/5 blur-[120px] rounded-full pointer-events-none" aria-hidden="true" />

        <div className="section-container relative z-10">

          {/* ─── Hero ──────────────────────────────────────────────── */}
          <div className="text-center mb-20 animate-fadeInUp">
            <div className="inline-block mb-4 px-3 py-1 rounded-full bg-primary/10 border border-primary/20">
              <span className="text-xs font-mono uppercase tracking-widest text-primary">{t('badge')}</span>
            </div>
            <h1 className="text-4xl md:text-6xl font-bold mb-6 tracking-tight">
              <span className="text-gradient">{t('heroTitle')}</span>
            </h1>
            <p className="text-lg text-muted max-w-2xl mx-auto leading-relaxed">
              {t('heroSubtitle')}
            </p>
          </div>

          {/* ─── Section 1: The Leap (V1 vs V2) ───────────────────── */}
          <section className="mb-28" aria-labelledby="leap-heading">
            <div className="text-center mb-12">
              <h2 id="leap-heading" className="text-3xl md:text-4xl font-bold text-white mb-3">{t('leapTitle')}</h2>
              <p className="text-muted max-w-xl mx-auto">{t('leapSubtitle')}</p>
            </div>

            {/* Stat pills row */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-10">
              {[
                { label: t('statMemLabel'), v1: t('statMemV1'), v2: t('statMemV2'), unit: '' },
                { label: t('statStartLabel'), v1: t('statStartV1'), v2: t('statStartV2'), unit: '' },
                { label: t('statInstLabel'), v1: t('statInstV1'), v2: t('statInstV2'), unit: '' },
                { label: t('statTestsLabel'), v1: t('statTestsV1'), v2: t('statTestsV2'), unit: '' },
              ].map((s) => (
                <div key={s.label} className="card p-4 text-center">
                  <div className="text-[10px] font-mono uppercase tracking-widest text-muted mb-2">{s.label}</div>
                  <div className="flex items-center justify-center gap-3">
                    <span className="text-sm text-muted/50 line-through">{s.v1}</span>
                    <span className="text-xl font-bold text-primary">{s.v2}</span>
                  </div>
                </div>
              ))}
            </div>

            {/* Full comparison table */}
            <div className="card p-0 overflow-hidden">
              <table className="w-full text-left border-collapse" aria-label={t('cmpTableCaption')}>
                <caption className="sr-only">{t('cmpTableCaption')}</caption>
                <thead>
                  <tr className="border-b border-white/10 bg-white/5">
                    <th className="py-3 px-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-muted">{t('cmpHeaderMetric')}</th>
                    <th className="py-3 px-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-muted/50">{t('cmpHeaderV1')}</th>
                    <th className="py-3 px-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-primary">{t('cmpHeaderV2')}</th>
                  </tr>
                </thead>
                <tbody>
                  {compareRows.map((row, i) => (
                    <tr key={row.metric} className={`border-b border-white/5 hover:bg-white/3 transition-colors ${i % 2 === 0 ? 'bg-transparent' : 'bg-white/[0.02]'}`}>
                      <td className="py-3 px-4 md:px-6 text-sm text-white font-medium">{row.metric}</td>
                      <td className="py-3 px-4 md:px-6 text-sm text-muted/50">{row.v1}</td>
                      <td className="py-3 px-4 md:px-6 text-sm text-primary font-semibold">{row.v2}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* ─── Section 2: What's New in V2 ──────────────────────── */}
          <section className="mb-28" aria-labelledby="new-v2-heading">
            <div className="text-center mb-12">
              <h2 id="new-v2-heading" className="text-3xl md:text-4xl font-bold text-white mb-3">{t('newTitle')}</h2>
              <p className="text-muted max-w-xl mx-auto">{t('newSubtitle')}</p>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 md:gap-6">
              {newFeatures.map((f) => (
                <GlassCard key={f.title} className="hover:border-primary/30 transition-all duration-300 hover:-translate-y-1">
                  <div className="text-3xl mb-3">{f.icon}</div>
                  <div className="text-[10px] font-mono uppercase tracking-widest text-primary mb-1">{f.label}</div>
                  <h3 className="font-semibold text-white mb-2 leading-snug">{f.title}</h3>
                  <p className="text-xs text-muted leading-relaxed">{f.desc}</p>
                </GlassCard>
              ))}
            </div>
          </section>

          {/* ─── Section 3: What's Next (Timeline) ────────────────── */}
          <section aria-labelledby="next-heading">
            <div className="text-center mb-16">
              <h2 id="next-heading" className="text-3xl md:text-4xl font-bold text-white mb-3">{t('nextTitle')}</h2>
              <p className="text-muted max-w-xl mx-auto">{t('nextSubtitle')}</p>
            </div>

            <div className="relative max-w-3xl mx-auto">
              {/* Vertical line */}
              <div className="absolute left-6 top-0 bottom-0 w-px bg-gradient-to-b from-primary/40 via-primary/20 to-transparent" aria-hidden="true" />

              <div className="space-y-10">
                {timeline.map((node) => (
                  <div key={node.phase} className="flex gap-6 group">
                    {/* Circle node */}
                    <div className="relative flex-shrink-0">
                      <div className="w-12 h-12 rounded-full bg-surface border border-white/10 flex items-center justify-center text-xl z-10 relative group-hover:border-primary/40 transition-colors">
                        {node.icon}
                      </div>
                    </div>

                    {/* Card */}
                    <GlassCard className="flex-1 group-hover:border-white/15 transition-all duration-300">
                      <div className="flex flex-wrap items-center gap-3 mb-3">
                        <span className="text-[10px] font-mono uppercase tracking-widest text-muted">{node.phase}</span>
                        <span className={`text-[10px] font-mono uppercase tracking-widest px-2 py-0.5 rounded-full border ${node.statusColor}`}>
                          {node.status}
                        </span>
                      </div>
                      <h3 className="text-xl font-bold text-white mb-1">{node.title}</h3>
                      <p className="text-sm text-muted mb-4 leading-relaxed">{node.subtitle}</p>
                      <ul className="space-y-2">
                        {node.items.map((item) => (
                          <li key={item} className="flex items-start gap-2 text-sm text-gray-300">
                            <span className="text-primary mt-0.5 flex-shrink-0">›</span>
                            <span>{item}</span>
                          </li>
                        ))}
                      </ul>
                    </GlassCard>
                  </div>
                ))}
              </div>

              {/* End of timeline note */}
              <div className="flex gap-6 mt-10">
                <div className="flex-shrink-0 w-12 flex items-center justify-center">
                  <div className="w-3 h-3 rounded-full bg-muted/30 border border-white/10" />
                </div>
                <p className="text-sm text-muted/60 italic self-center">{t('timelineNote')}</p>
              </div>
            </div>
          </section>

        </div>
      </div>

      <Footer />
    </main>
  );
}
