import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Sign In - dIKta.me',
  description: 'Sign in to your dIKta.me account to access your dashboard, manage settings, and sync across devices.',
  robots: 'noindex, nofollow',
};

export default function LoginLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return children;
}
