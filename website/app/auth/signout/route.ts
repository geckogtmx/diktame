// SPEC_042: Sign out route handler
import { createClient } from '@/lib/supabase/server';
import { cookies } from 'next/headers';
import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';

async function signOut() {
  const supabase = await createClient();
  await supabase.auth.signOut();
  const cookieStore = await cookies();
  const locale = cookieStore.get('NEXT_LOCALE')?.value ?? 'en';
  revalidatePath('/', 'layout');
  redirect(`/${locale}`);
}

export async function POST() {
  await signOut();
}

// Handle direct navigation (GET) to /auth/signout without 405
export async function GET() {
  await signOut();
}
