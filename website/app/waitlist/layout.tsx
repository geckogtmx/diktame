import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Join the Waitlist - dIKta.me',
  description: 'Sign up for early access to dIKta.me V2 — the most powerful local AI voice dictation tool for Windows. Be first in line for founder perks and private updates.',
};

export default function WaitlistLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
