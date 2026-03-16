'use server';

import { createClient, createAdminClient } from '@/lib/supabase/server';
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

  const supabase = await createAdminClient();

  const { data, error } = await supabase
    .from('waiting_list')
    .insert([{ name, email }])
    .select('id, name')
    .single();

  if (error) {
    if (error.code === '23505') {
      console.log(`Duplicate waitlist signup attempt: ${email}`);
      // Try to get user data for the viral card, but handle RLS failures gracefully
      const { data: existing, error: selectError } = await supabase
          .from('waiting_list')
          .select('id, name')
          .eq('email', email)
          .maybeSingle();
      
      if (selectError) {
        console.warn('RLS blocked existing user lookup, returning generic success.');
      }

      return { 
        success: true, 
        id: existing?.id,
        name: existing?.name,
        message: "You're already on the list! Spread the word to get priority access." 
      };
    }
    
    console.error('Waitlist submission error:', error, error.code, error.message);
    return { error: `Submission failed: ${error.message || 'Unknown error'}` };
  }

  revalidatePath('/waitlist');
  return { success: true, id: data.id, name: data.name };
}

export async function sendWaitlistInvite(senderId: string, senderName: string, recipientEmail: string) {
  if (!senderId || !recipientEmail) {
    return { error: 'Missing information.' };
  }

  const supabase = await createAdminClient();

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
    const response = await fetch(functionUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY}`,
      },
      body: JSON.stringify({ senderName, recipientEmail }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      console.error('Edge Function returned error:', response.status, errorData);
    }
  } catch (e) {
    console.error('Email trigger connection failed:', e);
  }
  return { success: true };
}

export async function getWaitlistInvites(senderId: string) {
  if (!senderId) return { invites: [] };

  const supabase = await createAdminClient();
  const { data, error } = await supabase
    .from('waitlist_invites')
    .select('recipient_email')
    .eq('sender_id', senderId);

  if (error) {
    console.error('Error fetching invites:', error);
    return { invites: [] };
  }

  return { invites: data.map((i: any) => i.recipient_email) };
}
