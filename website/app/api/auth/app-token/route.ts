// Server-side route: if the user is already signed in, redirect to the
// diktame:// deeplink so the desktop app receives the session token.
import { createClient } from '@/lib/supabase/server';
import { cookies } from 'next/headers';
import { NextResponse } from 'next/server';
import { buildDeeplinkPage } from '@/lib/auth/deeplink-page';

export async function GET() {
  const supabase = await createClient();
  const {
    data: { session },
  } = await supabase.auth.getSession();

  const cookieStore = await cookies();
  const locale = cookieStore.get('NEXT_LOCALE')?.value ?? 'en';

  if (session) {
    const deeplinkUrl = `diktame://auth?token=${session.access_token}${session.refresh_token ? `&refresh_token=${session.refresh_token}` : ''}`;

    return new NextResponse(buildDeeplinkPage(deeplinkUrl, locale), {
      status: 200,
      headers: { 'Content-Type': 'text/html' },
    });
  }

  // Not signed in — redirect to login with mode=app so the OAuth flow fires
  return NextResponse.redirect(new URL(`/${locale}/login?mode=app`, process.env.NEXT_PUBLIC_SITE_URL ?? 'https://dikta.me'));
}
