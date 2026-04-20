// Newsletter admin dashboard.
// Read-only at-a-glance view: subscriber counts per locale + status,
// growth in last 7/30 days, recent sends, and a 7-day event summary
// for deliverability sanity.
// Auth is already enforced by the parent hqbackstage/layout.tsx.

import { createAdminClient } from '@/lib/supabase/server';
import { Link } from '@/i18n/navigation';
import ResendButton from './ResendButton';

export const dynamic = 'force-dynamic';

type SubStatusRow = { locale: string; status: string; count: number };

async function fetchData() {
  const supabase = await createAdminClient();

  const [
    { data: subs },
    { data: sends },
    { data: events },
    { data: publishedPosts },
    { data: allSends },
  ] = await Promise.all([
    supabase.from('newsletter_subscribers').select('locale, status, confirmed_at, unsubscribed_at'),
    supabase
      .from('newsletter_sends')
      .select('id, locale, status, subscriber_count, started_at, completed_at, error, post_id, blog_posts(slug, title_en, title_es)')
      .order('started_at', { ascending: false, nullsFirst: false })
      .limit(10),
    supabase
      .from('newsletter_events')
      .select('event_type, created_at')
      .gte('created_at', new Date(Date.now() - 7 * 24 * 3600 * 1000).toISOString()),
    supabase
      .from('blog_posts')
      .select('id, slug, title_en, title_es, published_at')
      .eq('status', 'published')
      .order('published_at', { ascending: false, nullsFirst: false })
      .limit(50),
    supabase.from('newsletter_sends').select('post_id, locale, status'),
  ]);

  // Missing-sends matrix: for each published post, which locales have no row
  // OR have a row with a non-done status (failed/partial).
  type SendLookup = Map<string, Map<'en' | 'es', { status: string }>>;
  const sendsByPost: SendLookup = new Map();
  for (const s of allSends ?? []) {
    const pid = s.post_id as string;
    const loc = s.locale as 'en' | 'es';
    if (!sendsByPost.has(pid)) sendsByPost.set(pid, new Map());
    sendsByPost.get(pid)!.set(loc, { status: s.status as string });
  }

  type MissingRow = {
    post_id: string;
    slug: string;
    title: string;
    published_at: string | null;
    en: { state: 'missing' | 'failed' | 'partial' | 'done'; status?: string };
    es: { state: 'missing' | 'failed' | 'partial' | 'done'; status?: string };
  };
  const missingSends: MissingRow[] = [];
  for (const p of publishedPosts ?? []) {
    const pid = p.id as string;
    const m = sendsByPost.get(pid);
    const classify = (locale: 'en' | 'es'): MissingRow['en'] => {
      const hit = m?.get(locale);
      if (!hit) return { state: 'missing' };
      if (hit.status === 'failed') return { state: 'failed', status: hit.status };
      if (hit.status === 'partial') return { state: 'partial', status: hit.status };
      return { state: 'done', status: hit.status };
    };
    const en = classify('en');
    const es = classify('es');
    if (en.state === 'done' && es.state === 'done') continue;
    missingSends.push({
      post_id: pid,
      slug: (p.slug as string) ?? '',
      title: (p.title_en as string) || (p.title_es as string) || (p.slug as string) || pid,
      published_at: (p.published_at as string | null) ?? null,
      en,
      es,
    });
  }

  // Subscriber breakdown: group by locale + status
  const statusRows: SubStatusRow[] = [];
  const byLocale = new Map<string, Map<string, number>>();
  for (const row of subs ?? []) {
    const l = row.locale as string;
    const s = row.status as string;
    if (!byLocale.has(l)) byLocale.set(l, new Map());
    const m = byLocale.get(l)!;
    m.set(s, (m.get(s) ?? 0) + 1);
  }
  for (const [locale, m] of byLocale.entries()) {
    for (const [status, count] of m.entries()) {
      statusRows.push({ locale, status, count });
    }
  }

  // Growth: confirmed in last 7 / 30 days per locale
  const now = Date.now();
  const d7 = now - 7 * 24 * 3600 * 1000;
  const d30 = now - 30 * 24 * 3600 * 1000;
  const growth = new Map<string, { new7: number; new30: number; unsub30: number }>();
  for (const row of subs ?? []) {
    const l = row.locale as string;
    if (!growth.has(l)) growth.set(l, { new7: 0, new30: 0, unsub30: 0 });
    const g = growth.get(l)!;
    if (row.confirmed_at) {
      const t = new Date(row.confirmed_at as string).getTime();
      if (t >= d7) g.new7++;
      if (t >= d30) g.new30++;
    }
    if (row.unsubscribed_at) {
      const t = new Date(row.unsubscribed_at as string).getTime();
      if (t >= d30) g.unsub30++;
    }
  }

  // Event breakdown last 7 days
  const eventCounts = new Map<string, number>();
  for (const e of events ?? []) {
    const key = e.event_type as string;
    eventCounts.set(key, (eventCounts.get(key) ?? 0) + 1);
  }

  return { statusRows, growth, sends: sends ?? [], eventCounts, missingSends };
}

