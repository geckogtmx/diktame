import { createAdminClient } from '@/lib/supabase/server';
import { Link } from '@/i18n/navigation';

export default async function AdminBlogPage() {
  const supabase = await createAdminClient();

  const { data: posts } = await supabase
    .from('blog_posts')
    .select(
      'id, title_en, voice_label_en, status, run_date, image_url_en, image_url_es, created_at',
    )
    .order('created_at', { ascending: false });

  const rows = posts ?? [];

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-bold">Blog Posts</h1>

      <div className="overflow-x-auto rounded-lg border border-white/10">
        <table className="w-full text-sm">
          <thead className="bg-white/5">
            <tr>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Title (EN)</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Voice</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Status</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Run Date</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Images</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Created</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/5">
            {rows.map((post) => (
              <tr key={post.id} className="hover:bg-white/5">
                <td className="px-4 py-3">
                  <Link
                    href={`/hqbackstage/blog/${post.id}`}
                    className="text-blue-400 hover:text-blue-300 hover:underline"
                  >
                    {post.title_en || 'Untitled'}
                  </Link>
                </td>
                <td className="px-4 py-3 text-gray-400">{post.voice_label_en || '—'}</td>
                <td className="px-4 py-3">
                  <StatusBadge status={post.status} />
                </td>
                <td className="px-4 py-3 text-gray-400">
                  {post.run_date
                    ? new Date(post.run_date).toLocaleDateString('en-US', {
                        month: 'short',
                        day: 'numeric',
                        year: 'numeric',
                      })
                    : '—'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3 text-xs">
                    <span className="flex items-center gap-1">
                      EN{' '}
                      {post.image_url_en ? (
                        <span className="text-green-400">&#10003;</span>
                      ) : (
                        <span className="text-red-400">Missing</span>
                      )}
                    </span>
                    <span className="flex items-center gap-1">
                      ES{' '}
                      {post.image_url_es ? (
                        <span className="text-green-400">&#10003;</span>
                      ) : (
                        <span className="text-red-400">Missing</span>
                      )}
                    </span>
                  </div>
                </td>
                <td className="px-4 py-3 text-gray-400">
                  {new Date(post.created_at).toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                  })}
                </td>
              </tr>
            ))}
            {rows.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-gray-500">
                  No blog posts yet. Use /news-publish to create drafts.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    draft: 'bg-yellow-500/20 text-yellow-300',
    published: 'bg-green-500/20 text-green-300',
    archived: 'bg-gray-500/20 text-gray-300',
  };
  return (
    <span
      className={`inline-block px-2 py-0.5 rounded text-xs ${colors[status] ?? 'bg-gray-500/20 text-gray-300'}`}
    >
      {status}
    </span>
  );
}
