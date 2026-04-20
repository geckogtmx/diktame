import Image from 'next/image';
import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { Link } from '@/i18n/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { createClient } from '@/lib/supabase/server';
import { BlogLanguagePills } from '@/app/components/BlogLanguagePills';
import { NewsletterSignupBox } from '@/app/components/NewsletterSignupBox';
import {
  BlogArchiveNav,
  type ArchiveYear,
} from '@/app/components/BlogArchiveNav';
import { BlogPagination } from '@/app/components/BlogPagination';

const POSTS_PER_PAGE = 12;

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
      canonical: `https://dikta.me/${locale}/blog`,
      languages: {
        en: 'https://dikta.me/blog',
        es: 'https://dikta.me/es/blog',
      },
    },
  };
}

function parseMonth(monthParam: string | undefined): {
  key: string;
  startIso: string;
  endIso: string;
} | null {
  if (!monthParam) return null;
  // Expect YYYY-MM
  const match = /^(\d{4})-(\d{2})$/.exec(monthParam);
  if (!match) return null;
  const year = Number(match[1]);
  const month = Number(match[2]); // 1-12
  if (month < 1 || month > 12) return null;
  const start = new Date(Date.UTC(year, month - 1, 1));
  const end = new Date(Date.UTC(year, month, 1));
  return {
    key: `${match[1]}-${match[2]}`,
    startIso: start.toISOString(),
    endIso: end.toISOString(),
  };
}

