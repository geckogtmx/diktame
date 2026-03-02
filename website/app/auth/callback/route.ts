// SPEC_042: OAuth callback handler
import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  const { searchParams, origin } = new URL(request.url);
  const code = searchParams.get('code');
  const next = searchParams.get('next') ?? '/dashboard';
  const mode = searchParams.get('mode'); // 'app' for desktop app auth

  if (code) {
    const supabase = await createClient();
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (!error) {
      // If mode=app, redirect to deeplink for desktop app
      if (mode === 'app') {
        const {
          data: { session },
        } = await supabase.auth.getSession();
        if (session) {
          // Redirect to app deeplink with session token
          return NextResponse.redirect(`diktame://auth?token=${session.access_token}`);
        }
      }

      // Normal web flow - redirect to dashboard
      const forwardedHost = request.headers.get('x-forwarded-host');
      const isLocalEnv = process.env.NODE_ENV === 'development';

      if (isLocalEnv) {
        return NextResponse.redirect(`${origin}${next}`);
      } else if (forwardedHost) {
        return NextResponse.redirect(`https://${forwardedHost}${next}`);
      } else {
        return NextResponse.redirect(`${origin}${next}`);
      }
    }
  }

  // Return to login on error
  return NextResponse.redirect(`${origin}/login`);
}
