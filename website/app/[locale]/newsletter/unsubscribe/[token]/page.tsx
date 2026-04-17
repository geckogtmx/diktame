// Newsletter unsubscribe landing page.
// User arrives here from the email's one-click unsubscribe link.
// Idempotent: hitting it twice just shows "unsubscribed" without erroring.

import { getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { createAdminClient } from '@/lib/supabase/server';

export const dynamic = 'force-dynamic';

type Outcome = 'success' | 'invalid';

async function unsubscribe(token: string): Promise<{ outcome: Outcome; locale: string }> {
  const supabase = await createAdminClient();

  const { data: sub, error } = await supabase
    .from('newsletter_subscribers')
    .select('id, locale, status')
    .eq('unsubscribe_token', token)
    .maybeSingle();

  if (error) {
    console.error('Unsubscribe select error:', error);
    return { outcome: 'invalid', locale: 'en' };
  }
  if (!sub) return { outcome: 'invalid', locale: 'en' };

  const locale = (sub.locale as string) ?? 'en';

  if (sub.status === 'unsubscribed') {
    return { outcome: 'success', locale };
  }

  const { error: updateErr } = await supabase
    .from('newsletter_subscribers')
    .update({
      status: 'unsubscribed',
      unsubscribed_at: new Date().toISOString(),
    })
    .eq('id', sub.id);

  if (updateErr) {
    console.error('Unsubscribe update error:', updateErr);
    return { outcome: 'invalid', locale };
  }

  return { outcome: 'success', locale };
}

export default async function NewsletterUnsubscribePage({
  params,
}: {
  params: Promise<{ locale: string; token: string }>;
}) {
  const { locale, token } = await params;
  const t = await getTranslations({ locale, namespace: 'Newsletter' });

  const { outcome } = await unsubscribe(token);

  const heading = outcome === 'success' ? t('unsubscribeTitle') : t('confirmInvalidTitle');
  const body = outcome === 'success' ? t('unsubscribeBody') : t('unsubscribeInvalid');

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center px-4 py-20">
      <div className="w-full max-w-lg rounded-2xl border border-gray-800 bg-gray-900/70 backdrop-blur-xl p-10 text-center">
        <div className="text-lg font-bold text-white mb-6">
          dIKta<span className="text-blue-500">.</span>me
        </div>
        <h1 className="text-3xl font-extrabold tracking-tight text-white mb-4">{heading}</h1>
        <p className="text-gray-400 leading-relaxed mb-8">{body}</p>
        {outcome === 'success' && (
          <Link
            href={`/newsletter/preferences/${token}`}
            className="mr-3 inline-block rounded-lg border border-gray-700 px-5 py-3 text-sm font-semibold text-gray-300 hover:bg-gray-800 transition-colors"
          >
            {t('preferencesTitle')}
          </Link>
        )}
        <Link
          href="/blog"
          className="inline-block rounded-lg bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-500"
        >
          {t('backToBlog')}
        </Link>
      </div>
    </div>
  );
}
