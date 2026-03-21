// Server-side route: if the user is already signed in, redirect to the
// diktame:// deeplink so the desktop app receives the session token.
import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';

export async function GET() {
  const supabase = await createClient();
  const {
    data: { session },
  } = await supabase.auth.getSession();

  if (session) {
    const deeplink = new URL('diktame://auth');
    deeplink.searchParams.set('token', session.access_token);
    if (session.refresh_token) {
      deeplink.searchParams.set('refresh_token', session.refresh_token);
    }
    return NextResponse.redirect(deeplink.toString());
  }

  // Not signed in — redirect to login with mode=app so the OAuth flow fires
  return NextResponse.redirect(new URL('/login?mode=app', process.env.NEXT_PUBLIC_SITE_URL ?? 'https://dikta.me'));
}
