import Image from 'next/image';
import { notFound } from 'next/navigation';
import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { MarkdownRenderer } from '@/app/components/MarkdownRenderer';
import { Link } from '@/i18n/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { createClient } from '@/lib/supabase/server';

const SPANISH_VOICES = [
  'fuentes',
  'poniatowska',
  'garcia-marquez',
  'paz',
  'galeano',
  'bolano',
  'pacheco',
];

function isSpanishVoice(voiceId: string | null): boolean {
  if (!voiceId) return false;
  return SPANISH_VOICES.some((v) => voiceId.toLowerCase().includes(v));
}

function estimateReadingTime(text: string | null): number {
  if (!text) return 1;
  const wordCount = text.trim().split(/\s+/).length;
  return Math.max(1, Math.round(wordCount / 200));
}

async function getPost(slug: string) {
  const supabase = await createClient();
  const { data: post } = await supabase
    .from('blog_posts')
    .select('*')
    .eq('slug', slug)
    .eq('status', 'published')
    .single();
  return post;
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string; slug: string }>;
}) {
  const { locale, slug } = await params;
  const post = await getPost(slug);

  if (!post) {
    return { title: 'Not Found' };
  }

  const title =
    (locale === 'es' ? post.title_es : post.title_en) ?? post.title_en ?? '';
  const hook =
    (locale === 'es' ? post.hook_es : post.hook_en) ?? post.hook_en ?? '';
  const imageUrl =
    (locale === 'es' ? post.image_url_es : post.image_url_en) ??
    post.image_url_en ??
    post.image_url_es;

  return {
    title: `${title} — dIKta.me`,
    description: hook,
    openGraph: {
      title,
      description: hook,
      type: 'article',
      publishedTime: post.published_at,
      modifiedTime: post.updated_at,
      ...(imageUrl
        ? {
            images: [
              {
                url: imageUrl,
                width: 1200,
                height: 630,
                alt: title,
              },
            ],
          }
        : {}),
    },
    twitter: {
      card: 'summary_large_image',
      title,
      description: hook,
      ...(imageUrl ? { images: [imageUrl] } : {}),
    },
    alternates: {
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/blog/${slug}`,
      languages: {
        en: `https://dikta.me/blog/${slug}`,
        es: `https://dikta.me/es/blog/${slug}`,
      },
    },
  };
}

export default async function BlogPostPage({
  params,
}: {
  params: Promise<{ locale: string; slug: string }>;
}) {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('BlogPage');

  const post = await getPost(slug);
  if (!post) notFound();

  const title =
    (locale === 'es' ? post.title_es : post.title_en) ?? post.title_en ?? '';
  const hook =
    (locale === 'es' ? post.hook_es : post.hook_en) ?? post.hook_en ?? '';
  const body =
    (locale === 'es' ? post.body_es : post.body_en) ?? post.body_en ?? '';
  const closing =
    (locale === 'es' ? post.closing_es : post.closing_en) ??
    post.closing_en ??
    '';
  const imageUrl =
    (locale === 'es' ? post.image_url_es : post.image_url_en) ??
    post.image_url_en ??
    post.image_url_es;

  const publishedAt = post.published_at
    ? new Date(post.published_at).toLocaleDateString(
        locale === 'es' ? 'es-ES' : 'en-US',
        { year: 'numeric', month: 'long', day: 'numeric' },
      )
    : null;

  const readingTime = estimateReadingTime(body);
  const writtenInSpanish = isSpanishVoice(post.voice_id);
  const originalLanguage = writtenInSpanish ? t('spanish') : t('english');

  const jsonLd = {
    '@context': 'https://schema.org',
    '@type': 'BlogPosting',
    headline: title,
    description: hook,
    ...(imageUrl ? { image: imageUrl } : {}),
    datePublished: post.published_at,
    dateModified: post.updated_at ?? post.published_at,
    author: { '@type': 'Organization', name: 'dIKta.me' },
    publisher: { '@type': 'Organization', name: 'dIKta.me' },
  };

  return (
    <main
      id="main-content"
      className="min-h-screen bg-black text-white selection:bg-primary/30"
    >
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />

      <Navbar />

      <article className="relative pt-32 pb-20">
        {/* Hero image — full bleed */}
        {imageUrl && (
          <Container>
            <div className="max-w-5xl mx-auto mb-12">
              <div className="relative aspect-[2/1] w-full overflow-hidden rounded-2xl">
                <Image
                  src={imageUrl}
                  alt={title}
                  fill
                  priority
                  className="object-cover"
                  sizes="(max-width: 768px) 100vw, 1024px"
                />
              </div>
            </div>
          </Container>
        )}

        {/* Text content — narrower */}
        <Container>
          <div className="max-w-3xl mx-auto">
            {/* Header */}
            <header className="mb-12">
              <h1 className="text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight mb-6 leading-tight">
                {title}
              </h1>

              <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-gray-500 mb-8">
                {publishedAt && (
                  <span>
                    {t('publishedOn')} {publishedAt}
                  </span>
                )}
                <span className="hidden sm:inline">&middot;</span>
                <span>
                  {readingTime} {t('minRead')}
                </span>
              </div>

              {hook && (
                <p className="text-xl md:text-2xl text-gray-300 italic leading-relaxed">
                  {hook}
                </p>
              )}
            </header>

            <hr className="border-white/10 mb-12" />

            {/* Body */}
            <div className="mb-16">
              <MarkdownRenderer content={body} />
            </div>

            {/* Closing */}
            {closing && (
              <aside className="border-l-4 border-orange-500/60 pl-6 py-4 mb-16 bg-white/[0.02] rounded-r-lg">
                <div className="text-gray-300 italic leading-relaxed prose prose-invert max-w-none prose-p:my-2">
                  <MarkdownRenderer content={closing} />
                </div>
              </aside>
            )}

            {/* Footer meta */}
            <footer className="border-t border-white/10 pt-8 space-y-6">
              {/* Original language */}
              <p className="text-sm text-gray-500">
                {t('originallyIn')} {originalLanguage}.
              </p>

              {/* LinkedIn */}
              {post.linkedin_url && (
                <div>
                  <a
                    href={post.linkedin_url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-2 text-blue-400 hover:text-blue-300 transition-colors text-sm font-medium"
                  >
                    <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 0 1-2.063-2.065 2.064 2.064 0 1 1 2.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/></svg>
                    {locale === 'en' ? 'Also on LinkedIn' : 'También en LinkedIn'} &rarr;
                  </a>
                </div>
              )}

              {/* Language toggle */}
              <div>
                <Link
                  href={`/blog/${slug}`}
                  locale={locale === 'en' ? 'es' : 'en'}
                  className="inline-flex items-center gap-2 text-orange-400 hover:text-orange-300 transition-colors text-sm font-medium"
                >
                  {locale === 'en' ? t('readInEs') : t('readInEn')} &rarr;
                </Link>
              </div>

              {/* Back to blog */}
              <div>
                <Link
                  href="/blog"
                  className="inline-flex items-center gap-2 text-gray-400 hover:text-white transition-colors text-sm"
                >
                  &larr; {t('backToBlog')}
                </Link>
              </div>
            </footer>
          </div>
        </Container>
      </article>

      <Footer />
    </main>
  );
}
