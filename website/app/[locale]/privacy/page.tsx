import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { setRequestLocale, getTranslations } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('privacyTitle'),
    description: t('privacyDescription'),
    alternates: {
      canonical: `https://dikta.me/${locale}/privacy`,
      languages: {
        en: 'https://dikta.me/privacy',
        es: 'https://dikta.me/es/privacy',
      },
    },
  };
}

export default async function PrivacyPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('PrivacyPage');
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-3xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-4 tracking-tight">{t('title')}</h1>
            <p className="text-muted mb-12">{t('lastUpdated')}</p>

            <div className="prose prose-invert max-w-none space-y-10 text-gray-300 leading-relaxed">
              {/* Intro */}
              <section>
                <p className="text-lg">
                  {t('introP1')}
                </p>
                <p className="text-lg font-semibold text-white">
                  {t('introHighlight')}
                </p>
                <p>
                  {t('introP2')}
                </p>
              </section>

              {/* The Desktop App */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('desktopTitle')}</h2>
                <p>
                  {t('desktopP1')}
                </p>
              </section>

              {/* What the Website Collects */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('websiteTitle')}</h2>
                <p>
                  {t('websiteP1')}
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li>{t('websiteItem1')}</li>
                  <li>{t('websiteItem2')}</li>
                  <li>{t('websiteItem3')}</li>
                </ul>
                <p className="mt-4">
                  {t('websiteP2')}
                </p>
              </section>

              {/* Cloud Features */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('cloudTitle')}</h2>
                <p>
                  {t('cloudP1')}
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li>{t('cloudItem1')}</li>
                  <li>{t('cloudItem2')}</li>
                  <li>{t('cloudItem3')}</li>
                </ul>
                <p className="mt-4">
                  {t('cloudP2')}
                </p>
              </section>

              {/* Your Data */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('yourDataTitle')}</h2>
                <ul className="list-disc pl-6 space-y-2">
                  <li>{t('yourDataItem1')}</li>
                  <li>{t('yourDataItem2')}</li>
                  <li>{t('yourDataItem3')}</li>
                </ul>
              </section>

              {/* Contact */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('contactTitle')}</h2>
                <p>
                  {t('contactP1')}{' '}
                  <a href="mailto:privacy@dikta.me" className="text-primary hover:underline">
                    privacy@dikta.me
                  </a>
                </p>
              </section>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