const STATUS_ORDER = ['confirmed', 'pending', 'unsubscribed', 'bounced', 'complained', 'deleted'];
const STATUS_LABEL: Record<string, string> = {
  confirmed: 'Confirmed',
  pending: 'Pending',
  unsubscribed: 'Unsubscribed',
  bounced: 'Bounced',
  complained: 'Complained',
  deleted: 'Deleted',
};
const STATUS_COLOR: Record<string, string> = {
  confirmed: 'text-green-400',
  pending: 'text-yellow-400',
  unsubscribed: 'text-gray-400',
  bounced: 'text-red-400',
  complained: 'text-red-500',
  deleted: 'text-gray-600',
};
const SEND_STATUS_COLOR: Record<string, string> = {
  done: 'bg-green-500/20 text-green-300',
  sending: 'bg-blue-500/20 text-blue-300',
  queued: 'bg-gray-500/20 text-gray-300',
  failed: 'bg-red-500/20 text-red-300',
  partial: 'bg-yellow-500/20 text-yellow-300',
};

export default async function NewsletterAdminPage() {
  const { statusRows, growth, sends, eventCounts, missingSends } = await fetchData();

  const locales = Array.from(new Set(statusRows.map((r) => r.locale))).sort();

  function countFor(locale: string, status: string): number {
    return statusRows.find((r) => r.locale === locale && r.status === status)?.count ?? 0;
  }

  const confirmedTotal = statusRows
    .filter((r) => r.status === 'confirmed')
    .reduce((sum, r) => sum + r.count, 0);

  return (
    <div className="max-w-6xl space-y-8">
      <header>
        <h1 className="text-3xl font-bold text-white">Newsletter</h1>
        <p className="text-sm text-gray-400 mt-1">
          Subscriber health, recent sends, and deliverability signals.
        </p>
      </header>

      {/* Headline */}
      <div className="rounded-xl border border-white/10 bg-white/5 p-6">
        <p className="text-sm text-gray-400">Confirmed subscribers</p>
        <p className="text-4xl font-extrabold text-white mt-1">{confirmedTotal}</p>
        <p className="text-xs text-gray-500 mt-2">
          Across {locales.length} {locales.length === 1 ? 'locale' : 'locales'}
        </p>
      </div>

      {/* Breakdown per locale */}
      <section>
        <h2 className="text-lg font-semibold text-white mb-3">By locale</h2>
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr className="text-left text-gray-400">
                <th className="px-4 py-2 font-medium">Locale</th>
                {STATUS_ORDER.map((s) => (
                  <th key={s} className="px-4 py-2 font-medium">
                    {STATUS_LABEL[s]}
                  </th>
                ))}
                <th className="px-4 py-2 font-medium">New 7d</th>
                <th className="px-4 py-2 font-medium">New 30d</th>
                <th className="px-4 py-2 font-medium">Unsub 30d</th>
              </tr>
            </thead>
            <tbody>
              {locales.map((l) => {
                const g = growth.get(l) ?? { new7: 0, new30: 0, unsub30: 0 };
                return (
                  <tr key={l} className="border-t border-white/10">
                    <td className="px-4 py-2 font-semibold text-white uppercase">{l}</td>
                    {STATUS_ORDER.map((s) => {
                      const n = countFor(l, s);
                      return (
                        <td key={s} className={`px-4 py-2 ${STATUS_COLOR[s] ?? 'text-gray-300'}`}>
                          {n}
                        </td>
                      );
                    })}
                    <td className="px-4 py-2 text-green-400">+{g.new7}</td>
                    <td className="px-4 py-2 text-green-400">+{g.new30}</td>
                    <td className="px-4 py-2 text-gray-400">{g.unsub30}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      {/* Missing / failed sends — operator can retrigger from here */}
      <section>
        <h2 className="text-lg font-semibold text-white mb-3">
          Published posts with missing or failed sends
        </h2>
        {missingSends.length === 0 ? (
          <p className="text-sm text-gray-500">
            All published posts have a successful send on both locales. Nothing to retrigger.
          </p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-white/10">
            <table className="w-full text-sm">
              <thead className="bg-white/5">
                <tr className="text-left text-gray-400">
                  <th className="px-4 py-2 font-medium">Published</th>
                  <th className="px-4 py-2 font-medium">Post</th>
                  <th className="px-4 py-2 font-medium">EN</th>
                  <th className="px-4 py-2 font-medium">ES</th>
                </tr>
              </thead>
              <tbody>
                {missingSends.map((row) => {
                  const when = row.published_at
                    ? new Date(row.published_at).toLocaleString('en-US', {
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit',
                      })
                    : '—';
                  const cell = (side: 'en' | 'es') => {
                    const d = row[side];
                    if (d.state === 'done') {
                      return <span className="text-green-400 text-xs">✓ sent</span>;
                    }
                    if (d.state === 'missing') {
                      return (
                        <ResendButton
                          postId={row.post_id}
                          locale={side}
                          label={`Send ${side.toUpperCase()}`}
                        />
                      );
                    }
                    // failed / partial — require force + confirm
                    return (
                      <ResendButton
                        postId={row.post_id}
                        locale={side}
                        force
                        label={`Retry ${side.toUpperCase()} (${d.state})`}
                        confirmText={`A previous ${side.toUpperCase()} send for this post is marked "${d.state}". Retrying will delete the old row and re-send to every confirmed ${side.toUpperCase()} subscriber — some may get a duplicate email. Proceed?`}
                      />
                    );
                  };
                  return (
                    <tr key={row.post_id} className="border-t border-white/10">
                      <td className="px-4 py-2 text-gray-400 whitespace-nowrap">{when}</td>
                      <td className="px-4 py-2 text-gray-200">
                        <Link
                          href={`/hqbackstage/blog/${row.post_id}`}
                          className="hover:text-blue-400 hover:underline"
                        >
                          {row.title}
                        </Link>
                      </td>
                      <td className="px-4 py-2">{cell('en')}</td>
                      <td className="px-4 py-2">{cell('es')}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Recent sends */}
      <section>
        <h2 className="text-lg font-semibold text-white mb-3">Recent sends</h2>
        {sends.length === 0 ? (
          <p className="text-sm text-gray-500">No sends yet.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-white/10">
            <table className="w-full text-sm">
              <thead className="bg-white/5">
                <tr className="text-left text-gray-400">
                  <th className="px-4 py-2 font-medium">When</th>
                  <th className="px-4 py-2 font-medium">Locale</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                  <th className="px-4 py-2 font-medium">Recipients</th>
                  <th className="px-4 py-2 font-medium">Post</th>
                </tr>
              </thead>
              <tbody>
                {sends.map((s) => {
                  const post = s.blog_posts as
                    | { slug?: string | null; title_en?: string | null; title_es?: string | null }
                    | null;
                  const title =
                    post?.title_en ??
                    post?.title_es ??
                    (post?.slug ?? 'unknown');
                  const when = s.started_at
                    ? new Date(s.started_at as string).toLocaleString('en-US', {
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric',
                        hour: '2-digit',
                        minute: '2-digit',
                      })
                    : '—';
                  return (
                    <tr key={s.id} className="border-t border-white/10">
                      <td className="px-4 py-2 text-gray-300">{when}</td>
                      <td className="px-4 py-2 uppercase text-gray-400">{s.locale}</td>
                      <td className="px-4 py-2">
                        <span
                          className={`inline-block px-2 py-0.5 rounded text-xs ${
                            SEND_STATUS_COLOR[s.status as string] ?? 'bg-gray-500/20 text-gray-300'
                          }`}
                        >
                          {s.status}
                        </span>
                      </td>
                      <td className="px-4 py-2 text-gray-300">{s.subscriber_count}</td>
                      <td className="px-4 py-2 text-gray-200">
                        {post?.slug ? (
                          <Link
                            href={`/hqbackstage/blog/${s.post_id}`}
                            className="hover:text-blue-400 hover:underline"
                          >
                            {title}
                          </Link>
                        ) : (
                          <span className="text-gray-500">{title}</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* Deliverability (last 7 days) */}
      <section>
        <h2 className="text-lg font-semibold text-white mb-3">Events · last 7 days</h2>
        <div className="flex flex-wrap gap-3">
          {(['sent', 'delivered', 'opened', 'clicked', 'bounced', 'complained', 'failed', 'unsubscribed'] as const).map(
            (type) => {
              const n = eventCounts.get(type) ?? 0;
              const bad = type === 'bounced' || type === 'complained' || type === 'failed';
              return (
                <div
                  key={type}
                  className={`min-w-[120px] rounded-lg border px-4 py-3 ${
                    bad && n > 0
                      ? 'border-red-900/50 bg-red-950/30'
                      : 'border-white/10 bg-white/5'
                  }`}
                >
                  <p className="text-xs text-gray-400 uppercase tracking-wider">{type}</p>
                  <p
                    className={`text-2xl font-extrabold mt-1 ${
                      bad && n > 0 ? 'text-red-300' : 'text-white'
                    }`}
                  >
                    {n}
                  </p>
                </div>
              );
            },
          )}
        </div>
        {eventCounts.size === 0 && (
          <p className="text-sm text-gray-500 mt-3">
            No webhook events in the last 7 days. If you sent a newsletter recently, check the Resend webhook configuration.
          </p>
        )}
      </section>
    </div>
  );
}
