// Server-side route: if the user is already signed in, redirect to the
// diktame:// deeplink so the desktop app receives the session token.
import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';

export async function GET() {
  console.log('[app-token] GET hit');

  const supabase = await createClient();
  const {
    data: { session },
    error,
  } = await supabase.auth.getSession();

  console.log('[app-token] session?', !!session, 'error?', error?.message ?? 'none');

  if (session) {
    const deeplinkUrl = `diktame://auth?token=${session.access_token}${session.refresh_token ? `&refresh_token=${session.refresh_token}` : ''}`;
    console.log('[app-token] redirecting to deeplink, token length:', session.access_token.length);

    // NextResponse.redirect() may reject custom URL schemes — return an HTML page
    // that does the redirect client-side instead.
    return new NextResponse(
      `<!DOCTYPE html><html><head><meta http-equiv="refresh" content="0;url=${deeplinkUrl}"/></head><body><p>Redirecting to dIKta.me app...</p><script>window.location.href=${JSON.stringify(deeplinkUrl)};</script></body></html>`,
      {
        status: 200,
        headers: { 'Content-Type': 'text/html' },
      },
    );
  }

  // Not signed in — redirect to login with mode=app so the OAuth flow fires
  console.log('[app-token] no session, redirecting to /login?mode=app');
  return NextResponse.redirect(new URL('/login?mode=app', process.env.NEXT_PUBLIC_SITE_URL ?? 'https://dikta.me'));
}
