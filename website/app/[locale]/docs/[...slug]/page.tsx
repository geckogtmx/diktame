import { notFound } from 'next/navigation';
import fs from 'fs';
import path from 'path';
import { MarkdownRenderer } from '@/app/components/MarkdownRenderer';
import { DocumentationSidebar } from '@/app/components/DocumentationSidebar';
import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import type { Metadata } from 'next';
import { setRequestLocale } from 'next-intl/server';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string; slug: string[] }>;
}): Promise<Metadata> {
  const resolvedParams = await params;
  const slugPath = resolvedParams.slug.join('/');
  const localePath = path.join(process.cwd(), 'content', resolvedParams.locale, 'docs', `${slugPath}.md`);
  const fallbackPath = path.join(process.cwd(), 'content', 'en', 'docs', `${slugPath}.md`);
  const targetPath = fs.existsSync(localePath) ? localePath : fallbackPath;

  let title = 'Documentation - dIKta.me';
  let description = 'Learn how to configure and adapt dIKta.me to your workflow.';

  try {
    if (fs.existsSync(targetPath)) {
      const content = fs.readFileSync(targetPath, 'utf-8');
      const h1Match = content.match(/^#\s+(.*)/m);
      if (h1Match) {
        title = `${h1Match[1]} - dIKta.me Docs`;
      }
      // Extract first non-heading paragraph as description
      const paraMatch = content.match(/^(?!#)([A-Z].*)/m);
      if (paraMatch) {
        description = paraMatch[1].slice(0, 160);
      }
    }
  } catch {
    // Fall back to defaults
  }

  return {
    title,
    description,
    alternates: {
      canonical: `https://dikta.me/${resolvedParams.locale}/docs/${slugPath}`,
      languages: {
        en: `https://dikta.me/en/docs/${slugPath}`,
        es: `https://dikta.me/es/docs/${slugPath}`,
        'x-default': `https://dikta.me/en/docs/${slugPath}`,
      },
    },
  };
}

export default async function DocViewPage({
  params,
}: {
  params: Promise<{ locale: string; slug: string[] }>
}) {
  const resolvedParams = await params;
  setRequestLocale(resolvedParams.locale);
  const slugPath = resolvedParams.slug.join('/');
  
  // Try locale-specific docs first, fall back to English
  const localePath = path.join(process.cwd(), 'content', resolvedParams.locale, 'docs', `${slugPath}.md`);
  const fallbackPath = path.join(process.cwd(), 'content', 'en', 'docs', `${slugPath}.md`);
  const targetPath = fs.existsSync(localePath) ? localePath : fallbackPath;
  
  let content = '';
  try {
    if (!fs.existsSync(targetPath)) {
      notFound();
    }
    content = fs.readFileSync(targetPath, 'utf-8');
  } catch (err) {
    notFound();
  }

  // To extract h1 as title if needed
  const titleMatch = content.match(/^#\s+(.*)/m);
  const pageTitle = titleMatch ? titleMatch[1] : 'Documentation';

  return (
    <main className="min-h-[100dvh] flex flex-col bg-black text-white selection:bg-blue-500/30">
      <Navbar />
      
      <div className="flex-1 max-w-7xl mx-auto w-full px-4 sm:px-6 lg:px-8 pt-32 pb-20 mt-4">
        
        {/* Breadcrumb / Mobile header space */}
        <div className="lg:hidden mb-8 border-b border-white/10 pb-4">
          <h1 className="text-2xl font-bold">{pageTitle}</h1>
        </div>

        <div className="flex flex-col lg:flex-row gap-12">
          {/* Left Sidebar */}
          <DocumentationSidebar />

          {/* Right Content */}
          <div className="flex-1 min-w-0 max-w-4xl bg-gray-900/40 p-6 md:p-10 rounded-2xl border border-white/5 shadow-2xl">
            <MarkdownRenderer content={content} />
          </div>
        </div>
      </div>

      <Footer />
    </main>
  );
}
