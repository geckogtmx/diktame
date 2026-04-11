// SPEC_042: OAuth callback handler
import { createClient, createAdminClient } from '@/lib/supabase/server';
import { NextResponse, type NextRequest } from 'next/server';
import { buildDeeplinkPage } from '@/lib/auth/deeplink-page';

export async function GET(request: NextRequest) {
  const { searchParams, origin } = new URL(request.url);
  const locale = request.cookies.get('NEXT_LOCALE')?.value ?? 'en';
  const code = searchParams.get('code');
  const next = searchParams.get('next') ?? `/${locale}/dashboard`;
  const mode = searchParams.get('mode'); // 'app' for desktop app auth

  if (code) {
    const supabase = await createClient();
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (!error) {
      // Ensure a profiles row exists (trigger handles most cases; this is a safety net)
      const {
        data: { user },
      } = await supabase.auth.getUser();
      if (user) {
        const { data: profile } = await supabase
          .from('profiles')
          .select('id')
          .eq('id', user.id)
          .single();

        if (!profile) {
          // Safety-net profile creation (zeroed trial fields — wallet replaces trial)
          await supabase.from('profiles').insert({
            id: user.id,
            email: user.email,
            name:
              user.user_metadata?.full_name ??
              user.user_metadata?.name ??
              '',
            trial_words_quota: 0,
            trial_words_used: 0,
            trial_expires_at: null,
          });

          // Grant $1.00 promo wallet credit (mirrors handle_new_user trigger)
          // Uses service-role client — wallet_ledger has no INSERT policy for authenticated
          const adminClient = await createAdminClient();
          await adminClient.from('wallet_ledger').insert({
            user_id: user.id,
            amount_micro: 1000000,
            balance_after_micro: 1000000,
            type: 'GRANT',
            expires_at: new Date(Date.now() + 90 * 24 * 60 * 60 * 1000).toISOString(),
            metadata: { reason: 'signup_promotional', amount_usd: '1.00' },
            order_ref: `signup_promo_${user.id}`,
          });
        }
      }

      // If mode=app, redirect to deeplink for desktop app
      if (mode === 'app') {
        const {
          data: { session },
        } = await supabase.auth.getSession();
        if (session) {
          const deeplinkUrl = `diktame://auth?token=${session.access_token}${session.refresh_token ? `&refresh_token=${session.refresh_token}` : ''}`;
          return new NextResponse(buildDeeplinkPage(deeplinkUrl, locale), {
            status: 200,
            headers: { 'Content-Type': 'text/html' },
          });
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
  return NextResponse.redirect(`${origin}/${locale}/login`);
}
