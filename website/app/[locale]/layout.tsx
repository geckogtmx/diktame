import { NextIntlClientProvider } from 'next-intl';
import { getMessages, getTranslations, setRequestLocale } from 'next-intl/server';
import { Plus_Jakarta_Sans } from 'next/font/google';
import Script from 'next/script';
import { Analytics } from '@vercel/analytics/next';
import { SpeedInsights } from '@vercel/speed-insights/next';
import { locales } from '@/i18n/config';
import { GA_ID } from '@/lib/analytics';
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
        <Script src={`https://www.googletagmanager.com/gtag/js?id=${GA_ID}`} strategy="afterInteractive" />
        <Script id="gtag-init" strategy="afterInteractive">
          {`window.dataLayer = window.dataLayer || [];
function gtag(){dataLayer.push(arguments);}
gtag('js', new Date());
gtag('config', '${GA_ID}');`}
        </Script>
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
                url: 'https://dikta.me',
                description: locale === 'es'
                  ? 'Dictado por voz con IA local para Windows. Reconocimiento de voz con Whisper y LLMs locales, sin nube, sin suscripciones.'
                  : 'Open-source local AI voice dictation for Windows. Whisper-powered speech-to-text with local LLMs, no cloud, no subscriptions.',
                inLanguage: ['en', 'es'],
                operatingSystem: 'Windows 10+',
                applicationCategory: 'UtilitiesApplication',
                featureList: 'Voice dictation, Ask Mode, Refine Mode, Translate Mode, Quick Chat, Text-to-Speech, Voice Macros, Audio Ducking, Streaming Dictation',
                offers: {
                  '@type': 'AggregateOffer',
                  lowPrice: '0',
                  highPrice: '25',
                  priceCurrency: 'USD',
                  offerCount: 2,
                },
              },
              {
                '@context': 'https://schema.org',
                '@type': 'Organization',
                name: 'dIKta.me',
                url: 'https://dikta.me',
                logo: 'https://dikta.me/images/app-icon.png',
              },
              {
                '@context': 'https://schema.org',
                '@type': 'WebSite',
                name: 'dIKta.me',
                url: 'https://dikta.me',
                inLanguage: ['en', 'es'],
              },
              {
                '@context': 'https://schema.org',
                '@type': 'FAQPage',
                mainEntity: locale === 'es' ? [
                  {
                    '@type': 'Question',
                    name: '¿Cómo funciona dIKta.me sin internet?',
                    acceptedAnswer: { '@type': 'Answer', text: 'dIKta.me ejecuta Whisper V3 Turbo y LLMs locales (Gemma 3, Llama 3) directamente en tu GPU. Ningún audio ni texto sale de tu máquina. Es 100% local por defecto.' },
                  },
                  {
                    '@type': 'Question',
                    name: '¿Qué sistemas operativos soporta dIKta.me?',
                    acceptedAnswer: { '@type': 'Answer', text: 'dIKta.me está disponible para Windows 10+ (x64). macOS y Linux están en la hoja de ruta.' },
                  },
                  {
                    '@type': 'Question',
                    name: '¿Cuánto cuesta dIKta.me?',
                    acceptedAnswer: { '@type': 'Answer', text: 'Prueba gratuita con créditos cloud incluidos. Versión Completa: $20 (pago único) para dictado local ilimitado, todas las funciones y actualizaciones de por vida. Sin suscripción.' },
                  },
                  {
                    '@type': 'Question',
                    name: '¿Qué idiomas soporta el reconocimiento de voz?',
                    acceptedAnswer: { '@type': 'Answer', text: 'Whisper V3 Turbo soporta más de 90 idiomas con detección automática. La traducción bidireccional inglés-español está integrada.' },
                  },
                  {
                    '@type': 'Question',
                    name: '¿Necesito una GPU NVIDIA para usar dIKta.me?',
                    acceptedAnswer: { '@type': 'Answer', text: 'Una GPU NVIDIA se recomienda para STT y LLM locales con máxima velocidad. Sin embargo, también funciona en CPU (más lento) y ofrece un modo cloud con créditos wallet para usuarios sin GPU potente.' },
                  },
                ] : [
                  {
                    '@type': 'Question',
                    name: 'How does dIKta.me work offline?',
                    acceptedAnswer: { '@type': 'Answer', text: 'dIKta.me runs Whisper V3 Turbo and local LLMs (Gemma 3, Llama 3) directly on your GPU. No audio or text data ever leaves your machine. It is 100% air-gapped by default.' },
                  },
                  {
                    '@type': 'Question',
                    name: 'What operating systems does dIKta.me support?',
                    acceptedAnswer: { '@type': 'Answer', text: 'dIKta.me is available for Windows 10+ (x64). macOS and Linux support are on the roadmap.' },
                  },
                  {
                    '@type': 'Question',
                    name: 'How much does dIKta.me cost?',
                    acceptedAnswer: { '@type': 'Answer', text: 'Free trial with cloud credits included. Full Version: $20 one-time purchase for unlimited local dictation, all voice modes, and lifetime updates. No subscription required.' },
                  },
                  {
                    '@type': 'Question',
                    name: 'What languages does dIKta.me support for speech recognition?',
                    acceptedAnswer: { '@type': 'Answer', text: 'Whisper V3 Turbo supports 90+ languages with automatic language detection. Bidirectional English-Spanish translation is built-in.' },
                  },
                  {
                    '@type': 'Question',
                    name: 'Do I need an NVIDIA GPU to use dIKta.me?',
                    acceptedAnswer: { '@type': 'Answer', text: 'An NVIDIA GPU is recommended for the fastest local STT and LLM processing. However, dIKta.me also works on CPU (slower) and offers a cloud mode with wallet credits for users without a powerful GPU.' },
                  },
                ],
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
