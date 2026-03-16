'use server';

import { createClient } from '@/lib/supabase/server';
import { revalidatePath } from 'next/cache';

export async function submitWaitlist(formData: FormData) {
  const name = formData.get('name') as string;
  const email = formData.get('email') as string;

  if (!name || !email) {
    return { error: 'Name and email are required.' };
  }

  const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  if (!emailRegex.test(email)) {
    return { error: 'Please enter a valid email address.' };
  }

  const supabase = await createClient();

  const { data, error } = await supabase
    .from('waiting_list')
    .insert([{ name, email }])
    .select('id, name')
    .single();

  if (error) {
    if (error.code === '23505') {
      // If they already exist, try to get their ID for the viral card, but don't fail if we can't
      try {
        const { data: existing } = await supabase
          .from('waiting_list')
          .select('id, name')
          .eq('email', email)
          .maybeSingle();
          
        return { 
          success: true, 
          id: existing?.id,
          name: existing?.name,
          message: "You're already on the list! Spread the word to get priority access." 
        };
      } catch (e) {
        return { success: true, message: "You're already on the list! We'll stay in touch." };
      }
    }
    
    console.error('Waitlist submission error:', error);
    return { error: 'Something went wrong. Please try again.' };
  }

  revalidatePath('/waitlist');
  return { success: true, id: data.id, name: data.name };
}

export async function sendWaitlistInvite(senderId: string, senderName: string, recipientEmail: string) {
  if (!senderId || !recipientEmail) {
    return { error: 'Missing information.' };
  }

  const supabase = await createClient();

  // 1. Check invite count
  const { count, error: countError } = await supabase
    .from('waitlist_invites')
    .select('*', { count: 'exact', head: true })
    .eq('sender_id', senderId);

  if (countError) return { error: 'Database error. Please try again.' };
  if (count && count >= 5) return { error: 'You have used all 5 of your priority passes.' };

  // 2. record the invite
  const { error: insertError } = await supabase
    .from('waitlist_invites')
    .insert([{ sender_id: senderId, recipient_email: recipientEmail }]);

  if (insertError) {
    if (insertError.code === '23505') return { error: 'You already invited this person!' };
    return { error: 'Invitation failed. Please try again.' };
  }

  // 3. Trigger Edge Function (fire and forget or wait for success)
  try {
    const functionUrl = `${process.env.NEXT_PUBLIC_SUPABASE_URL}/functions/v1/waitlist-invite`;
    await fetch(functionUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY}`,
      },
      body: JSON.stringify({ senderName, recipientEmail }),
    });
  } catch (e) {
    console.error('Email trigger failed:', e);
    // We don't block the UI here as the database record is saved
  }

  return { success: true };
}
