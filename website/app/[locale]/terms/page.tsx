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
    title: t('termsTitle'),
    description: t('termsDescription'),
    alternates: {
      canonical: `https://dikta.me/${locale}/terms`,
      languages: {
        en: 'https://dikta.me/terms',
        es: 'https://dikta.me/es/terms',
      },
    },
  };
}

export default async function TermsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('TermsPage');
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
                <p>
                  {t('introP2')}
                </p>
              </section>

              {/* License */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('licenseTitle')}</h2>
                <p>
                  {t('licenseP1')}
                </p>
                <p className="mt-4">
                  {t('licenseP2')}
                </p>
              </section>

              {/* Pricing */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('pricingTitle')}</h2>
                <p>
                  {t('pricingP1')}
                </p>
                <p className="mt-4">
                  {t('pricingP2')}
                </p>
              </section>

              {/* Privacy */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('privacyTitle')}</h2>
                <p>
                  {t('privacyP1')}
                </p>
              </section>

              {/* Disclaimer */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('disclaimerTitle')}</h2>
                <p>
                  {t('disclaimerP1')}
                </p>
              </section>

              {/* Support */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('supportTitle')}</h2>
                <p>
                  {t('supportP1')}
                </p>
              </section>

              {/* Termination */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('terminationTitle')}</h2>
                <p>
                  {t('terminationP1')}
                </p>
              </section>

              {/* Contact */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">{t('contactTitle')}</h2>
                <p>
                  {t('contactP1')}{' '}
                  <a href="mailto:legal@dikta.me" className="text-primary hover:underline">
                    legal@dikta.me
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
