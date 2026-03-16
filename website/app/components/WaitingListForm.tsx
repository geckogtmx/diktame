'use client';

import { useState } from 'react';
import { submitWaitlist } from '../actions/waitlist';
import { ViralSuccessCard } from './ViralSuccessCard';

export function WaitingListForm() {
  const [isPending, setIsPending] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [userData, setUserData] = useState<{ id: string; name: string } | null>(null);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsPending(true);
    setMessage(null);

    const formData = new FormData(event.currentTarget);
    const result = await submitWaitlist(formData);

    setIsPending(false);

    if (result.error) {
      setMessage({ type: 'error', text: result.error });
    } else if (result.success) {
      if (result.id && result.name) {
        setUserData({ id: result.id, name: result.name });
      }
      setMessage({ 
        type: 'success', 
        text: result.message || 'Thank you! You have been added to the waiting list.' 
      });
      (event.target as HTMLFormElement).reset();
    }
  }

  if (userData) {
    return <ViralSuccessCard senderId={userData.id} senderName={userData.name} />;
  }

  return (
    <div className="w-full max-w-md mx-auto">
      {message ? (
        <div className={`p-4 rounded-lg border mb-6 animate-fade-in ${
          message.type === 'success' 
            ? 'bg-blue-500/10 border-blue-500/20 text-blue-400' 
            : 'bg-red-500/10 border-red-500/20 text-red-400'
        }`}>
          {message.text}
          {message.type === 'success' && (
            <button 
              onClick={() => setMessage(null)}
              className="block mt-2 text-xs underline opacity-70 hover:opacity-100"
            >
              Sign up another email
            </button>
          )}
        </div>
      ) : null}

      {!message || message.type === 'error' ? (
        <form onSubmit={handleSubmit} className="space-y-4 animate-fade-in">
          <div>
            <label htmlFor="name" className="block text-sm font-medium text-muted mb-1.5 ml-1">
              What&apos;s your name?
            </label>
            <input
              type="text"
              id="name"
              name="name"
              required
              disabled={isPending}
              placeholder="e.g. John Doe"
              className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all disabled:opacity-50"
            />
          </div>

          <div>
            <label htmlFor="email" className="block text-sm font-medium text-muted mb-1.5 ml-1">
              Email Address
            </label>
            <input
              type="email"
              id="email"
              name="email"
              required
              disabled={isPending}
              placeholder="john@example.com"
              className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all disabled:opacity-50"
            />
          </div>

          <button
            type="submit"
            disabled={isPending}
            className="w-full py-4 rounded-xl bg-primary text-white font-bold hover:bg-primary/80 transition-all shadow-glow hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50 disabled:scale-100 flex items-center justify-center gap-2"
          >
            {isPending ? (
              <>
                <div className="w-5 h-5 border-2 border-white/20 border-t-white rounded-full animate-spin"></div>
                Signing you up...
              </>
            ) : (
              'Join the Waiting List'
            )}
          </button>
        </form>
      ) : null}
    </div>
  );
}
