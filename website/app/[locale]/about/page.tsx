import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
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
    title: t('aboutTitle'),
    description: t('aboutDescription'),
    alternates: {
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/about`,
      languages: {
        en: 'https://dikta.me/about',
        es: 'https://dikta.me/es/about',
      },
    },
  };
}

export default async function AboutPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('AboutPage');
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-3xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-12 tracking-tight">{t('title')}</h1>

            <div className="prose prose-invert max-w-none space-y-10 text-gray-300 leading-relaxed">
              {/* What */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('whatTitle')}</h2>
                <p className="text-lg">
                  {t('whatP1')}
                </p>
                <p>
                  {t('whatP2')}
                </p>
              </section>

              {/* Why */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('whyTitle')}</h2>
                <p>
                  {t('whyP1')}
                </p>
                <p>
                  {t('whyP2')}
                </p>
              </section>

              {/* How */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('howTitle')}</h2>
                <p>
                  {t('howP1')}
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li>
                    <strong className="text-white">{t('howStt')}</strong>{' '}
                    {t('howSttDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('howAi')}</strong>{' '}
                    {t('howAiDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('howTts')}</strong>{' '}
                    {t('howTtsDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('howCloud')}</strong>{' '}
                    {t('howCloudDesc')}
                  </li>
                </ul>
                <p className="mt-4">
                  {t('howP2')}
                </p>
              </section>

              {/* Who */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('whoTitle')}</h2>
                <p>
                  {t('whoP1')}
                </p>
              </section>

              {/* Support */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('supportTitle')}</h2>
                <p>
                  {t('supportP1')}
                </p>
                <div className="flex flex-wrap gap-4 mt-6">
                  <Link
                    href="/waitlist"
                    className="px-6 py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/80 transition-colors"
                  >
                    {t('joinWaitlist')}
                  </Link>
                  <a
                    href="https://ko-fi.com/geckogtmx"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="px-6 py-3 bg-white/5 border border-white/20 text-white font-bold rounded-lg hover:bg-white/10 transition-colors"
                  >
                    {t('supportKoFi')}
                  </a>
                </div>
              </section>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
