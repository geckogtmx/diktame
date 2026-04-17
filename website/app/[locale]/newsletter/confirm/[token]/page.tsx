// Newsletter confirmation landing page.
// User arrives here from the email's "Confirm subscription" link.
// This server component performs the DB flip and dispatches the welcome
// email, then renders a nicely-styled result page.

import { getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';
import { createAdminClient } from '@/lib/supabase/server';

export const dynamic = 'force-dynamic';

const RESEND_API_KEY = process.env.RESEND_API_KEY;
const SUPABASE_URL = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const SITE_URL = 'https://www.dikta.me';

type Outcome = 'success' | 'already' | 'invalid';

function welcomeEmailHtml(locale: string, preferencesUrl: string, unsubscribeUrl: string) {
  const copy = locale === 'es'
    ? {
        title: '¡Bienvenido a dIKta.me!',
        intro: 'Estás dentro. A partir de ahora recibirás cada nueva publicación directamente en tu bandeja de entrada, con el audio incluido.',
        cta: 'Visitar el blog',
        prefs: 'Gestionar preferencias',
        unsub: 'Cancelar suscripción',
      }
    : {
        title: 'Welcome to dIKta.me!',
        intro: "You're in. From now on, every new post lands in your inbox — full text, with audio.",
        cta: 'Visit the blog',
        prefs: 'Manage preferences',
        unsub: 'Unsubscribe',
      };

  return `<!DOCTYPE html>
<html><head><meta charset="utf-8"><style>
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:#020617;color:#fff;margin:0;padding:40px}
.c{max-width:600px;margin:0 auto;background:#0f172a;border:1px solid #1e293b;border-radius:16px;padding:40px}
.logo{font-size:24px;font-weight:bold;margin-bottom:32px}
h1{font-size:26px;font-weight:800;margin:0 0 16px;letter-spacing:-0.02em}
p{font-size:16px;line-height:1.6;color:#94a3b8;margin:0 0 20px}
.btn{display:inline-block;background:#2563eb;color:#fff;padding:14px 28px;border-radius:8px;text-decoration:none;font-weight:bold;margin:8px 0 24px}
.foot{margin-top:32px;padding-top:20px;border-top:1px solid #1e293b;font-size:12px;color:#64748b}
.foot a{color:#94a3b8}
</style></head><body><div class="c">
<div class="logo">dIKta<span style="color:#2563eb">.</span>me</div>
<h1>${copy.title}</h1>
<p>${copy.intro}</p>
<a class="btn" href="${SITE_URL}/${locale}/blog">${copy.cta}</a>
<div class="foot">
<a href="${preferencesUrl}">${copy.prefs}</a> · <a href="${unsubscribeUrl}">${copy.unsub}</a><br><br>
San Francisco 1826-C-101, Del Valle, 03100, CDMX, México
</div>
</div></body></html>`;
}

async function confirmSubscription(token: string): Promise<{
  outcome: Outcome;
  locale: string;
}> {
  const supabase = await createAdminClient();

  const { data: sub, error } = await supabase
    .from('newsletter_subscribers')
    .select('id, email, locale, status, unsubscribe_token')
    .eq('confirm_token', token)
    .maybeSingle();

  if (error) {
    console.error('Confirm select error:', error);
    return { outcome: 'invalid', locale: 'en' };
  }
  if (!sub) {
    // Token may also match an already-confirmed row where confirm_token was
    // cleared; we can't distinguish without a second lookup. Return invalid.
    return { outcome: 'invalid', locale: 'en' };
  }

  const locale = (sub.locale as string) ?? 'en';

  if (sub.status === 'confirmed') {
    return { outcome: 'already', locale };
  }

  const { error: updateErr } = await supabase
    .from('newsletter_subscribers')
    .update({
      status: 'confirmed',
      confirmed_at: new Date().toISOString(),
      confirm_token: null,
    })
    .eq('id', sub.id);

  if (updateErr) {
    console.error('Confirm update error:', updateErr);
    return { outcome: 'invalid', locale };
  }

  // Send welcome email (fire-and-forget)
  if (RESEND_API_KEY && sub.unsubscribe_token) {
    const preferencesUrl = `${SITE_URL}/${locale}/newsletter/preferences/${sub.unsubscribe_token}`;
    const unsubscribeUrl = `${SITE_URL}/${locale}/newsletter/unsubscribe/${sub.unsubscribe_token}`;
    const subject = locale === 'es' ? '¡Bienvenido a dIKta.me!' : 'Welcome to dIKta.me!';
    fetch('https://api.resend.com/emails', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${RESEND_API_KEY}`,
      },
      body: JSON.stringify({
        from: 'dIKta.me <newsletter@dikta.me>',
        reply_to: 'newsletter@dikta.me',
        to: [sub.email],
        subject,
        html: welcomeEmailHtml(locale, preferencesUrl, unsubscribeUrl),
        headers: {
          'List-Unsubscribe': `<${unsubscribeUrl}>`,
          'List-Unsubscribe-Post': 'List-Unsubscribe=One-Click',
        },
      }),
    }).catch((err) => console.error('Welcome email failed:', err));
  }

  return { outcome: 'success', locale };
}

export default async function NewsletterConfirmPage({
  params,
}: {
  params: Promise<{ locale: string; token: string }>;
}) {
  const { locale, token } = await params;
  const t = await getTranslations({ locale, namespace: 'Newsletter' });

  const { outcome } = await confirmSubscription(token);

  const heading =
    outcome === 'success' ? t('confirmTitle')
      : outcome === 'already' ? t('confirmAlready')
      : t('confirmInvalidTitle');
  const body =
    outcome === 'success' ? t('confirmBody')
      : outcome === 'already' ? t('confirmAlreadyBody')
      : t('confirmInvalidBody');

  return (
    <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center px-4 py-20">
      <div className="w-full max-w-lg rounded-2xl border border-gray-800 bg-gray-900/70 backdrop-blur-xl p-10 text-center">
        <div className="text-lg font-bold text-white mb-6">
          dIKta<span className="text-blue-500">.</span>me
        </div>
        <h1 className="text-3xl font-extrabold tracking-tight text-white mb-4">{heading}</h1>
        <p className="text-gray-400 leading-relaxed mb-8">{body}</p>
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
