'use server';

import { createClient } from '@/lib/supabase/server';
import { revalidatePath } from 'next/cache';

export async function submitWaitlist(formData: FormData) {
  const name = formData.get('name') as string;
  const email = formData.get('email') as string;

  if (!name || !email) {
    return { error: 'Name and email are required.' };
  }

  // Simple email validation
  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    return { error: 'Please enter a valid email address.' };
  }

  const supabase = await createClient();

  const { error } = await supabase
    .from('waiting_list')
    .insert([{ name, email }]);

  if (error) {
    console.error('Waitlist submission error:', error);
    return { error: 'Something went wrong. Please try again.' };
  }

  revalidatePath('/waitlist');
  return { success: true };
}
