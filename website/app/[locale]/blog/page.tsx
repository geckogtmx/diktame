import Image from 'next/image';
import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { Link } from '@/i18n/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { createClient } from '@/lib/supabase/server';
import { BlogLanguagePills } from '@/app/components/BlogLanguagePills';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'BlogPage' });

  return {
    title: t('metaTitle'),
    description: t('metaDescription'),
    alternates: {
      canonical: `https://dikta.me${locale === 'en' ? '' : '/es'}/blog`,
      languages: {
        en: 'https://dikta.me/blog',
        es: 'https://dikta.me/es/blog',
      },
    },
  };
}

export default async function BlogPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('BlogPage');

  const supabase = await createClient();
  const { data: posts } = await supabase
    .from('blog_posts')
    .select(
      'id, slug, slug_es, title_en, title_es, hook_en, hook_es, image_url_en, image_url_es, published_at, voice_id',
    )
    .eq('status', 'published')
    .order('published_at', { ascending: false });

  const rows = posts ?? [];
  const featured = rows[0] ?? null;
  const rest = rows.slice(1);

  function resolvePost(post: (typeof rows)[number]) {
    return {
      title:
        (locale === 'es' ? post.title_es : post.title_en) ??
        post.title_en ??
        'Untitled',
      hook:
        (locale === 'es' ? post.hook_es : post.hook_en) ?? post.hook_en,
      imageUrl:
        (locale === 'es' ? post.image_url_es : post.image_url_en) ??
        post.image_url_en ??
        post.image_url_es,
      publishedAt: post.published_at
        ? new Date(post.published_at).toLocaleDateString(
            locale === 'es' ? 'es-ES' : 'en-US',
            { year: 'numeric', month: 'long', day: 'numeric' },
          )
        : null,
    };
  }

  return (
    <main id="main-content" className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          {/* Header */}
          <div className="max-w-5xl mx-auto mb-12">
            <h1 className="text-4xl md:text-5xl font-bold mb-4 tracking-tight">
              {t('title')}
            </h1>
            <p className="text-lg text-gray-400">
              {t('metaDescription')}
            </p>
          </div>

          {rows.length === 0 ? (
            <p className="text-gray-500 text-center py-20 text-lg">
              {t('noPosts')}
            </p>
          ) : (
            <div className="max-w-5xl mx-auto">
              {/* Featured Post — Hero */}
              {featured && (() => {
                const f = resolvePost(featured);
                return (
                  <article className="group mb-16">
                    <Link href={`/blog/${locale === 'es' && featured.slug_es ? featured.slug_es : featured.slug}`} className="block">
                      {f.imageUrl && (
                        <div className="relative aspect-[2/1] w-full overflow-hidden rounded-xl mb-6">
                          <Image
                            src={f.imageUrl}
                            alt={f.title}
                            fill
                            className="object-cover transition-transform duration-500 group-hover:scale-[1.02]"
                            sizes="(max-width: 768px) 100vw, 1024px"
                            priority
                          />
                        </div>
                      )}
                      <div className="space-y-3">
                        {f.publishedAt && (
                          <p className="text-sm text-gray-500 uppercase tracking-wider">
                            {f.publishedAt}
                          </p>
                        )}
                        <h2 className="text-2xl md:text-3xl font-bold tracking-tight group-hover:text-orange-400 transition-colors">
                          {f.title}
                        </h2>
                        {f.hook && (
                          <p className="text-lg text-gray-400 italic leading-relaxed">
                            {f.hook}
                          </p>
                        )}
                        <span className="inline-block text-orange-400 text-sm font-medium mt-2 group-hover:underline">
                          {t('readMore')} &rarr;
                        </span>
                      </div>
                    </Link>
                  </article>
                );
              })()}

              {/* Divider */}
              {rest.length > 0 && (
                <div className="border-t border-gray-800 mb-12" />
              )}

              {/* Content + Sidebar */}
              {rest.length > 0 && (
                <div className="flex flex-col lg:flex-row gap-12">
                  {/* Mobile sidebar — horizontal bar above grid */}
                  <div className="flex items-center justify-between lg:hidden mb-2">
                    <div className="flex items-center gap-4">
                      <a
                        href="https://www.linkedin.com/company/diktame/"
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label="LinkedIn"
                        className="text-gray-500 hover:text-white transition-colors"
                      >
                        <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 01-2.063-2.065 2.064 2.064 0 112.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/></svg>
                      </a>
                      <a
                        href="https://x.com/dIKtameapp"
                        target="_blank"
                        rel="noopener noreferrer"
                        aria-label="X (Twitter)"
                        className="text-gray-500 hover:text-white transition-colors"
                      >
                        <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"/></svg>
                      </a>
                    </div>
                    <BlogLanguagePills />
                  </div>

                  {/* Main grid */}
                  <div className="flex-1">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                      {rest.map((post) => {
                        const p = resolvePost(post);
                        return (
                          <article key={post.id} className="group">
                            <Link href={`/blog/${locale === 'es' && post.slug_es ? post.slug_es : post.slug}`} className="block">
                              {p.imageUrl && (
                                <div className="relative aspect-[3/2] w-full overflow-hidden rounded-lg mb-4">
                                  <Image
                                    src={p.imageUrl}
                                    alt={p.title}
                                    fill
                                    className="object-cover transition-transform duration-500 group-hover:scale-[1.03]"
                                    sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 400px"
                                  />
                                </div>
                              )}
                              <div className="space-y-2">
                                {p.publishedAt && (
                                  <p className="text-xs text-gray-500 uppercase tracking-wider">
                                    {p.publishedAt}
                                  </p>
                                )}
                                <h3 className="text-lg font-semibold tracking-tight group-hover:text-orange-400 transition-colors leading-snug">
                                  {p.title}
                                </h3>
                                <span className="inline-block text-orange-400 text-sm font-medium group-hover:underline">
                                  {t('readMore')} &rarr;
                                </span>
                              </div>
                            </Link>
                          </article>
                        );
                      })}
                    </div>
                  </div>

                  {/* Desktop sidebar */}
                  <aside className="hidden lg:block w-56 shrink-0">
                    <div className="sticky top-24 space-y-8">
                      {/* Social */}
                      <div>
                        <h4 className="text-xs text-gray-500 uppercase tracking-wider mb-3">
                          {t('followUs')}
                        </h4>
                        <div className="flex items-center gap-3">
                          <a
                            href="https://www.linkedin.com/company/diktame/"
                            target="_blank"
                            rel="noopener noreferrer"
                            aria-label="LinkedIn"
                            className="text-gray-400 hover:text-white transition-colors"
                          >
                            <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 01-2.063-2.065 2.064 2.064 0 112.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/></svg>
                          </a>
                          <a
                            href="https://x.com/dIKtameapp"
                            target="_blank"
                            rel="noopener noreferrer"
                            aria-label="X (Twitter)"
                            className="text-gray-400 hover:text-white transition-colors"
                          >
                            <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"/></svg>
                          </a>
                        </div>
                      </div>

                      {/* Language */}
                      <div>
                        <h4 className="text-xs text-gray-500 uppercase tracking-wider mb-3">
                          {locale === 'es' ? 'Idioma' : 'Language'}
                        </h4>
                        <BlogLanguagePills />
                      </div>
                    </div>
                  </aside>
                </div>
              )}
            </div>
          )}
        </Container>
      </div>

      <Footer />
    </main>
  );
}
