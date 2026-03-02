// API route Supabase client that supports both cookie auth (web) and Bearer token auth (desktop app).

import { createClient as createCookieClient } from './server';
import { createClient } from '@supabase/supabase-js';

/**
 * Creates an authenticated Supabase client for API routes.
 * Reads Authorization: Bearer <token> header first (desktop app),
 * falls back to cookie-based auth (web browser).
 */
export async function createApiClient(request: Request) {
  const authHeader = request.headers.get('authorization');

  if (authHeader?.startsWith('Bearer ')) {
    const token = authHeader.slice(7);
    return createClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!,
      { global: { headers: { Authorization: `Bearer ${token}` } } }
    );
  }

  // Fall back to cookie-based auth for web requests
  return createCookieClient();
}
