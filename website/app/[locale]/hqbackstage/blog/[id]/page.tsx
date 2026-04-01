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

  return <BlogPostEditor post={post} />;
}
