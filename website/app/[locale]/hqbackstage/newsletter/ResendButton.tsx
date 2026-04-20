'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

type Props = {
  postId: string;
  locale: 'en' | 'es';
  label: string;
  force?: boolean;
  confirmText?: string;
};

export default function ResendButton({ postId, locale, label, force, confirmText }: Props) {
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle');
  const [message, setMessage] = useState<string>('');
  const router = useRouter();

  async function onClick() {
    if (confirmText && !window.confirm(confirmText)) return;
    setState('sending');
    setMessage('');
    try {
      const res = await fetch('/api/hqbackstage/newsletter/resend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ post_id: postId, locale, force: force === true }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok) {
        setState('error');
        setMessage(body?.error ?? `HTTP ${res.status}`);
        return;
      }
      setState('done');
      const n = body?.subscriber_count ?? 0;
      setMessage(`${body?.status ?? 'done'} · ${n} recipient${n === 1 ? '' : 's'}`);
      router.refresh();
    } catch (err) {
      setState('error');
      setMessage(err instanceof Error ? err.message : 'Network error');
    }
  }

  const base =
    'inline-flex items-center gap-1 rounded px-2 py-1 text-xs font-medium transition-colors';
  const variant =
    state === 'done'
      ? 'bg-green-500/20 text-green-300'
      : state === 'error'
        ? 'bg-red-500/20 text-red-300'
        : state === 'sending'
          ? 'bg-gray-500/20 text-gray-400 cursor-wait'
          : force
            ? 'bg-yellow-500/20 text-yellow-300 hover:bg-yellow-500/30'
            : 'bg-blue-500/20 text-blue-300 hover:bg-blue-500/30';

  return (
    <span className="inline-flex items-center gap-2">
      <button
        type="button"
        onClick={onClick}
        disabled={state === 'sending' || state === 'done'}
        className={`${base} ${variant}`}
      >
        {state === 'sending' ? 'Sending…' : state === 'done' ? '✓ Sent' : label}
      </button>
      {message && (
        <span
          className={`text-xs ${
            state === 'error' ? 'text-red-300' : 'text-gray-400'
          }`}
        >
          {message}
        </span>
      )}
    </span>
  );
}
