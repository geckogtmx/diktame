import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { PricingSection } from '@/app/components/PricingSection';
import { setRequestLocale, getTranslations } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('pricingTitle'),
    description: t('pricingDescription'),
    alternates: {
      canonical: `https://dikta.me/${locale}/pricing`,
      languages: {
        en: 'https://dikta.me/pricing',
        es: 'https://dikta.me/es/pricing',
      },
    },
  };
}

export default async function PricingPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  return (
    <main id="main-content" className="min-h-screen bg-black text-white">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{
          __html: JSON.stringify({
            '@context': 'https://schema.org',
            '@type': 'Product',
            name: 'dIKta.me',
            description: locale === 'es'
              ? 'Dictado por voz con IA local para Windows. Pago único, sin suscripciones.'
              : 'Local AI voice dictation for Windows. One-time purchase, no subscriptions.',
            brand: { '@type': 'Organization', name: 'dIKta.me' },
            offers: [
              {
                '@type': 'Offer',
                name: locale === 'es' ? 'Prueba Gratuita' : 'Free Trial',
                price: '0',
                priceCurrency: 'USD',
                availability: 'https://schema.org/InStock',
              },
              {
                '@type': 'Offer',
                name: locale === 'es' ? 'Versión Completa' : 'Full Version',
                price: '20',
                priceCurrency: 'USD',
                availability: 'https://schema.org/InStock',
              },
            ],
          }),
        }}
      />

      <Navbar />

      {/* Pricing Section starts at top */}
      <div className="pt-20">
        <PricingSection asH1 />
      </div>

      <Footer />
    </main>
  );
}
