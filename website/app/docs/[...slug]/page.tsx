import { notFound } from 'next/navigation';
import fs from 'fs';
import path from 'path';
import { MarkdownRenderer } from '../../components/MarkdownRenderer';
import { DocumentationSidebar } from '../../components/DocumentationSidebar';
import { Navbar } from '../../components/Navbar';
import { Footer } from '../../components/Footer';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Documentation - dIKta.me',
  description: 'Learn how to configure and adapt dIKta.me to your workflow.',
};

export default async function DocViewPage({
  params,
}: {
  params: Promise<{ slug: string[] }>
}) {
  const resolvedParams = await params;
  const slugPath = resolvedParams.slug.join('/');
  
  // Try to find the Markdown file in website/content/docs
  const targetPath = path.join(process.cwd(), 'content', 'docs', `${slugPath}.md`);
  
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
