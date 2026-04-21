'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';

type Props = {
  postId: string;
  locale: 'en' | 'es';
  label: string;
  force?: boolean;
  confirmText?: string;
  /** Prior-send info loaded server-side. If set, the button defaults to a "Sent" pill with a Resend affordance. */
  priorSend?: {
    status: string;
    subscriber_count: number | null;
    completed_at: string | null;
    started_at: string | null;
  } | null;
};

function formatSentAt(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

export default function ResendButton({ postId, locale, label, force, confirmText, priorSend }: Props) {
  const [state, setState] = useState<'idle' | 'sending' | 'done' | 'error'>('idle');
  const [message, setMessage] = useState<string>('');
  const router = useRouter();

  async function send(useForce: boolean) {
    setState('sending');
    setMessage('');
    try {
      const res = await fetch('/api/hqbackstage/newsletter/resend', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ post_id: postId, locale, force: useForce }),
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

  async function onClick() {
    if (confirmText && !window.confirm(confirmText)) return;
    await send(force === true);
  }

  async function onForceResend() {
    const confirmMsg = `Already sent ${formatSentAt(priorSend?.completed_at ?? priorSend?.started_at ?? null)}. Send ${locale.toUpperCase()} again? Subscribers will get a duplicate email.`;
    if (!window.confirm(confirmMsg)) return;
    await send(true);
  }

  const base =
    'inline-flex items-center gap-1 rounded px-2 py-1 text-xs font-medium transition-colors';

  // When a prior send exists AND we haven't just acted, render the sent pill + tiny resend link.
  const showSentPill = !!priorSend && state === 'idle';

  if (showSentPill) {
    const when = formatSentAt(priorSend.completed_at ?? priorSend.started_at);
    const n = priorSend.subscriber_count ?? 0;
    return (
      <span className="inline-flex items-center gap-2">
        <span
          title={`${priorSend.status} · ${n} recipient${n === 1 ? '' : 's'}${when ? ` · ${when}` : ''}`}
          className={`${base} bg-green-500/15 text-green-300 cursor-default`}
        >
          ✓ Sent {locale.toUpperCase()}{when ? ` · ${when}` : ''}
        </span>
        <button
          type="button"
          onClick={onForceResend}
          className="text-[11px] text-gray-500 hover:text-yellow-300 transition-colors underline underline-offset-2 decoration-dotted"
          title="Send again (will duplicate for subscribers)"
        >
          resend
        </button>
      </span>
    );
  }

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
