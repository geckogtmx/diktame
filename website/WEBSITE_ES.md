# Spanish Localization Guide — dikta.me Website

A step-by-step guide for adding full Spanish (`es`) support to the dIKta.me Next.js website. Written so any developer — including juniors — can follow it from start to finish.

> **Estimated effort**: ~3-5 days for infrastructure + string extraction (Phases 1-5), then translation time on top.

---

## Table of Contents

1. [What We're Starting With](#what-were-starting-with)
2. [Phase 1: Install the i18n Library](#phase-1-install-the-i18n-library)
3. [Phase 2: Restructure the App Directory](#phase-2-restructure-the-app-directory)
4. [Phase 3: Extract Strings from Components](#phase-3-extract-strings-from-components)
5. [Phase 4: Metadata & SEO](#phase-4-metadata--seo)
6. [Phase 5: Markdown Documentation](#phase-5-markdown-documentation)
7. [Phase 6: Language Switcher UI](#phase-6-language-switcher-ui)
8. [Phase 7: Translation](#phase-7-translation)
9. [Verification Checklist](#verification-checklist)
10. [Troubleshooting](#troubleshooting)

---

## What We're Starting With

Before you begin, understand what exists today:

| Aspect | Current state |
|--------|---------------|
| **i18n library** | None installed |
| **Text storage** | 100% hardcoded in JSX — no translation files |
| **Locale routing** | None — all URLs are English-only (`/about`, `/pricing`) |
| **`<html lang>`** | Hardcoded `"en"` in `app/layout.tsx:48` |
| **Metadata** | Hardcoded English titles/descriptions in each page |
| **Middleware** | `middleware.ts` — Supabase auth session refresh only |
| **Docs content** | ~30 markdown files in `content/docs/`, loaded by `app/docs/[...slug]/page.tsx` |
| **Components** | 26 files in `app/components/` — all with hardcoded English text |
| **Pages** | 12 routes (home, about, pricing, features, waitlist, privacy, terms, login, dashboard, dashboard/profile, docs, docs/[...slug]) |

**Goal**: After this guide, the website will support English (default, at `/`) and Spanish (at `/es/`). A user visiting `dikta.me/es` sees the full site in Spanish. A language toggle in the navbar lets them switch.

---

## Phase 1: Install the i18n Library

**What we're doing**: Installing `next-intl`, the most popular i18n library for Next.js App Router. It gives us:
- Locale-based URL routing (`/es/about`, `/es/pricing`)
- A `useTranslations()` hook for components
- Automatic locale detection from browser language
- Cookie-based locale persistence

### Step 1.1 — Install the package

Open a terminal in the `website/` folder and run:

```bash
npm install next-intl
```

**What to expect**: A new entry in `package.json` dependencies. No errors.

### Step 1.2 — Create the i18n configuration files

You'll create 3 small files. These tell `next-intl` which languages we support and how routing works.

**File 1** — Create `website/i18n/config.ts`:

```ts
// This is the single source of truth for supported languages.
// To add a new language later, just add it to this array.
export const locales = ['en', 'es'] as const;
export type Locale = (typeof locales)[number];
export const defaultLocale: Locale = 'en';
```

**File 2** — Create `website/i18n/routing.ts`:

```ts
import { defineRouting } from 'next-intl/routing';
import { locales, defaultLocale } from './config';

export const routing = defineRouting({
  locales,
  defaultLocale,
  // "as-needed" means:
  //   English (default) → dikta.me/about      (no /en prefix)
  //   Spanish           → dikta.me/es/about   (/es prefix)
  localePrefix: 'as-needed',
});
```

**File 3** — Create `website/i18n/request.ts`:

```ts
import { getRequestConfig } from 'next-intl/server';
import { routing } from './routing';

// This runs on every request. It figures out the locale and loads
// the right translation file (messages/en.json or messages/es.json).
export default getRequestConfig(async ({ requestLocale }) => {
  let locale = await requestLocale;

  // If the locale isn't valid (e.g. someone visits /fr/about), fall back to English
  if (!locale || !routing.locales.includes(locale as any)) {
    locale = routing.defaultLocale;
  }

  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default,
  };
});
```

**File 4** — Create `website/i18n/navigation.ts`:

```ts
import { createNavigation } from 'next-intl/navigation';
import { routing } from './routing';

// These are locale-aware replacements for next/link and next/navigation.
// When you use this Link component, href="/about" automatically becomes
// "/es/about" when the user is viewing the Spanish version.
export const { Link, redirect, usePathname, useRouter } = createNavigation(routing);
```

### Step 1.3 — Create empty translation files

Create the `messages/` folder and two JSON files. These will be filled in Phase 3.

**File** — Create `website/messages/en.json`:

```json
{
  "metadata": {
    "title": "dIKta.me - Private AI Voice Dictation for Windows",
    "description": "Local, fast, intelligent voice-to-text powered by on-device AI. No cloud, no subscriptions, no compromise on privacy."
  }
}
```

**File** — Create `website/messages/es.json`:

```json
{
  "metadata": {
    "title": "dIKta.me - Dictado por Voz con IA Privada para Windows",
    "description": "Voz a texto local, rápido e inteligente impulsado por IA en tu dispositivo. Sin nube, sin suscripciones, sin comprometer tu privacidad."
  }
}
```

### Step 1.4 — Update `next.config.ts`

**Why**: `next-intl` needs to hook into the Next.js build via a plugin wrapper.

**File**: `website/next.config.ts`

Replace the entire file with:

```ts
import type { NextConfig } from 'next';
import createNextIntlPlugin from 'next-intl/plugin';

const withNextIntl = createNextIntlPlugin('./i18n/request.ts');

const nextConfig: NextConfig = {};

export default withNextIntl(nextConfig);
```

### Step 1.5 — Update `middleware.ts`

**Why**: The middleware needs to detect the user's language from the URL (or browser headers) and route them to the right locale. We must also keep the existing Supabase auth logic.

**File**: `website/middleware.ts`

Replace the entire file with:

```ts
// Middleware for locale routing + authentication
// next-intl handles locale detection and URL rewriting
// Supabase handles auth session refresh

import createMiddleware from 'next-intl/middleware';
import { routing } from './i18n/routing';
import { createServerClient } from '@supabase/ssr';
import { type NextRequest } from 'next/server';

// Create the next-intl middleware (handles /es prefix, Accept-Language, etc.)
const intlMiddleware = createMiddleware(routing);

export async function middleware(request: NextRequest) {
  // Step 1: Let next-intl handle locale routing
  // This rewrites /es/about → internally routes to /[locale]/about with locale="es"
  const response = intlMiddleware(request);

  // Step 2: Supabase session refresh (keeps user logged in)
  // This is the same logic that was here before — just using the response from step 1
  const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

  if (!supabaseUrl || !supabaseAnonKey) {
    // Skip session refresh if env vars not configured (e.g. during build)
    return response;
  }

  const supabase = createServerClient(supabaseUrl, supabaseAnonKey, {
    cookies: {
      getAll() {
        return request.cookies.getAll();
      },
      setAll(cookiesToSet) {
        cookiesToSet.forEach(({ name, value, options }) =>
          response.cookies.set(name, value, options)
        );
      },
    },
  });

  // Refresh session if expired
  await supabase.auth.getUser();

  return response;
}

export const config = {
  matcher: [
    // Match all paths except static files and images
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)',
  ],
};
```

### Checkpoint

At this point, `npm run dev` should still work but you'll see errors because we haven't restructured the routes yet. That's next.

---

## Phase 2: Restructure the App Directory

**What we're doing**: Moving all pages inside a `[locale]` folder. This is how `next-intl` knows which language to use — the `[locale]` segment in the URL becomes a parameter that every page receives.

### Step 2.1 — Create the `[locale]` folder

```
mkdir app/[locale]
```

### Step 2.2 — Move all pages and sub-layouts

Move every page (and its folder) into `app/[locale]/`. Here's the exact list:

```
# Move these files/folders INTO app/[locale]/
app/page.tsx              → app/[locale]/page.tsx
app/about/                → app/[locale]/about/
app/pricing/              → app/[locale]/pricing/
app/features/             → app/[locale]/features/
app/waitlist/             → app/[locale]/waitlist/
app/privacy/              → app/[locale]/privacy/
app/terms/                → app/[locale]/terms/
app/login/                → app/[locale]/login/
app/dashboard/            → app/[locale]/dashboard/
app/docs/                 → app/[locale]/docs/
```

**Do NOT move**: `app/layout.tsx`, `app/globals.css`, `app/components/` — these stay where they are.

### Step 2.3 — Simplify the root layout

**Why**: The root `app/layout.tsx` currently renders `<html lang="en">` and all the metadata. After the move, the `[locale]` layout will handle that instead. The root layout becomes a pass-through.

**File**: `app/layout.tsx` — Replace the entire file with:

```tsx
// Root layout — minimal wrapper. All locale-specific logic is in [locale]/layout.tsx
export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return children;
}
```

### Step 2.4 — Create the locale layout

**Why**: This new layout receives the `locale` parameter and sets `<html lang>`, loads translations, and renders the page.

**File**: Create `app/[locale]/layout.tsx`:

```tsx
import { NextIntlClientProvider } from 'next-intl';
import { getMessages, getTranslations, setRequestLocale } from 'next-intl/server';
import { Plus_Jakarta_Sans } from 'next/font/google';
import { Analytics } from '@vercel/analytics/next';
import { SpeedInsights } from '@vercel/speed-insights/next';
import { locales } from '@/i18n/config';
import '../globals.css';

const plusJakarta = Plus_Jakarta_Sans({ subsets: ['latin'] });

// Tell Next.js to pre-render pages for both locales at build time
export function generateStaticParams() {
  return locales.map((locale) => ({ locale }));
}

// Dynamic metadata — title/description changes based on language
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

  // Required for static rendering — tells next-intl which locale this page uses
  setRequestLocale(locale);

  // Load all translations for this locale so client components can use them
  const messages = await getMessages();

  return (
    <html lang={locale} className="dark">
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=5" />
        <meta name="theme-color" content="#020617" />
        <link rel="canonical" href={locale === 'en' ? 'https://dikta.me' : 'https://dikta.me/es'} />
      </head>
      <body className={plusJakarta.className}>
        {/* JSON-LD structured data */}
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

        {/* NextIntlClientProvider makes translations available to all 'use client' components */}
        <NextIntlClientProvider locale={locale} messages={messages}>
          {children}
        </NextIntlClientProvider>

        <Analytics />
        <SpeedInsights />
      </body>
    </html>
  );
}
```

### Step 2.5 — Update every `page.tsx` to accept the locale param

Every page under `app/[locale]/` needs to call `setRequestLocale()` at the top. This is a `next-intl` requirement for static rendering.

**Pattern for server components** (most pages):

```tsx
import { setRequestLocale } from 'next-intl/server';

export default async function AboutPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  // ...rest of the component
}
```

**Pattern for pages that also have metadata**:

```tsx
import { getTranslations, setRequestLocale } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'aboutPage' });
  return {
    title: t('title'),
    description: t('description'),
  };
}

export default async function AboutPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  // ...rest of the component
}
```

### Step 2.6 — Fix import paths

After the move, some relative imports in moved pages may break (e.g. `../../components/Navbar` needs an extra `../`). Fix them, or use the `@/` alias:

```tsx
// Instead of fragile relative paths:
import { Navbar } from '../../components/Navbar';

// Use the @ alias (already configured in tsconfig.json):
import { Navbar } from '@/app/components/Navbar';
```

### Checkpoint

Run `npm run dev`. Visit `http://localhost:3000` — it should load the English home page. Visit `http://localhost:3000/es` — it should also load (same content for now, since translations aren't done yet). If you see 404s, double-check that all pages were moved into `app/[locale]/`.

---

## Phase 3: Extract Strings from Components

**What we're doing**: Right now every component has text like `<h1>STOP TYPING.</h1>` hardcoded. We need to move those strings into the JSON translation files and replace them with `t('key')` calls.

### Step 3.1 — Understand the two patterns

There are two different hooks depending on where you are:

| Component type | How to tell | Which hook |
|---------------|------------|------------|
| **Server component** | No `'use client'` at top | `getTranslations()` from `next-intl/server` |
| **Client component** | Has `'use client'` at top | `useTranslations()` from `next-intl` |

Most pages are server components. Most things in `app/components/` are client components (they have `'use client'`).

### Step 3.2 — Full worked example: Navbar

Here's a complete before/after for `app/components/Navbar.tsx` (a client component):

**Step A** — Add strings to `messages/en.json`:

```json
{
  "metadata": { "..." : "..." },
  "navbar": {
    "features": "Features",
    "vsOthers": "vs Others",
    "specs": "Specs",
    "pricing": "Pricing",
    "docs": "Docs",
    "documentation": "Documentation",
    "dashboard": "Dashboard",
    "signUp": "Sign Up",
    "toggleMenu": "Toggle menu"
  }
}
```

**Step B** — Add the same keys with Spanish values to `messages/es.json`:

```json
{
  "metadata": { "..." : "..." },
  "navbar": {
    "features": "Funciones",
    "vsOthers": "vs Otros",
    "specs": "Especificaciones",
    "pricing": "Precios",
    "docs": "Docs",
    "documentation": "Documentación",
    "dashboard": "Panel",
    "signUp": "Registrarse",
    "toggleMenu": "Abrir menú"
  }
}
```

**Step C** — Update the component:

```tsx
'use client';

import { useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';       // <-- ADD THIS
import { Link } from '@/i18n/navigation';            // <-- CHANGE: was 'next/link'
import Image from 'next/image';
import { createClient } from '@/lib/supabase/client';

export function Navbar() {
  const t = useTranslations('navbar');               // <-- ADD THIS
  const [isScrolled, setIsScrolled] = useState(false);
  // ... (rest of state/effects unchanged)

  return (
    <nav /* ... */>
      <div className="hidden md:flex items-center gap-12 text-sm font-medium">
        <Link href="/#core-track" className="...">
          {t('features')}                            {/* was: Features */}
        </Link>
        <Link href="/#versus-track" className="...">
          {t('vsOthers')}                            {/* was: vs Others */}
        </Link>
        {/* ... same pattern for all links */}
      </div>
      {/* ... */}
    </nav>
  );
}
```

**Key changes**:
1. `import Link from 'next/link'` → `import { Link } from '@/i18n/navigation'`
2. Add `const t = useTranslations('navbar');`
3. Replace every hardcoded string with `{t('keyName')}`

### Step 3.3 — Full worked example: Footer

Same pattern for `app/components/Footer.tsx`:

**Add to `messages/en.json`**:
```json
"footer": {
  "copyright": "© 2026 dIKta.me. All rights reserved.",
  "coAuthored": "Co-authored by Human & AI (Gemini Studio, Antigravity, Claude Code)",
  "about": "About",
  "privacy": "Privacy",
  "terms": "Terms"
}
```

**Update component**: Same pattern — `useTranslations('footer')` + `{ Link } from '@/i18n/navigation'`.

### Step 3.4 — Full worked example: Server component (docs page)

For `app/[locale]/docs/page.tsx` (a server component — no `'use client'`):

```tsx
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { Link } from '@/i18n/navigation';

export default async function DocsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('docsPage');    // <-- NOTE: "await" because server

  return (
    <h1>{t('title')}</h1>  // <-- Same t('key') pattern
  );
}
```

**The only differences from client components**:
- Use `getTranslations()` (with `await`) instead of `useTranslations()`
- Import from `next-intl/server` instead of `next-intl`
- Must call `setRequestLocale(locale)` first

### Step 3.5 — Complete component inventory

Work through these files one at a time. For each file:
1. Read it and list every visible English string
2. Add those strings to `messages/en.json` under a namespace matching the component
3. Copy the keys to `messages/es.json` (leave values in English for now — translation is Phase 7)
4. Update the component to use `t('key')` calls
5. Replace `import Link from 'next/link'` with `import { Link } from '@/i18n/navigation'`

| File | Namespace | ~Strings | Notes |
|------|-----------|----------|-------|
| `components/Navbar.tsx` | `navbar` | 10 | Nav links, CTAs |
| `components/Footer.tsx` | `footer` | 6 | Copyright, links |
| `components/HeroSection.tsx` | `hero` | 15 | Badge, titles, carousel words, CTA, platforms |
| `components/CoreArsenalSection.tsx` | `coreArsenal` | 20 | 4 feature cards |
| `components/VersusSection.tsx` | `versus` | 40 | Comparison table rows |
| `components/SpecsSection.tsx` | `specs` | 60 | 12+ spec cards in 3 groups |
| `components/PricingSection.tsx` | `pricing` | 25 | 2 tiers + bullet features |
| `components/FeaturesModal.tsx` | `featuresModal` | 90 | 45+ features |
| `components/BilingualSection.tsx` | `bilingual` | 15 | Demo content |
| `components/AskModeSection.tsx` | `askMode` | 15 | Demo content |
| `components/QuickChatSection.tsx` | `quickChat` | 15 | Demo content |
| `components/TokensSection.tsx` | `tokens` | 15 | Demo content |
| `components/VoiceMacrosSection.tsx` | `voiceMacros` | 15 | Demo content |
| `components/TTSSection.tsx` | `tts` | 15 | Demo content |
| `components/WaitingListForm.tsx` | `waitingList` | 15 | Form labels, validation |
| `components/DocumentationSidebar.tsx` | `docsSidebar` | 35 | Section titles + link labels |
| `[locale]/page.tsx` | `homePage` | 5 | Wrapper text |
| `[locale]/about/page.tsx` | `aboutPage` | 40 | Full prose |
| `[locale]/pricing/page.tsx` | `pricingPage` | 5 | Metadata |
| `[locale]/features/page.tsx` | `featuresPage` | 5 | Metadata |
| `[locale]/waitlist/page.tsx` | `waitlistPage` | 20 | Hero + benefits |
| `[locale]/privacy/page.tsx` | `privacyPage` | 80 | Legal text |
| `[locale]/terms/page.tsx` | `termsPage` | 80 | Legal text |
| `[locale]/login/page.tsx` | `loginPage` | 10 | Form labels |
| `[locale]/dashboard/page.tsx` | `dashboard` | 20 | Labels |
| `[locale]/dashboard/profile/page.tsx` | `profile` | 15 | Form labels |
| `[locale]/docs/page.tsx` | `docsPage` | 40 | Sections, Quick Start |
| `[locale]/docs/[...slug]/page.tsx` | `docView` | 5 | Fallback text |
| **Total** | | **~750** | |

### Checkpoint

After extracting all strings, run `npm run dev` and navigate to every page. Everything should look exactly the same as before (since `en.json` has the same strings). If something shows a translation key like `navbar.features` instead of "Features", you have a typo in the JSON key or namespace.

---

## Phase 4: Metadata & SEO

**What we're doing**: Making sure search engines understand we have two languages, and serve the right one.

### Step 4.1 — Convert static metadata to dynamic

Every page that currently has `export const metadata = { ... }` must change to `export async function generateMetadata()`. This was already shown in Phase 2, Step 2.5. Make sure every page under `app/[locale]/` uses this pattern.

**Pages that currently have static metadata** (search for `export const metadata` in each file):
- `app/[locale]/docs/page.tsx`
- `app/[locale]/about/page.tsx` (if it has metadata)
- `app/[locale]/waitlist/page.tsx` (check its layout too)
- Any other page with `export const metadata`

### Step 4.2 — Add `hreflang` to every page

`hreflang` tells Google that `/about` and `/es/about` are the same page in different languages. Add this to every page's `generateMetadata()`:

```ts
alternates: {
  languages: {
    en: `https://dikta.me${pagePath}`,
    es: `https://dikta.me/es${pagePath}`,
  },
},
```

Where `pagePath` is the path for that specific page (e.g. `/about`, `/pricing`, etc.). The root locale layout already handles this for the homepage.

### Step 4.3 — Create a sitemap

**Why**: Tells search engines about all pages in both languages.

**File**: Create `app/sitemap.ts`:

```ts
import type { MetadataRoute } from 'next';

const baseUrl = 'https://dikta.me';

const routes = [
  '/',
  '/about',
  '/pricing',
  '/features',
  '/docs',
  '/waitlist',
  '/privacy',
  '/terms',
];

export default function sitemap(): MetadataRoute.Sitemap {
  return routes.flatMap((route) => {
    const enUrl = `${baseUrl}${route === '/' ? '' : route}`;
    const esUrl = `${baseUrl}/es${route === '/' ? '' : route}`;

    return [
      {
        url: enUrl,
        lastModified: new Date(),
        alternates: {
          languages: { en: enUrl, es: esUrl },
        },
      },
      {
        url: esUrl,
        lastModified: new Date(),
        alternates: {
          languages: { en: enUrl, es: esUrl },
        },
      },
    ];
  });
}
```

---

## Phase 5: Markdown Documentation

**What we're doing**: The docs pages (`/docs/getting-started`, `/docs/features/dictation`, etc.) load markdown files from `content/docs/`. We need to add Spanish versions and make the code pick the right one based on locale.

### Step 5.1 — Reorganize the content folder

**Currently**:
```
content/
  docs/
    getting-started.md
    features/
      dictation.md
      ask.md
      ...
    settings/
      general.md
      ...
    dev/
      setup.md
      ...
```

**Move to**:
```
content/
  en/
    docs/
      getting-started.md
      features/
        dictation.md
        ...
      settings/
        general.md
        ...
      dev/
        setup.md
        ...
  es/
    docs/
      (Spanish translations go here — same file structure)
```

**How to do the move**:
1. Create `content/en/` folder
2. Move the entire `content/docs/` folder into `content/en/` → becomes `content/en/docs/`
3. Create `content/es/docs/` (empty for now — you'll add translated files in Phase 7)

### Step 5.2 — Update the doc viewer page

**File**: `app/[locale]/docs/[...slug]/page.tsx`

The file currently reads from `content/docs/`. Change it to read from `content/{locale}/docs/`, with a fallback to English if the Spanish version doesn't exist yet.

**Find this line** (currently around line 51):
```ts
const targetPath = path.join(process.cwd(), 'content', 'docs', `${slugPath}.md`);
```

**Replace with**:
```ts
const { locale } = await params;

// Try the locale-specific doc first
let targetPath = path.join(process.cwd(), 'content', locale, 'docs', `${slugPath}.md`);

// Fall back to English if the translated version doesn't exist
if (!fs.existsSync(targetPath)) {
  targetPath = path.join(process.cwd(), 'content', 'en', 'docs', `${slugPath}.md`);
}
```

Do the same for the `generateMetadata` function in the same file (it also reads from `content/docs/`).

**Why the fallback matters**: This means you can launch with only a few docs translated. Untranslated pages will show the English version instead of a 404 error.

### Step 5.3 — Translate sidebar labels

The `DocumentationSidebar.tsx` component has a hardcoded `navigation` array with section titles like "Overview", "Core Features", etc. These need to go through the translation system too.

This was covered in Phase 3 — use namespace `docsSidebar` in the JSON.

### Step 5.4 — Priority docs for translation

You don't need to translate everything at launch. Here are the priorities:

**P0 — Must have for launch:**
- `getting-started.md` — first thing users read
- `troubleshooting.md` — support deflection
- `features/dictation.md` — the core feature

**P1 — Should have:**
- `features/refine.md`, `features/ask.md`, `features/translate.md`, `features/quick-chat.md`
- `settings/general.md`, `settings/ai-engine.md`, `settings/hotkeys.md`

**P2 — Nice to have (can launch without):**
- All other settings docs
- `features/note.md`, `features/oops.md`, `features/tts.md`, `features/snippets.md`

**Skip entirely:**
- Everything in `dev/` — developer docs stay English-only

---

## Phase 6: Language Switcher UI

**What we're doing**: Adding a button to the navbar that lets users switch between English and Spanish.

### Step 6.1 — Create a LanguageSwitcher component

**File**: Create `app/components/LanguageSwitcher.tsx`:

```tsx
'use client';

import { useLocale } from 'next-intl';
import { usePathname, useRouter } from '@/i18n/navigation';

export function LanguageSwitcher() {
  const locale = useLocale();       // "en" or "es"
  const router = useRouter();
  const pathname = usePathname();    // e.g. "/about" (without locale prefix)

  // Toggle to the other language
  const switchTo = locale === 'en' ? 'es' : 'en';
  const label = locale === 'en' ? 'ES' : 'EN';

  function handleSwitch() {
    // router.replace navigates to the same page in the other language
    // e.g. if you're on /es/about and click, it goes to /about
    router.replace(pathname, { locale: switchTo });
  }

  return (
    <button
      onClick={handleSwitch}
      className="text-muted hover:text-white transition-colors text-sm font-medium px-2 py-1 rounded border border-white/10 hover:border-white/20"
      aria-label={`Switch to ${switchTo === 'es' ? 'Spanish' : 'English'}`}
    >
      {label}
    </button>
  );
}
```

### Step 6.2 — Add to the Navbar

In `Navbar.tsx`, import and place the switcher:

```tsx
import { LanguageSwitcher } from './LanguageSwitcher';

// Inside the desktop nav (between the last link and the CTA button):
<LanguageSwitcher />

// Also inside the mobile menu:
<LanguageSwitcher />
```

### Step 6.3 — Locale persistence (automatic)

`next-intl` middleware automatically sets a `NEXT_LOCALE` cookie when the user visits a localized URL. This means:
- User visits `/es/about` → cookie set to `es`
- User visits `/pricing` later → middleware sees cookie, redirects to `/es/pricing`

**No extra code needed.** This works out of the box.

---

## Phase 7: Translation

**What we're doing**: The actual translation of all text.

### Step 7.1 — Complete `messages/en.json`

By now, you've already been adding strings to `en.json` during Phase 3. Do a final pass to make sure every hardcoded string from every component is in the JSON.

### Step 7.2 — Translate `messages/es.json`

1. Copy `en.json` to `es.json` (if not already done)
2. Translate every value. **Keys stay in English.**

**Translation guidelines**:
- **"dIKta.me"** — never translate the brand name
- **Feature names** that appear in the app UI (Refine, Quick Chat, Oops, Ask, Note) — keep in English but optionally add a Spanish explanation in parentheses, e.g. `"Refine (Edición refinada)"`
- **Marketing copy** — match the punchy, direct tone. Don't use machine translation for taglines.
- **Legal pages** (Privacy, Terms) — these must be reviewed by someone who understands legal Spanish, not just translated literally
- **Dynamic values** — `next-intl` supports `{variable}` placeholders: `"greeting": "Hola {name}"` → in code: `t('greeting', { name: 'Ana' })`

### Step 7.3 — Translate markdown docs

1. For each P0/P1 doc listed in Phase 5.4:
   - Copy the English file from `content/en/docs/...` to `content/es/docs/...` (same path)
   - Translate the prose
   - **Keep code blocks and terminal commands in English** (code is language-neutral)
   - Keep image paths unchanged (images work for both languages)
   - Screenshots showing the English app UI are fine for launch

---

## Verification Checklist

After everything is implemented, go through this list:

### Build & Deploy
- [ ] `npm run build` succeeds with zero errors
- [ ] `npm run dev` works locally

### English (Default)
- [ ] `https://dikta.me` loads in English
- [ ] View source → `<html lang="en">`
- [ ] Page title in browser tab is in English
- [ ] All pages work: `/about`, `/pricing`, `/features`, `/docs`, `/waitlist`, `/privacy`, `/terms`

### Spanish
- [ ] `https://dikta.me/es` loads in Spanish
- [ ] View source → `<html lang="es">`
- [ ] Page title in browser tab is in Spanish
- [ ] All pages work: `/es/about`, `/es/pricing`, `/es/features`, `/es/docs`, etc.
- [ ] No English strings visible on any `/es` page (spot check all sections)

### Language Switcher
- [ ] Clicking EN/ES in navbar switches language
- [ ] Switcher preserves the current page (e.g. `/es/about` → `/about`)
- [ ] Switcher works on mobile menu
- [ ] After switching, subsequent navigation stays in the chosen language

### SEO
- [ ] View source on `/es` → `<link rel="alternate" hreflang="en" ...>` present
- [ ] View source on `/` → `<link rel="alternate" hreflang="es" ...>` present
- [ ] OpenGraph `locale` is `es_ES` on `/es` pages
- [ ] JSON-LD `description` is in Spanish on `/es`
- [ ] `/sitemap.xml` includes both EN and ES URLs

### Documentation
- [ ] `/es/docs/getting-started` loads the Spanish version
- [ ] `/es/docs/dev/setup` falls back to English (dev docs not translated)
- [ ] Sidebar labels are in Spanish on `/es/docs/*`

### Auth
- [ ] Login works on `/es/login`
- [ ] Dashboard loads on `/es/dashboard`
- [ ] Supabase session refresh still works (user stays logged in)

### Cookie
- [ ] After visiting `/es`, the `NEXT_LOCALE` cookie is set to `es`
- [ ] Opening a new tab to `dikta.me` redirects to `/es` (because of cookie)

---

## Troubleshooting

### "Missing message: ..." errors in console
You forgot to add a key to the JSON file, or there's a typo. Check that the namespace in `useTranslations('xxx')` matches the top-level key in the JSON, and that the specific key exists.

### Page shows translation keys instead of text
Same as above — the key exists in code but not in the JSON file.

### 404 on `/es/about`
The page wasn't moved into `app/[locale]/about/page.tsx`. Double check the file exists at that exact path.

### `npm run build` fails with "Missing messages"
Make sure `messages/es.json` has ALL the same keys as `messages/en.json`. Even if you haven't translated them yet, the keys must exist (you can temporarily use the English values).

### Supabase auth broken after restructure
Check that the middleware still runs `supabase.auth.getUser()`. The locale middleware must run first, then auth. See Phase 1, Step 1.5.

### Links go to wrong locale
You're still using `import Link from 'next/link'` somewhere. Search the codebase for `from 'next/link'` and replace with `from '@/i18n/navigation'` in every component.

### Images/styles broken after moving pages
Check import paths. After moving pages into `[locale]/`, relative paths like `../../components/Navbar` need an extra `../`. Using `@/app/components/Navbar` is safer.
