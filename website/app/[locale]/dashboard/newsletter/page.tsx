// Dashboard newsletter preferences page.
// Logged-in users manage their EN / ES subscriptions here. Their email is
// already verified by Supabase auth, so we skip double opt-in and flip
// status directly. Matches rows by email (covers both account-signup and
// blog-sidebar-originated subscriptions).

import { getTranslations, setRequestLocale } from 'next-intl/server';
import { redirect } from 'next/navigation';
import { revalidatePath } from 'next/cache';
import { createAdminClient, createClient } from '@/lib/supabase/server';

export const dynamic = 'force-dynamic';

const LOCALES = ['en', 'es'] as const;
type Locale = (typeof LOCALES)[number];

type Row = {
  locale: Locale;
  status: string | null; // null = no row exists
};

async function loadState(email: string): Promise<Row[]> {
  const admin = await createAdminClient();
  const { data } = await admin
    .from('newsletter_subscribers')
    .select('locale, status')
    .eq('email', email)
    .in('locale', ['en', 'es']);

  const byLocale = new Map<string, string>();
  for (const row of data ?? []) {
    byLocale.set(row.locale as string, row.status as string);
  }
  return LOCALES.map((l) => ({ locale: l, status: byLocale.get(l) ?? null }));
}

async function toggleSubscription(formData: FormData) {
  'use server';
  const locale = formData.get('locale')?.toString() as Locale | undefined;
  const action = formData.get('action')?.toString();
  const pageLocale = formData.get('pageLocale')?.toString() ?? 'en';
  if (!locale || !LOCALES.includes(locale) || !action) return;

  const supabase = await createClient();
  const {
    data: { user },
  } = await supabase.auth.getUser();
  if (!user || !user.email) return;

  const admin = await createAdminClient();
  const email = user.email.trim().toLowerCase();
  const now = new Date().toISOString();

  const { data: existing } = await admin
    .from('newsletter_subscribers')
    .select('id, status, unsubscribe_token')
    .eq('email', email)
    .eq('locale', locale)
    .maybeSingle();

  if (action === 'subscribe') {
    if (!existing) {
      await admin.from('newsletter_subscribers').insert({
        email,
        locale,
        status: 'confirmed',
        user_id: user.id,
        source: 'account-signup',
        consent_text_version: 'v1-2026-04-17',
        unsubscribe_token: crypto.randomUUID(),
        confirmed_at: now,
      });
    } else {
      await admin
        .from('newsletter_subscribers')
        .update({
          status: 'confirmed',
          user_id: user.id,
          confirmed_at: now,
          unsubscribed_at: null,
          unsubscribe_token: existing.unsubscribe_token ?? crypto.randomUUID(),
        })
        .eq('id', existing.id);
    }
  } else if (action === 'unsubscribe' && existing) {
    await admin
      .from('newsletter_subscribers')
      .update({ status: 'unsubscribed', unsubscribed_at: now })
      .eq('id', existing.id);
  }

  revalidatePath(`/${pageLocale}/dashboard/newsletter`);
}

export default async function DashboardNewsletterPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('Newsletter');

  const supabase = await createClient();
  const {
    data: { user },
  } = await supabase.auth.getUser();
  if (!user || !user.email) redirect(`/${locale}/login`);

  const rows = await loadState(user.email.toLowerCase());

  const copy: Record<Locale, { label: string; desc: string }> = {
    en: { label: t('englishLabel'), desc: t('englishDesc') },
    es: { label: t('spanishLabel'), desc: t('spanishDesc') },
  };

  function statusLabel(status: string | null): string {
    if (status === 'confirmed') return t('dashboardSubscribed');
    if (status === 'pending') return t('dashboardPending');
    return t('dashboardUnsubscribed');
  }

  function statusTone(status: string | null): string {
    if (status === 'confirmed') return 'text-green-400';
    if (status === 'pending') return 'text-yellow-400';
    return 'text-gray-400';
  }

  return (
    <div className="max-w-3xl space-y-6">
      <header>
        <h1 className="text-3xl font-bold text-white">{t('dashboardTitle')}</h1>
        <p className="text-sm text-gray-400 mt-2 leading-relaxed">{t('dashboardSubtitle')}</p>
      </header>

      <div className="space-y-3">
        {rows.map((r) => {
          const isSubscribed = r.status === 'confirmed';
          const isPending = r.status === 'pending';
          const canResub = r.status === 'unsubscribed' || r.status === 'bounced' || r.status === 'complained' || r.status === 'deleted';
          const subscribeLabel = canResub ? t('dashboardResubscribe') : t('dashboardSubscribe');

          return (
            <div
              key={r.locale}
              className="rounded-xl border border-white/10 bg-white/5 p-5 flex flex-col sm:flex-row sm:items-center gap-4"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-3">
                  <h2 className="text-base font-semibold text-white">{copy[r.locale].label}</h2>
                  <span className={`text-xs uppercase tracking-wider ${statusTone(r.status)}`}>
                    {statusLabel(r.status)}
                  </span>
                </div>
                <p className="text-sm text-gray-400 mt-1">{copy[r.locale].desc}</p>
              </div>
              <form action={toggleSubscription} className="shrink-0">
                <input type="hidden" name="locale" value={r.locale} />
                <input type="hidden" name="pageLocale" value={locale} />
                <input
                  type="hidden"
                  name="action"
                  value={isSubscribed || isPending ? 'unsubscribe' : 'subscribe'}
                />
                <button
                  type="submit"
                  className={`px-4 py-2 rounded-lg text-sm font-semibold transition-colors ${
                    isSubscribed || isPending
                      ? 'border border-gray-700 bg-gray-800/50 text-gray-200 hover:bg-gray-800'
                      : 'bg-blue-600 text-white hover:bg-blue-500'
                  }`}
                >
                  {isSubscribed || isPending ? t('dashboardUnsubscribeAction') : subscribeLabel}
                </button>
              </form>
            </div>
          );
        })}
      </div>

      <p className="text-xs text-gray-500 pt-2">
        {t('privacyNote')}
      </p>
    </div>
  );
}
