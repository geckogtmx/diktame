'use client';

// Blog-sidebar newsletter signup form. Posts to the public newsletter-subscribe
// edge function and shows a "check your inbox" confirmation on success.

import { useState } from 'react';
import { useTranslations, useLocale } from 'next-intl';

const SUPABASE_URL = process.env.NEXT_PUBLIC_SUPABASE_URL!;
const ANON_KEY = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;

type Status = 'idle' | 'submitting' | 'pending' | 'already' | 'error';

export function NewsletterSignupBox() {
  const t = useTranslations('Newsletter');
  const locale = useLocale();
  const [email, setEmail] = useState('');
  const [status, setStatus] = useState<Status>('idle');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const trimmed = email.trim().toLowerCase();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmed)) {
      setStatus('error');
      setErrorMsg(t('invalidEmail'));
      return;
    }
    setStatus('submitting');
    setErrorMsg(null);
    try {
      const res = await fetch(`${SUPABASE_URL}/functions/v1/newsletter-subscribe`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${ANON_KEY}`,
          apikey: ANON_KEY,
        },
        body: JSON.stringify({ email: trimmed, locale, source: 'blog-sidebar' }),
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        setStatus('error');
        setErrorMsg(t('genericError'));
        return;
      }
      if (data.status === 'already_subscribed') {
        setStatus('already');
      } else {
        setStatus('pending');
      }
    } catch {
      setStatus('error');
      setErrorMsg(t('genericError'));
    }
  }

  if (status === 'pending') {
    return (
      <div className="rounded-xl border border-blue-500/30 bg-blue-500/10 p-5">
        <h3 className="text-sm font-bold text-white mb-1">{t('pendingTitle')}</h3>
        <p className="text-xs text-gray-300 leading-relaxed">{t('pendingBody')}</p>
      </div>
    );
  }

  if (status === 'already') {
    return (
      <div className="rounded-xl border border-green-500/30 bg-green-500/10 p-5">
        <p className="text-sm text-green-200">{t('alreadySubscribed')}</p>
      </div>
    );
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="rounded-xl border border-gray-700/50 bg-gray-800/30 p-5"
    >
      <h3 className="text-sm font-bold text-white mb-1">{t('signupTitle')}</h3>
      <p className="text-xs text-gray-400 mb-3">{t('signupSubtitle')}</p>
      <input
        type="email"
        value={email}
        onChange={(e) => setEmail(e.target.value)}
        placeholder={t('emailPlaceholder')}
        required
        aria-label={t('emailPlaceholder')}
        disabled={status === 'submitting'}
        className="w-full rounded-lg border border-gray-700 bg-gray-900/60 px-3 py-2 text-sm text-white placeholder-gray-500 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:opacity-50"
      />
      <button
        type="submit"
        disabled={status === 'submitting'}
        className="mt-2 w-full rounded-lg bg-blue-600 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
      >
        {status === 'submitting' ? t('submitting') : t('subscribe')}
      </button>
      {status === 'error' && errorMsg && (
        <p className="mt-2 text-xs text-red-400">{errorMsg}</p>
      )}
      <p className="mt-3 text-[11px] text-gray-500">{t('privacyNote')}</p>
    </form>
  );
}