function buildArchive(
  publishedAts: string[],
  locale: string,
): ArchiveYear[] {
  const byMonth = new Map<string, number>();
  for (const iso of publishedAts) {
    if (!iso) continue;
    const d = new Date(iso);
    const key = `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
    byMonth.set(key, (byMonth.get(key) ?? 0) + 1);
  }
  const formatter = new Intl.DateTimeFormat(locale === 'es' ? 'es-ES' : 'en-US', {
    month: 'long',
    year: 'numeric',
  });
  const byYear = new Map<
    string,
    { total: number; months: { key: string; label: string; count: number }[] }
  >();
  // Sort descending by month key
  const keys = Array.from(byMonth.keys()).sort().reverse();
  for (const key of keys) {
    const year = key.slice(0, 4);
    const monthNum = Number(key.slice(5, 7));
    const labelDate = new Date(Date.UTC(Number(year), monthNum - 1, 1));
    const label = formatter.format(labelDate);
    const count = byMonth.get(key) ?? 0;
    const bucket = byYear.get(year) ?? { total: 0, months: [] };
    bucket.total += count;
    bucket.months.push({ key, label, count });
    byYear.set(year, bucket);
  }
  return Array.from(byYear.entries())
    .sort((a, b) => (a[0] < b[0] ? 1 : -1))
    .map(([year, data]) => ({ year, ...data }));
}

export default async function BlogPage({
  params,
  searchParams,
}: {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ page?: string; month?: string }>;
}) {
  const { locale } = await params;
  const { page: pageParam, month: monthParam } = await searchParams;
  setRequestLocale(locale);
  const t = await getTranslations('BlogPage');

  const supabase = await createClient();

  // Parse + validate pagination
  const page = Math.max(1, Number.parseInt(pageParam ?? '1', 10) || 1);
  const monthFilter = parseMonth(monthParam);
  const from = (page - 1) * POSTS_PER_PAGE;
  const to = from + POSTS_PER_PAGE - 1;

  // Main posts query — respects month filter and paginates
  let postsQuery = supabase
    .from('blog_posts')
    .select(
      'id, slug, slug_es, title_en, title_es, hook_en, hook_es, image_url_en, image_url_es, published_at, voice_id',
      { count: 'exact' },
    )
    .eq('status', 'published')
    .order('published_at', { ascending: false });

  if (monthFilter) {
    postsQuery = postsQuery
      .gte('published_at', monthFilter.startIso)
      .lt('published_at', monthFilter.endIso);
  }

  const { data: posts, count } = await postsQuery.range(from, to);
  const rows = posts ?? [];
  const totalCount = count ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / POSTS_PER_PAGE));

  // Featured hero only on the first page of the unfiltered view
  const showFeatured = page === 1 && !monthFilter && rows.length > 0;
  const featured = showFeatured ? rows[0] : null;
  const grid = showFeatured ? rows.slice(1) : rows;

  // Archive data — always the full published set, separate lightweight query
  const { data: archiveRows } = await supabase
    .from('blog_posts')
    .select('published_at')
    .eq('status', 'published')
    .order('published_at', { ascending: false });
  const archiveYears = buildArchive(
    (archiveRows ?? []).map((r) => r.published_at as string).filter(Boolean),
    locale,
  );

  // Month label for breadcrumb
  const activeMonthLabel = monthFilter
    ? new Intl.DateTimeFormat(locale === 'es' ? 'es-ES' : 'en-US', {
        month: 'long',
        year: 'numeric',
      }).format(new Date(monthFilter.startIso))
    : null;

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
            {activeMonthLabel && (
              <p className="mt-4 text-sm text-gray-400">
                {t('filteredBy')}{' '}
                <span className="font-semibold text-white">{activeMonthLabel}</span>
                {' · '}
                <Link
                  href="/blog"
                  className="text-orange-400 hover:underline"
                >
                  {t('clearFilter')}
                </Link>
              </p>
            )}
          </div>

          {totalCount === 0 ? (
            <p className="text-gray-500 text-center py-20 text-lg">
              {t('noPosts')}
            </p>
          ) : (
            <div className="max-w-5xl mx-auto">
              {/* Featured Post — Hero (only on page 1, unfiltered) */}
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
              {featured && grid.length > 0 && (
                <div className="border-t border-gray-800 mb-12" />
              )}

              {/* Content + Sidebar */}
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
                      href="https://www.facebook.com/profile.php?id=61574298158591"
                      target="_blank"
                      rel="noopener noreferrer"
                      aria-label="Facebook"
                      className="text-gray-500 hover:text-white transition-colors"
                    >
                      <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
                    </a>
                    <a
                      href="https://ko-fi.com/geckogtmx"
                      target="_blank"
                      rel="noopener noreferrer"
                      aria-label="Ko-fi"
                      className="text-gray-500 hover:text-white transition-colors"
                    >
                      <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M23.881 8.948c-.773-4.085-4.859-4.593-4.859-4.593H.723c-.604 0-.679.798-.679.798s-.082 7.324-.022 11.822c.164 2.424 2.586 2.672 2.586 2.672s8.267-.023 11.966-.049c2.438-.426 2.683-2.566 2.658-3.734 4.352.24 7.422-2.831 6.649-6.916zm-11.062 3.511c-1.246 1.453-4.011 3.976-4.011 3.976s-.121.119-.31.023c-.076-.057-.108-.09-.108-.09-.443-.441-3.368-3.049-4.034-3.954-.709-.965-1.041-2.7-.091-3.71.951-1.01 3.005-1.086 4.363.407 0 0 1.565-1.782 3.468-.963 1.904.82 1.832 3.011.723 4.311zm6.173.478c-.928.116-1.682.028-1.682.028V7.284h1.77s1.971.551 1.971 2.638c0 1.913-.985 2.667-2.059 3.015z"/></svg>
                    </a>
                  </div>
                  <BlogLanguagePills />
                </div>

                {/* Main grid + pagination */}
                <div className="flex-1 min-w-0">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                    {grid.map((post) => {
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

                  <BlogPagination
                    locale={locale}
                    currentPage={page}
                    totalPages={totalPages}
                    month={monthFilter?.key ?? null}
                  />
                </div>

                {/* Desktop sidebar */}
                <aside className="hidden lg:block w-72 shrink-0">
                  <div className="sticky top-24 space-y-8">
                    {/* Newsletter signup */}
                    <NewsletterSignupBox />

                    {/* Archive */}
                    <BlogArchiveNav
                      years={archiveYears}
                      activeMonth={monthFilter?.key ?? null}
                    />

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
                          href="https://www.facebook.com/profile.php?id=61574298158591"
                          target="_blank"
                          rel="noopener noreferrer"
                          aria-label="Facebook"
                          className="text-gray-400 hover:text-white transition-colors"
                        >
                          <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>
                        </a>
                        <a
                          href="https://ko-fi.com/geckogtmx"
                          target="_blank"
                          rel="noopener noreferrer"
                          aria-label="Ko-fi"
                          className="text-gray-400 hover:text-white transition-colors"
                        >
                          <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24"><path d="M23.881 8.948c-.773-4.085-4.859-4.593-4.859-4.593H.723c-.604 0-.679.798-.679.798s-.082 7.324-.022 11.822c.164 2.424 2.586 2.672 2.586 2.672s8.267-.023 11.966-.049c2.438-.426 2.683-2.566 2.658-3.734 4.352.24 7.422-2.831 6.649-6.916zm-11.062 3.511c-1.246 1.453-4.011 3.976-4.011 3.976s-.121.119-.31.023c-.076-.057-.108-.09-.108-.09-.443-.441-3.368-3.049-4.034-3.954-.709-.965-1.041-2.7-.091-3.71.951-1.01 3.005-1.086 4.363.407 0 0 1.565-1.782 3.468-.963 1.904.82 1.832 3.011.723 4.311zm6.173.478c-.928.116-1.682.028-1.682.028V7.284h1.77s1.971.551 1.971 2.638c0 1.913-.985 2.667-2.059 3.015z"/></svg>
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
            </div>
          )}
        </Container>
      </div>

      <Footer />
    </main>
  );
}
