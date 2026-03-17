import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { GlassCard } from '@/app/components/GlassCard';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('docsTitle'),
    description: t('docsDescription'),
    alternates: {
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/docs`,
      languages: {
        en: 'https://dikta.me/docs',
        es: 'https://dikta.me/es/docs',
      },
    },
  };
}

export default async function DocsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('DocsPage');

  const userDocs = [
    {
      title: t('userDocsTitle'),
      description: t('userDocsDesc'),
      links: [
        { href: '/docs/getting-started', label: t('gettingStartedGuide') },
        { href: '/docs/troubleshooting', label: t('troubleshooting') },
      ]
    },
    {
      title: t('coreFeaturesTitle'),
      description: t('coreFeaturesDesc'),
      links: [
        { href: '/docs/features/dictation', label: t('dictation') },
        { href: '/docs/features/refine', label: t('refineMode') },
        { href: '/docs/features/quick-chat', label: t('quickChat') },
      ]
    },
    {
      title: t('utilityPipelinesTitle'),
      description: t('utilityPipelinesDesc'),
      links: [
        { href: '/docs/features/ask', label: t('askMode') },
        { href: '/docs/features/translate', label: t('translateMode') },
        { href: '/docs/features/note', label: t('noteMode') },
        { href: '/docs/features/oops', label: t('textToSpeech') },
      ]
    },
    {
      title: t('settingsTitle'),
      description: t('settingsDesc'),
      links: [
        { href: '/docs/settings/general', label: t('generalSettings') },
        { href: '/docs/settings/ai-engine', label: t('aiEngineConfig') },
        { href: '/docs/settings/hotkeys', label: t('hotkeySetup') },
        { href: '/docs/settings/modes', label: t('dictationModeConfig') },
      ]
    }
  ];

  const devDocs = [
    {
      title: t('devGuideTitle'),
      description: t('devGuideDesc'),
      links: [
        { href: '/docs/dev/setup', label: t('envSetup') },
        { href: '/docs/dev/architecture/ui-mvvm', label: t('archPatterns') },
        { href: '/docs/dev/api/stt-providers', label: t('apiExtensibility') },
        { href: '/docs/dev/migration/v1-to-v2-guide', label: t('migrationGuide') },
      ]
    }
  ];

  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Hero */}
      <section className="pt-32 pb-10 sm:pb-12 relative overflow-hidden">
        <div className="absolute inset-0 -z-10">
          <div className="absolute inset-0 bg-gradient-to-b from-blue-900/20 via-transparent to-transparent" />
        </div>

        <Container>
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-5xl sm:text-6xl md:text-7xl font-bold mb-6">
              {t('heroTitle')}
            </h1>
            <p className="text-xl text-gray-400">
              {t('heroSubtitle')}
            </p>
          </div>
        </Container>
      </section>

      {/* Documentation Grid */}
      <section className="py-10 sm:py-16">
        <Container>
          {/* User Documentation Section */}
          <div className="mb-12">
            <h2 className="text-3xl font-bold text-white mb-4">{t('userDocsTitle')}</h2>
            <p className="text-gray-400">{t('userDocsDesc')}</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-16">
            {userDocs.map((doc) => (
              <GlassCard key={doc.title}>
                <Link href={doc.links[0].href} className="text-2xl font-bold text-white mb-3 block hover:text-blue-400 transition">
                  <h2>{doc.title}</h2>
                </Link>
                <p className="text-gray-400 mb-6">{doc.description}</p>
                <ul className="space-y-2">
                  {doc.links.map((link) => (
                    <li key={link.href} className="flex items-center gap-2 text-gray-300">
                      <span className="text-blue-400">→</span>
                      <Link href={link.href} className="hover:text-blue-400 transition">
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </GlassCard>
            ))}
          </div>

          {/* Developer Documentation + Quick Start */}
          <div className="mt-24 border-t border-white/10 pt-16">
            <h2 className="text-3xl font-bold text-white mb-4">{t('devGuideTitle')}</h2>
            <p className="text-gray-400 mb-12">{t('devGuideDesc')}</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-16">
            {devDocs.map((doc) => (
              <GlassCard key={doc.title}>
                <Link href={doc.links[0].href} className="text-2xl font-bold text-white mb-3 block hover:text-blue-400 transition">
                  <h2>{doc.title}</h2>
                </Link>
                <p className="text-gray-400 mb-6">{doc.description}</p>
                <ul className="space-y-2">
                  {doc.links.map((link) => (
                    <li key={link.href} className="flex items-center gap-2 text-gray-300">
                      <span className="text-blue-400">→</span>
                      <Link href={link.href} className="hover:text-blue-400 transition">
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </GlassCard>
            ))}

            {/* Quick Start — distinct card beside Developer Guide */}
            <div className="rounded-2xl border border-orange-500/20 bg-gradient-to-br from-orange-950/30 to-black/50 p-8 backdrop-blur-sm">
              <h3 className="text-2xl font-bold text-white mb-6">{t('quickStartTitle')}</h3>
              <ol className="space-y-5">
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center font-bold text-sm">
                    1
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">{t('quickStartStep1Title')}</h4>
                    <p className="text-gray-400 text-sm">
                      {t('quickStartStep1Desc')}
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center font-bold text-sm">
                    2
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">{t('quickStartStep2Title')}</h4>
                    <p className="text-gray-400 text-sm">
                      {t('quickStartStep2Desc')}
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center font-bold text-sm">
                    3
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">{t('quickStartStep3Title')}</h4>
                    <p className="text-gray-400 text-sm">
                      {t('quickStartStep3Desc')}
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-orange-500 text-white flex items-center justify-center font-bold text-sm">
                    4
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">{t('quickStartStep4Title')}</h4>
                    <p className="text-gray-400 text-sm">
                      {t('quickStartStep4Desc')}
                    </p>
                  </div>
                </li>
              </ol>
            </div>
          </div>
        </Container>
      </section>

      {/* External Resources */}
      <section className="py-16 sm:py-20 border-t border-white/10">
        <Container>
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-4xl font-bold text-white mb-6">{t('needMoreHelp')}</h2>
            <p className="text-lg text-gray-400 mb-8">
              {t('needMoreHelpDesc')}
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <a
                href="https://github.com/geckogtmx/diktame"
                className="px-8 py-3 bg-gray-800 hover:bg-gray-700 rounded-lg font-semibold transition-colors"
              >
                {t('viewOnGithub')}
              </a>
              <a
                href="https://github.com/geckogtmx/diktame/issues"
                className="px-8 py-3 border border-blue-500/20 text-blue-400 hover:bg-blue-500/10 rounded-lg font-semibold transition-colors"
              >
                {t('reportIssue')}
              </a>
            </div>
          </div>
        </Container>
      </section>

      <Footer />
    </main>
  );
}
