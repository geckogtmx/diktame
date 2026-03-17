import { NextIntlClientProvider } from 'next-intl';
import { getMessages, getTranslations, setRequestLocale } from 'next-intl/server';
import { Plus_Jakarta_Sans } from 'next/font/google';
import { Analytics } from '@vercel/analytics/next';
import { SpeedInsights } from '@vercel/speed-insights/next';
import { locales } from '@/i18n/config';
import '../globals.css';

const plusJakarta = Plus_Jakarta_Sans({ subsets: ['latin'] });

export function generateStaticParams() {
  return locales.map((locale) => ({ locale }));
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    metadataBase: new URL('https://dikta.me'),
    title: t('title'),
    description: t('description'),
    keywords: ['voice dictation', 'speech recognition', 'AI', 'local AI', 'privacy', 'Windows'],
    authors: [{ name: 'dIKta.me Team' }],
    creator: 'dIKta.me',
    publisher: 'dIKta.me',
    robots: 'index, follow',
    openGraph: {
      type: 'website',
      locale: locale === 'es' ? 'es_ES' : 'en_US',
      url: '/',
      title: t('title'),
      description: t('description'),
      siteName: 'dIKta.me',
      images: [
        {
          url: '/og-image.png',
          width: 1200,
          height: 630,
          alt: 'dIKta.me - AI Voice Dictation',
        },
      ],
    },
    twitter: {
      card: 'summary_large_image',
      title: t('title'),
      description: t('description'),
      images: ['/og-image.png'],
    },
    alternates: {
      canonical: locale === 'en' ? 'https://dikta.me' : 'https://dikta.me/es',
      languages: {
        en: 'https://dikta.me',
        es: 'https://dikta.me/es',
      },
    },
  };
}

export default async function LocaleLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const messages = await getMessages();

  return (
    <html lang={locale} className="dark">
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=5" />
        <meta name="theme-color" content="#020617" />
        <link rel="canonical" href={locale === 'en' ? 'https://dikta.me' : 'https://dikta.me/es'} />
      </head>
      <body className={plusJakarta.className}>
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{
            __html: JSON.stringify([
              {
                '@context': 'https://schema.org',
                '@type': 'SoftwareApplication',
                name: 'dIKta.me',
                description: locale === 'es'
                  ? 'Voz a texto local, rápido e inteligente impulsado por IA en tu dispositivo.'
                  : 'Local, fast, intelligent voice-to-text powered by on-device AI.',
                inLanguage: locale,
                operatingSystem: 'Windows 10+',
                applicationCategory: 'UtilitiesApplication',
                offers: {
                  '@type': 'Offer',
                  price: '20.00',
                  priceCurrency: 'USD',
                },
              },
              {
                '@context': 'https://schema.org',
                '@type': 'Organization',
                name: 'dIKta.me',
                url: 'https://dikta.me',
                logo: 'https://dikta.me/images/app-icon.png',
              },
            ]),
          }}
        />

        <NextIntlClientProvider locale={locale} messages={messages}>
          {children}
        </NextIntlClientProvider>

        <Analytics />
        <SpeedInsights />
      </body>
    </html>
  );
}
