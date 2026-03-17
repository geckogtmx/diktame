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
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/pricing`,
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
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Pricing Section starts at top */}
      <div className="pt-20">
        <PricingSection />
      </div>

      <Footer />
    </main>
  );
}
