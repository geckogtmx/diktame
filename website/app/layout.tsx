import type { Metadata } from "next";
import { Plus_Jakarta_Sans } from "next/font/google";
import "./globals.css";

const plusJakarta = Plus_Jakarta_Sans({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "dIKta.me - Private AI Voice Dictation for Windows",
  description: "Local, fast, intelligent voice-to-text powered by on-device AI. No cloud, no subscriptions, no compromise on privacy.",
  keywords: ["voice dictation", "speech recognition", "AI", "local AI", "privacy", "Windows"],
  authors: [{ name: "dIKta.me Team" }],
  creator: "dIKta.me",
  publisher: "dIKta.me",
  robots: "index, follow",
  openGraph: {
    type: "website",
    locale: "en_US",
    url: "https://dikta.me",
    title: "dIKta.me - Private AI Voice Dictation",
    description: "Local, fast, intelligent voice-to-text powered by on-device AI.",
    siteName: "dIKta.me",
    images: [
      {
        url: "https://dikta.me/og-image.png",
        width: 1200,
        height: 630,
        alt: "dIKta.me - AI Voice Dictation",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "dIKta.me - Private AI Voice Dictation",
    description: "Local, fast, intelligent voice-to-text powered by on-device AI.",
    images: ["https://dikta.me/og-image.png"],
    creator: "@diktate_app",
  },

};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="dark">
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=5" />
        <meta name="theme-color" content="#020617" />
        <link rel="canonical" href="https://dikta.me" />
      </head>
      <body className={plusJakarta.className}>{children}</body>
    </html>
  );
}
