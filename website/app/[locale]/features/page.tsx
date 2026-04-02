import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { GlassCard } from '@/app/components/GlassCard';
import { Link } from '@/i18n/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('featuresTitle'),
    description: t('featuresDescription'),
    alternates: {
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/features`,
      languages: {
        en: 'https://dikta.me/features',
        es: 'https://dikta.me/es/features',
      },
    },
  };
}

export default async function FeaturesPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('FeaturesPage');

  const featureCategories = [
    {
      category: t('catCoreModes'),
      features: [
        { name: t('featDictate'), description: t('featDictateDesc') },
        { name: t('featAsk'), description: t('featAskDesc') },
        { name: t('featRefine'), description: t('featRefineDesc') },
        { name: t('featVision'), description: t('featVisionDesc') },
        { name: t('featQuickChat'), description: t('featQuickChatDesc') },
      ],
    },
    {
      category: t('catIntelligence'),
      features: [
        { name: t('featWhisper'), description: t('featWhisperDesc') },
        { name: t('featLocalLlms'), description: t('featLocalLlmsDesc') },
        { name: t('featModelAgnostic'), description: t('featModelAgnosticDesc') },
        { name: t('featAudioDucking'), description: t('featAudioDuckingDesc') },
      ],
    },
    {
      category: t('catPrivacyTech'),
      features: [
        { name: t('featLocal'), description: t('featLocalDesc') },
        { name: t('featNativeBuild'), description: t('featNativeBuildDesc') },
        { name: t('featNoTelemetry'), description: t('featNoTelemetryDesc') },
        { name: t('featApiKeysSecured'), description: t('featApiKeysSecuredDesc') },
      ],
    },
    {
      category: t('catCustomization'),
      features: [
        { name: t('featHotkeys'), description: t('featHotkeysDesc') },
        { name: t('featSnippets'), description: t('featSnippetsDesc') },
        { name: t('featSoundFeedback'), description: t('featSoundFeedbackDesc') },
        { name: t('featKeyAutomation'), description: t('featKeyAutomationDesc') },
      ],
    },
    {
      category: t('catRoadmap'),
      features: [
        { name: t('featConnectors'), description: t('featConnectorsDesc') },
        { name: t('featMeetings'), description: t('featMeetingsDesc') },
        { name: t('featMemory'), description: t('featMemoryDesc') },
      ],
    },
  ];

  return (
    <main id="main-content" className="min-h-screen bg-black text-white">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify({
            '@context': 'https://schema.org',
            '@type': 'SoftwareApplication',
            name: 'dIKta.me',
            applicationCategory: 'ProductivityApplication',
            operatingSystem: 'Windows 10, Windows 11',
            description: locale === 'es'
              ? 'Motor de dictado por voz con IA local para Windows. 8 modos de flujo de trabajo, local-first, agnóstico de modelo.'
              : 'Local AI voice dictation engine for Windows. 8 workflow modes, local-first, model-agnostic.',
            url: 'https://dikta.me',
            offers: {
              '@type': 'Offer',
              price: '20',
              priceCurrency: 'USD',
              priceValidUntil: '2026-12-31',
            },
            featureList: locale === 'es'
              ? 'Dictado por voz, Refinamiento de texto con IA, Preguntas y respuestas, Traducción EN/ES, Notas de voz, Texto a voz, Chat con IA, Visión/OCR'
              : 'Voice dictation, AI text refinement, Q&A at cursor, EN/ES translation, Voice notes, Text-to-speech, AI chat, Vision/OCR',
          }),
        }}
      />

      <Navbar />

      {/* Hero */}
      <section className="pt-32 pb-20 sm:pb-32 relative overflow-hidden">
        <div className="absolute inset-0 -z-10">
          <div className="absolute inset-0 bg-gradient-to-b from-blue-900/20 via-transparent to-transparent" />
          <div className="absolute top-1/4 right-0 w-96 h-96 bg-blue-600/10 rounded-full blur-3xl opacity-40" />
        </div>

        <Container>
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-5xl sm:text-6xl md:text-7xl font-bold mb-6">
              {t('heroTitle')}
            </h1>
            <p className="text-xl text-gray-400 mb-8">
              {t('heroSubtitle')}
            </p>
          </div>
        </Container>
      </section>

      {/* Feature Categories */}
      <section className="py-20 sm:py-32">
        <Container>
          <div className="space-y-20">
            {featureCategories.map((category) => (
              <div key={category.category}>
                <h2 className="text-3xl sm:text-4xl font-bold text-white mb-12">
                  {category.category}
                </h2>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {category.features.map((feature) => (
                    <GlassCard key={feature.name}>
                      <h3 className="text-xl font-semibold text-white mb-2">
                        {feature.name}
                      </h3>
                      <p className="text-gray-400">{feature.description}</p>
                    </GlassCard>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </Container>
      </section>

      {/* CTA */}
      <section className="py-20 sm:py-32 border-t border-white/10">
        <Container>
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-4xl sm:text-5xl font-bold text-white mb-6">
              {t('ctaTitle')}
            </h2>
            <p className="text-lg text-gray-400 mb-8">
              {t('ctaSubtitle')}
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <Link
                href="/waitlist"
                className="px-8 py-3 bg-orange-500 hover:bg-orange-600 rounded-lg font-semibold transition-colors"
              >
                {t('ctaJoinWaitlist')}
              </Link>
              <Link
                href="/login"
                className="px-8 py-3 border border-blue-500/20 text-blue-400 hover:bg-blue-500/10 rounded-lg font-semibold transition-colors"
              >
                {t('ctaViewDashboard')}
              </Link>
            </div>
          </div>
        </Container>
      </section>

      <Footer />
    </main>
  );
}
