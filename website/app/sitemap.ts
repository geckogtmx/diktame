import type { MetadataRoute } from 'next';
import fs from 'fs';
import path from 'path';
import { createClient } from '@supabase/supabase-js';

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const baseUrl = 'https://dikta.me';

  // Static pages
  const staticPages = [
    '',
    '/features',
    '/pricing',
    '/about',
    '/waitlist',
    '/privacy',
    '/terms',
    '/roadmap',
    '/docs',
    '/blog',
  ];

  const staticEntries: MetadataRoute.Sitemap = staticPages.flatMap((page) => [
    {
      url: `${baseUrl}${page}`,
      lastModified: new Date(),
      changeFrequency: 'weekly' as const,
      priority: page === '' ? 1.0 : 0.8,
      alternates: {
        languages: {
          en: `${baseUrl}${page}`,
          es: `${baseUrl}/es${page}`,
        },
      },
    },
    {
      url: `${baseUrl}/es${page}`,
      lastModified: new Date(),
      changeFrequency: 'weekly' as const,
      priority: page === '' ? 1.0 : 0.8,
      alternates: {
        languages: {
          en: `${baseUrl}${page}`,
          es: `${baseUrl}/es${page}`,
        },
      },
    },
  ]);

  // Dynamic docs pages — scan content/docs for .md files
  const docsDir = path.join(process.cwd(), 'content', 'en', 'docs');
  const docEntries: MetadataRoute.Sitemap = [];

  function scanDocs(dir: string, prefix: string) {
    try {
      const items = fs.readdirSync(dir, { withFileTypes: true });
      for (const item of items) {
        if (item.isDirectory()) {
          scanDocs(path.join(dir, item.name), `${prefix}/${item.name}`);
        } else if (item.name.endsWith('.md')) {
          const slug = `${prefix}/${item.name.replace('.md', '')}`;
          docEntries.push(
            {
              url: `${baseUrl}/docs${slug}`,
              lastModified: new Date(),
              changeFrequency: 'monthly' as const,
              priority: 0.6,
              alternates: {
                languages: {
                  en: `${baseUrl}/docs${slug}`,
                  es: `${baseUrl}/es/docs${slug}`,
                },
              },
            },
            {
              url: `${baseUrl}/es/docs${slug}`,
              lastModified: new Date(),
              changeFrequency: 'monthly' as const,
              priority: 0.6,
              alternates: {
                languages: {
                  en: `${baseUrl}/docs${slug}`,
                  es: `${baseUrl}/es/docs${slug}`,
                },
              },
            },
          );
        }
      }
    } catch {
      // Directory doesn't exist yet
    }
  }

  scanDocs(docsDir, '');

  // Dynamic blog posts
  const blogEntries: MetadataRoute.Sitemap = [];
  try {
    const supabase = createClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
    );
    const { data: posts } = await supabase
      .from('blog_posts')
      .select('slug, published_at')
      .eq('status', 'published');

    for (const post of posts ?? []) {
      const slug = `/blog/${post.slug}`;
      blogEntries.push(
        {
          url: `${baseUrl}${slug}`,
          lastModified: post.published_at ? new Date(post.published_at) : new Date(),
          changeFrequency: 'weekly' as const,
          priority: 0.7,
          alternates: {
            languages: {
              en: `${baseUrl}${slug}`,
              es: `${baseUrl}/es${slug}`,
            },
          },
        },
        {
          url: `${baseUrl}/es${slug}`,
          lastModified: post.published_at ? new Date(post.published_at) : new Date(),
          changeFrequency: 'weekly' as const,
          priority: 0.7,
          alternates: {
            languages: {
              en: `${baseUrl}${slug}`,
              es: `${baseUrl}/es${slug}`,
            },
          },
        },
      );
    }
  } catch {
    // Supabase unavailable — skip blog entries
  }

  return [...staticEntries, ...docEntries, ...blogEntries];
}
