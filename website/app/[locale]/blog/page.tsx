import Image from 'next/image';
import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { Container } from '@/app/components/Container';
import { Link } from '@/i18n/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { createClient } from '@/lib/supabase/server';

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
      'id, slug, title_en, title_es, hook_en, hook_es, image_url_en, image_url_es, published_at, voice_id',
    )
    .eq('status', 'published')
    .order('published_at', { ascending: false });

  const rows = posts ?? [];

  return (
    <main id="main-content" className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-4xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-4 tracking-tight">
              {t('title')}
            </h1>
            <p className="text-lg text-gray-400 mb-16">
              {t('metaDescription')}
            </p>

            {rows.length === 0 ? (
              <p className="text-gray-500 text-center py-20 text-lg">
                {t('noPosts')}
              </p>
            ) : (
              <div className="space-y-16">
                {rows.map((post) => {
                  const title =
                    (locale === 'es' ? post.title_es : post.title_en) ??
                    post.title_en ??
                    'Untitled';
                  const hook =
                    (locale === 'es' ? post.hook_es : post.hook_en) ??
                    post.hook_en;
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

                  return (
                    <article key={post.id} className="group">
                      <Link href={`/blog/${post.slug}`} className="block">
                        {imageUrl && (
                          <div className="relative aspect-[2/1] w-full overflow-hidden rounded-xl mb-6">
                            <Image
                              src={imageUrl}
                              alt={title}
                              fill
                              className="object-cover transition-transform duration-500 group-hover:scale-[1.02]"
                              sizes="(max-width: 768px) 100vw, 896px"
                            />
                          </div>
                        )}
                        <div className="space-y-3">
                          {publishedAt && (
                            <p className="text-sm text-gray-500 uppercase tracking-wider">
                              {publishedAt}
                            </p>
                          )}
                          <h2 className="text-2xl md:text-3xl font-bold tracking-tight group-hover:text-orange-400 transition-colors">
                            {title}
                          </h2>
                          {hook && (
                            <p className="text-lg text-gray-400 italic leading-relaxed">
                              {hook}
                            </p>
                          )}
                          <span className="inline-block text-orange-400 text-sm font-medium mt-2 group-hover:underline">
                            {t('readMore')} &rarr;
                          </span>
                        </div>
                      </Link>
                    </article>
                  );
                })}
              </div>
            )}
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
