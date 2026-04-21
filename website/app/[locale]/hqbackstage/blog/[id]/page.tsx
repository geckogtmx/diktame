import { createAdminClient } from '@/lib/supabase/server';
import { BlogPostEditor } from './BlogPostEditor';

export default async function BlogPostEditPage({
  params,
}: {
  params: Promise<{ id: string; locale: string }>;
}) {
  const { id } = await params;
  const supabase = await createAdminClient();

  const { data: post } = await supabase.from('blog_posts').select('*').eq('id', id).single();

  if (!post) {
    return (
      <div className="flex items-center justify-center min-h-[50vh]">
        <p className="text-gray-500 text-lg">Post not found</p>
      </div>
    );
  }

  const [{ count: enCount }, { count: esCount }, { data: sendRows }] = await Promise.all([
    supabase
      .from('newsletter_subscribers')
      .select('*', { count: 'exact', head: true })
      .eq('locale', 'en')
      .eq('status', 'confirmed'),
    supabase
      .from('newsletter_subscribers')
      .select('*', { count: 'exact', head: true })
      .eq('locale', 'es')
      .eq('status', 'confirmed'),
    supabase
      .from('newsletter_sends')
      .select('locale, status, subscriber_count, started_at, completed_at')
      .eq('post_id', id),
  ]);

  type SendInfo = {
    status: string;
    subscriber_count: number | null;
    started_at: string | null;
    completed_at: string | null;
  };
  const sends: { en: SendInfo | null; es: SendInfo | null } = { en: null, es: null };
  for (const row of sendRows ?? []) {
    const key = row.locale === 'es' ? 'es' : 'en';
    sends[key] = {
      status: row.status,
      subscriber_count: row.subscriber_count,
      started_at: row.started_at,
      completed_at: row.completed_at,
    };
  }

  return (
    <BlogPostEditor
      post={post}
      subscriberCounts={{ en: enCount ?? 0, es: esCount ?? 0 }}
      sends={sends}
    />
  );
}
