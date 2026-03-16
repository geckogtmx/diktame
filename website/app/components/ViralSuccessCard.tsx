'use client';

import { useState, useEffect } from 'react';
import { sendWaitlistInvite, getWaitlistInvites } from '../actions/waitlist';

interface ViralSuccessCardProps {
  senderId: string;
  senderName: string;
}

export function ViralSuccessCard({ senderId, senderName }: ViralSuccessCardProps) {
  const [invites, setInvites] = useState<string[]>([]);
  const [currentEmail, setCurrentEmail] = useState('');
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const inviteLimit = 5;
  const remaining = inviteLimit - invites.length;

  useEffect(() => {
    async function fetchInvites() {
      const { invites: existingInvites } = await getWaitlistInvites(senderId);
      if (existingInvites && existingInvites.length > 0) {
        setInvites(existingInvites);
      }
    }
    fetchInvites();
  }, [senderId]);

  async function handleInvite(e: React.FormEvent) {
    e.preventDefault();
    if (!currentEmail || remaining <= 0) return;

    setIsPending(true);
    setError(null);
    setSuccess(null);

    const result = await sendWaitlistInvite(senderId, senderName, currentEmail);

    setIsPending(false);

    if (result.error) {
      setError(result.error);

      // Log details for debugging (visible in browser console)
      console.error('Invitation error details:', result);
    } else {
      setInvites([...invites, currentEmail]);
      setCurrentEmail('');

      // Differentiate between email sent vs. just recorded
      if (result.emailSent) {
        setSuccess(`Priority pass emailed to ${currentEmail}!`);
      } else {
        setSuccess(`Invitation recorded for ${currentEmail} (email status unknown)`);
      }
    }
  }

  function copyInviteText() {
    const text = `Hey! I just joined the dIKta.me V2 waitlist for local-first voice dictation. It looks incredible. I have a few priority passes to gift—here is one for you: https://dikta.me/waitlist`;
    navigator.clipboard.writeText(text);
    setSuccess('Invite text copied to clipboard!');
  }

  const shareUrl = "https://dikta.me/waitlist";
  const shareText = "Just joined the waitlist for dIKta.me - the future of local-first voice dictation! 🚀";

  return (
    <div className="w-full max-w-2xl mx-auto space-y-8 animate-fade-in">
      {/* Header */}
      <div className="text-center space-y-4">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-primary/10 border border-primary/20 text-primary text-xs font-mono uppercase tracking-widest">
          Sign Up Successful
        </div>
        <h2 className="text-3xl font-bold text-white">You&apos;re in, {senderName}!</h2>
        <p className="text-muted max-w-md mx-auto">
          You have <span className="text-white font-bold">{remaining} Priority Passes</span> left to gift. 
          Each friend who joins via your invite gets you higher in the queue.
        </p>
      </div>

      <div className="grid md:grid-cols-2 gap-6">
        {/* Pass Gifting Section */}
        <div className="card p-6 bg-white/5 border-white/10 space-y-6">
          <h3 className="text-sm font-bold text-white uppercase tracking-wider">Gift a Priority Pass</h3>
          
          <form onSubmit={handleInvite} className="space-y-3">
            <input
              type="email"
              value={currentEmail}
              onChange={(e) => setCurrentEmail(e.target.value)}
              placeholder="friend@example.com"
              disabled={isPending || remaining <= 0}
              className="w-full px-4 py-3 rounded-xl bg-black/40 border border-white/10 text-white placeholder:text-white/20 focus:outline-none focus:ring-2 focus:ring-primary/50 transition-all disabled:opacity-50"
            />
            <button
              type="submit"
              disabled={isPending || !currentEmail || remaining <= 0}
              className="w-full py-3 rounded-xl bg-primary text-white font-bold hover:bg-primary/80 transition-all shadow-glow flex items-center justify-center gap-2 disabled:opacity-50"
            >
              {isPending ? 'Sending...' : 'Send Invite'}
            </button>
          </form>

          {/* Slots visualization */}
          <div className="flex gap-2 justify-center">
            {[...Array(inviteLimit)].map((_, i) => (
              <div 
                key={i} 
                className={`h-1.5 flex-1 rounded-full transition-all duration-500 ${
                  i < invites.length ? 'bg-primary shadow-[0_0_8px_rgba(37,99,235,0.5)]' : 'bg-white/10'
                }`} 
              />
            ))}
          </div>

          {error && <p className="text-xs text-red-500 text-center">{error}</p>}
          {success && <p className="text-xs text-primary text-center">{success}</p>}
        </div>

        {/* Social / Manual Section */}
        <div className="card p-6 bg-white/5 border-white/10 space-y-6">
          <h3 className="text-sm font-bold text-white uppercase tracking-wider">Pass it forward manually</h3>
          
          <div className="space-y-3">
            <button
              onClick={copyInviteText}
              className="w-full py-3 px-4 rounded-xl bg-white/5 border border-white/10 text-white text-sm font-medium hover:bg-white/10 transition-all flex items-center justify-between group"
            >
              <span>Copy Personal Invite</span>
              <svg className="w-4 h-4 text-muted group-hover:text-white transition-colors" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3" />
              </svg>
            </button>

            <div className="grid grid-cols-2 gap-3">
              <a
                href={`https://twitter.com/intent/tweet?text=${encodeURIComponent(shareText)}&url=${encodeURIComponent(shareUrl)}`}
                target="_blank"
                rel="noopener noreferrer"
                className="py-3 rounded-xl bg-[#1DA1F2]/10 border border-[#1DA1F2]/20 text-[#1DA1F2] text-sm font-bold hover:bg-[#1DA1F2]/20 transition-all flex items-center justify-center gap-2"
              >
                Share on X
              </a>
              <a
                href={`https://wa.me/?text=${encodeURIComponent(shareText + " " + shareUrl)}`}
                target="_blank"
                rel="noopener noreferrer"
                className="py-3 rounded-xl bg-[#25D366]/10 border border-[#25D366]/20 text-[#25D366] text-sm font-bold hover:bg-[#25D366]/20 transition-all flex items-center justify-center gap-2"
              >
                WhatsApp
              </a>
            </div>
          </div>

          <p className="text-[10px] text-[#64748b] text-center leading-relaxed">
            By sharing, you help us stay independent and local-first. Thank you for your support.
          </p>
        </div>
      </div>
    </div>
  );
}
