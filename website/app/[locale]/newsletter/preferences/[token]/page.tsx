// Newsletter preference center.
// Accessed via the persistent unsubscribe_token link in every email footer.
// Supports: unsubscribe, resubscribe, soft-delete (GDPR data deletion).
// Token remains valid after unsubscribe so users can resubscribe themselves.

import { getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { revalidatePath } from 'next/cache';
import { createAdminClient } from '@/lib/supabase/server';

export const dynamic = 'force-dynamic';

type Subscriber = {
  id: string;
  email: string;
  locale: string;
  status: string;
};

async function loadSubscriber(token: string): Promise<Subscriber | null> {
  const supabase = await createAdminClient();
  const { data, error } = await supabase
    .from('newsletter_subscribers')
    .select('id, email, locale, status')
    .eq('unsubscribe_token', token)
    .maybeSingle();
  if (error) {
    console.error('Preferences select error:', error);
    return null;
  }
  return (data as Subscriber) ?? null;
}

async function applyAction(formData: FormData) {
  'use server';
  const token = formData.get('token')?.toString();
  const action = formData.get('action')?.toString();
  const locale = formData.get('locale')?.toString() ?? 'en';
  if (!token || !action) return;

  const supabase = await createAdminClient();
  const { data: sub } = await supabase
    .from('newsletter_subscribers')
    .select('id, email, status')
    .eq('unsubscribe_token', token)
    .maybeSingle();
  if (!sub) return;

  const now = new Date().toISOString();

  if (action === 'unsubscribe' && sub.status !== 'unsubscribed') {
    await supabase
      .from('newsletter_subscribers')
      .update({ status: 'unsubscribed', unsubscribed_at: now })
      .eq('id', sub.id);
  } else if (action === 'resubscribe' && sub.status === 'unsubscribed') {
    await supabase
      .from('newsletter_subscribers')
      .update({ status: 'confirmed', confirmed_at: now, unsubscribed_at: null })
      .eq('id', sub.id);
  } else if (action === 'delete' && sub.status !== 'deleted') {
    await supabase
      .from('newsletter_subscribers')
      .update({
        status: 'deleted',
        email: `deleted-${sub.id}@anon.local`,
        confirm_token: null,
        unsubscribe_token: null,
        signup_ip: null,
        signup_user_agent: null,
        unsubscribed_at: now,
      })
      .eq('id', sub.id);
  }

  revalidatePath(`/${locale}/newsletter/preferences/${token}`);
}

export default async function NewsletterPreferencesPage({
  params,
}: {
  params: Promise<{ locale: string; token: string }>;
}) {
  const { locale, token } = await params;
  const t = await getTranslations({ locale, namespace: 'Newsletter' });
  const sub = await loadSubscriber(token);

  if (!sub) {
    return (
      <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center px-4 py-20">
        <div className="w-full max-w-lg rounded-2xl border border-gray-800 bg-gray-900/70 backdrop-blur-xl p-10 text-center">
          <div className="text-lg font-bold text-white mb-6">
            dIKta<span className="text-blue-500">.</span>me
          </div>
          <h1 className="text-2xl font-extrabold text-white mb-3">
            {t('confirmInvalidTitle')}
          </h1>
          <p className="text-gray-400 mb-8">{t('unsubscribeInvalid')}</p>
          <Link
            href="/blog"
            className="inline-block rounded-lg bg-blue-600 px-6 py-3 text-sm font-semibold text-white hover:bg-blue-500 transition-colors"
          >
            {t('backToBlog')}
          </Link>
        </div>
      </div>
    );
  }

  const statusLabel =
    sub.status === 'confirmed' ? t('statusSubscribed')
      : sub.status === 'unsubscribed' ? t('statusUnsubscribed')
      : sub.status === 'pending' ? t('statusPending')
      : sub.status === 'deleted' ? t('statusDeleted')
      : sub.status;

  const canResub = sub.status === 'unsubscribed';
  const canUnsub = sub.status === 'confirmed' || sub.status === 'pending';
  const canDelete = sub.status !== 'deleted';

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center px-4 py-20">
      <div className="w-full max-w-lg rounded-2xl border border-gray-800 bg-gray-900/70 backdrop-blur-xl p-10">
        <div className="text-lg font-bold text-white text-center mb-8">
          dIKta<span className="text-blue-500">.</span>me
        </div>
        <h1 className="text-2xl font-extrabold text-white text-center mb-8">
          {t('preferencesTitle')}
        </h1>

        <div className="space-y-3 mb-8">
          <div className="flex justify-between text-sm border-b border-gray-800 pb-3">
            <span className="text-gray-400">Email</span>
            <span className="text-white font-medium break-all">{sub.email}</span>
          </div>
          <div className="flex justify-between text-sm">
            <span className="text-gray-400">{t('statusLabel')}</span>
            <span className="text-white font-medium">{statusLabel}</span>
          </div>
        </div>

        <h2 className="text-sm font-bold text-white uppercase tracking-wide mb-3">
          {t('actions')}
        </h2>
        <div className="space-y-2">
          {canResub && (
            <form action={applyAction}>
              <input type="hidden" name="token" value={token} />
              <input type="hidden" name="locale" value={locale} />
              <input type="hidden" name="action" value="resubscribe" />
              <button
                type="submit"
                className="w-full rounded-lg bg-blue-600 px-4 py-3 text-sm font-semibold text-white hover:bg-blue-500 transition-colors"
              >
                {t('resubscribe')}
              </button>
            </form>
          )}
          {canUnsub && (
            <form action={applyAction}>
              <input type="hidden" name="token" value={token} />
              <input type="hidden" name="locale" value={locale} />
              <input type="hidden" name="action" value="unsubscribe" />
              <button
                type="submit"
                className="w-full rounded-lg border border-gray-700 bg-gray-800/50 px-4 py-3 text-sm font-semibold text-gray-200 hover:bg-gray-800 transition-colors"
              >
                {t('unsubscribe')}
              </button>
            </form>
          )}
          {canDelete && (
            <form action={applyAction}>
              <input type="hidden" name="token" value={token} />
              <input type="hidden" name="locale" value={locale} />
              <input type="hidden" name="action" value="delete" />
              <button
                type="submit"
                className="w-full rounded-lg border border-red-900/50 bg-red-950/40 px-4 py-3 text-sm font-semibold text-red-300 hover:bg-red-950/70 transition-colors"
              >
                {t('deleteData')}
              </button>
            </form>
          )}
        </div>

        <div className="mt-8 text-center">
          <Link href="/blog" className="text-xs text-blue-400 hover:text-blue-300">
            {t('backToBlog')}
          </Link>
        </div>
      </div>
    </div>
  );
}
