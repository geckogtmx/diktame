// SPEC_042: Middleware for authentication
// Refreshes auth session on every request

import { createServerClient } from '@supabase/ssr';
import { NextResponse, type NextRequest } from 'next/server';

// SPEC_042: Coming Soon gate — blocks signup/login until launch.
// To enable: set NEXT_PUBLIC_COMING_SOON=true in Vercel env vars.
// To launch: delete the env var (no code changes needed).
const COMING_SOON = process.env.NEXT_PUBLIC_COMING_SOON === 'true';

// Routes blocked while coming soon mode is active
const BLOCKED_PATHS = ['/login', '/auth'];

export async function middleware(request: NextRequest) {
  const { pathname, searchParams } = request.nextUrl;

  // Redirect blocked routes to /coming-soon, EXCEPT:
  // - /coming-soon itself
  // - /login?mode=app (desktop app OAuth flow — must work during testing)
  // - /auth/callback (OAuth callback must complete regardless)
  if (COMING_SOON && pathname !== '/coming-soon') {
    const isAppLogin = pathname === '/login' && searchParams.get('mode') === 'app';
    const isAuthCallback = pathname.startsWith('/auth/callback');
    const isBlocked = BLOCKED_PATHS.some((p) => pathname === p || pathname.startsWith(p + '/'));
    if (isBlocked && !isAppLogin && !isAuthCallback) {
      return NextResponse.redirect(new URL('/coming-soon', request.url));
    }
  }

  const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL;
  const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY;

  // Skip session refresh if env vars not configured (e.g. during build or cold start)
  if (!supabaseUrl || !supabaseAnonKey) {
    return NextResponse.next({ request });
  }

  let supabaseResponse = NextResponse.next({
    request,
  });

  const supabase = createServerClient(supabaseUrl, supabaseAnonKey, {
    cookies: {
      getAll() {
        return request.cookies.getAll();
      },
      setAll(cookiesToSet) {
        cookiesToSet.forEach(({ name, value }) => request.cookies.set(name, value));
        supabaseResponse = NextResponse.next({
          request,
        });
        cookiesToSet.forEach(({ name, value, options }) =>
          supabaseResponse.cookies.set(name, value, options)
        );
      },
    },
  });

  // Refresh session if expired
  await supabase.auth.getUser();

  return supabaseResponse;
}

export const config = {
  matcher: [
    /*
     * Match all request paths except for the ones starting with:
     * - _next/static (static files)
     * - _next/image (image optimization files)
     * - favicon.ico (favicon file)
     * Feel free to modify this pattern to include more paths.
     */
    '/((?!_next/static|_next/image|favicon.ico|.*\\.(?:svg|png|jpg|jpeg|gif|webp)$).*)',
  ],
};
