// SPEC_042: Sign out route handler
import { createClient } from '@/lib/supabase/server';
import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';

async function signOut() {
  const supabase = await createClient();
  await supabase.auth.signOut();
  revalidatePath('/', 'layout');
  redirect('/');
}

export async function POST() {
  await signOut();
}

// Handle direct navigation (GET) to /auth/signout without 405
export async function GET() {
  await signOut();
}
