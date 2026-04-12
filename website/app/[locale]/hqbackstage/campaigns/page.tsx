'use client';

import { useState, useEffect } from 'react';

interface CampaignRow {
  id: string;
  slug: string;
  title_en: string | null;
  meta_campaign_id: string | null;
  published_at: string | null;
  facebook_post_en: string | null;
  facebook_post_es: string | null;
  facebook_image_url_en: string | null;
  facebook_image_url_es: string | null;
  facebook_post_id: string | null;
}

export default function CampaignsPage() {
  const [posts, setPosts] = useState<CampaignRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchPosts();
  }, []);

  async function fetchPosts() {
    try {
      const res = await fetch('/api/hqbackstage/campaigns');
      if (!res.ok) throw new Error('Failed to fetch campaigns');
      const data = await res.json();
      setPosts(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }

  const postsWithCampaign = posts.filter((p) => p.meta_campaign_id);
  const postsReady = posts.filter((p) => !p.meta_campaign_id && (p.facebook_post_en || p.facebook_post_es));
  const postsNeedCopy = posts.filter((p) => !p.meta_campaign_id && !p.facebook_post_en && !p.facebook_post_es);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold">Campaigns</h1>
        <p className="text-sm text-gray-500 mt-1">
          Meta Ads campaigns for blog posts. Metrics come from Meta Ads Manager — run <code className="text-xs bg-white/5 px-1 py-0.5 rounded">/news-boost</code> in Claude Code to create campaigns.
        </p>
      </div>

      {loading && (
        <div className="text-gray-500 text-sm">Loading...</div>
      )}

      {error && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          {error}
        </div>
      )}

      {!loading && !error && (
        <>
          {/* Active Campaigns */}
          <section>
            <h2 className="text-sm font-medium text-gray-400 mb-3 uppercase tracking-wider">
              Active Campaigns ({postsWithCampaign.length})
            </h2>
            {postsWithCampaign.length === 0 ? (
              <div className="rounded-lg border border-white/10 bg-white/5 p-6 text-center text-gray-600 text-sm">
                No campaigns yet. Publish a post, generate Facebook copy, then run <code className="bg-white/5 px-1 py-0.5 rounded">/news-boost</code>.
              </div>
            ) : (
              <div className="rounded-lg border border-white/10 overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-white/10 bg-white/5">
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">Post</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">Campaign ID</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">FB Post</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">Published</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {postsWithCampaign.map((post) => (
                      <tr key={post.id} className="hover:bg-white/5 transition-colors">
                        <td className="px-4 py-3">
                          <a
                            href={`/en/hqbackstage/blog/${post.id}`}
                            className="text-blue-400 hover:text-blue-300"
                          >
                            {post.title_en || post.slug}
                          </a>
                        </td>
                        <td className="px-4 py-3 text-gray-400 font-mono text-xs">
                          {post.meta_campaign_id}
                        </td>
                        <td className="px-4 py-3">
                          {post.facebook_post_id ? (
                            <span className="text-green-400 text-xs">Linked</span>
                          ) : (
                            <span className="text-gray-600 text-xs">Link Click only</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-gray-500 text-xs">
                          {post.published_at
                            ? new Date(post.published_at).toLocaleDateString('en-US', {
                                month: 'short',
                                day: 'numeric',
                              })
                            : '—'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {postsWithCampaign.length > 0 && (
              <p className="text-xs text-gray-600 mt-2">
                For impressions, reach, clicks, and spend data — check{' '}
                <a
                  href="https://business.facebook.com/adsmanager"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-blue-400 hover:text-blue-300"
                >
                  Meta Ads Manager
                </a>
                {' '}or ask Claude Code to fetch insights via the Meta Ads MCP.
              </p>
            )}
          </section>

          {/* Ready to Boost */}
          {postsReady.length > 0 && (
            <section>
              <h2 className="text-sm font-medium text-gray-400 mb-3 uppercase tracking-wider">
                Ready to Boost ({postsReady.length})
              </h2>
              <div className="rounded-lg border border-white/10 overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-white/10 bg-white/5">
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">Post</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">FB Copy</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">FB Image</th>
                      <th className="text-left px-4 py-3 text-gray-500 font-medium">Published</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-white/5">
                    {postsReady.map((post) => (
                      <tr key={post.id} className="hover:bg-white/5 transition-colors">
                        <td className="px-4 py-3">
                          <a
                            href={`/en/hqbackstage/blog/${post.id}`}
                            className="text-blue-400 hover:text-blue-300"
                          >
                            {post.title_en || post.slug}
                          </a>
                        </td>
                        <td className="px-4 py-3">
                          <span className="text-green-400 text-xs">
                            {[post.facebook_post_en && 'EN', post.facebook_post_es && 'ES']
                              .filter(Boolean)
                              .join(' + ')}
                          </span>
                        </td>
                        <td className="px-4 py-3">
                          {post.facebook_image_url_en || post.facebook_image_url_es ? (
                            <span className="text-green-400 text-xs">
                              {[post.facebook_image_url_en && 'EN', post.facebook_image_url_es && 'ES']
                                .filter(Boolean)
                                .join(' + ')}
                            </span>
                          ) : (
                            <span className="text-yellow-500 text-xs">Not cropped</span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-gray-500 text-xs">
                          {post.published_at
                            ? new Date(post.published_at).toLocaleDateString('en-US', {
                                month: 'short',
                                day: 'numeric',
                              })
                            : 'Draft'}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="text-xs text-gray-600 mt-2">
                These posts have Facebook copy ready. Run <code className="bg-white/5 px-1 py-0.5 rounded">/news-boost</code> to create a campaign.
              </p>
            </section>
          )}

          {/* Need Copy */}
          {postsNeedCopy.length > 0 && (
            <section>
              <h2 className="text-sm font-medium text-gray-400 mb-3 uppercase tracking-wider">
                Need Facebook Copy ({postsNeedCopy.length})
              </h2>
              <div className="space-y-1">
                {postsNeedCopy.map((post) => (
                  <div
                    key={post.id}
                    className="flex items-center justify-between px-4 py-2 rounded-lg bg-white/5 text-sm"
                  >
                    <a
                      href={`/en/hqbackstage/blog/${post.id}`}
                      className="text-gray-400 hover:text-white"
                    >
                      {post.title_en || post.slug}
                    </a>
                    <span className="text-gray-600 text-xs">No FB copy</span>
                  </div>
                ))}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  );
}
