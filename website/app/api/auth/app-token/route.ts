// Server-side route: if the user is already signed in, redirect to the
// diktame:// deeplink so the desktop app receives the session token.
import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';
import { buildDeeplinkPage } from '@/lib/auth/deeplink-page';

export async function GET() {
  const supabase = await createClient();
  const {
    data: { session },
  } = await supabase.auth.getSession();

  if (session) {
    const deeplinkUrl = `diktame://auth?token=${session.access_token}${session.refresh_token ? `&refresh_token=${session.refresh_token}` : ''}`;

    return new NextResponse(buildDeeplinkPage(deeplinkUrl), {
      status: 200,
      headers: { 'Content-Type': 'text/html' },
    });
  }

  // Not signed in — redirect to login with mode=app so the OAuth flow fires
  return NextResponse.redirect(new URL('/login?mode=app', process.env.NEXT_PUBLIC_SITE_URL ?? 'https://dikta.me'));
}
