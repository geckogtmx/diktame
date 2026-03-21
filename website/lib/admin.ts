import { createApiClient } from '@/lib/supabase/api';
import { createClient } from '@supabase/supabase-js';

/**
 * Validates that the request comes from an authenticated admin user.
 * Returns the user + a service-role Supabase client (bypasses RLS for admin queries).
 * Throws Response on auth failure (caller should catch and return it).
 */
export async function requireAdmin(request: Request) {
  const supabase = await createApiClient(request);
  const { data: { user }, error } = await supabase.auth.getUser();

  if (error || !user) {
    throw new Response(JSON.stringify({ error: 'Unauthorized' }), {
      status: 401,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  const { data: profile } = await supabase
    .from('profiles')
    .select('is_admin')
    .eq('id', user.id)
    .single();

  if (!profile?.is_admin) {
    throw new Response(JSON.stringify({ error: 'Forbidden' }), {
      status: 403,
      headers: { 'Content-Type': 'application/json' },
    });
  }

  // Return service-role client for admin operations (bypasses RLS)
  const serviceClient = createClient(
    process.env.NEXT_PUBLIC_SUPABASE_URL!,
    process.env.SUPABASE_SERVICE_ROLE_KEY!,
    { auth: { persistSession: false } }
  );

  return { user, supabase: serviceClient };
}
