import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';
import type { SupabaseClient } from '@supabase/supabase-js';

/** Query the latest wallet balance for a user. */
async function getWalletBalance(supabase: SupabaseClient, userId: string): Promise<number> {
  const { data: row } = await supabase
    .from('wallet_ledger')
    .select('balance_after_micro')
    .eq('user_id', userId)
    .order('created_at', { ascending: false })
    .limit(1)
    .maybeSingle();
  return row?.balance_after_micro ?? 0;
}

export async function GET(request: Request) {
  try {
    const supabase = await createApiClient(request);

    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const { data: profile, error: profileError } = await supabase
      .from('profiles')
      .select('*')
      .eq('id', user.id)
      .single();

    if (profileError || !profile) {
      return NextResponse.json({ error: 'Profile not found' }, { status: 404 });
    }

    const walletBalanceMicro = await getWalletBalance(supabase, user.id);

    return NextResponse.json({
      id: user.id,
      email: user.email,
      name: profile.name,
      avatarUrl: profile.avatar_url ?? null,
      walletBalanceMicro,
      hasCustomGeminiKey: !!profile.custom_gemini_key,
      createdAt: profile.created_at,
      updatedAt: profile.updated_at,
    });
  } catch (error) {
    console.error('Profile GET error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function PATCH(request: Request) {
  try {
    const supabase = await createApiClient(request);

    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const body = await request.json();
    const { name, customGeminiKey, avatarUrl } = body;

    const updates: Record<string, unknown> = {
      updated_at: new Date().toISOString(),
    };

    if (name !== undefined) {
      updates.name = name;
    }

    if (customGeminiKey !== undefined) {
      updates.custom_gemini_key = customGeminiKey === '' ? null : customGeminiKey;
    }

    if (avatarUrl !== undefined) {
      updates.avatar_url = avatarUrl === '' ? null : avatarUrl;
    }

    const { data: updatedProfile, error: updateError } = await supabase
      .from('profiles')
      .update(updates)
      .eq('id', user.id)
      .select()
      .single();

    if (updateError) {
      console.error('Profile PATCH error:', updateError);
      return NextResponse.json({ error: 'Failed to update profile' }, { status: 500 });
    }

    const walletBalanceMicro = await getWalletBalance(supabase, user.id);

    return NextResponse.json({
      id: user.id,
      email: user.email,
      name: updatedProfile.name,
      avatarUrl: updatedProfile.avatar_url ?? null,
      walletBalanceMicro,
      hasCustomGeminiKey: !!updatedProfile.custom_gemini_key,
      createdAt: updatedProfile.created_at,
      updatedAt: updatedProfile.updated_at,
    });
  } catch (error) {
    console.error('Profile PATCH error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const supabase = await createApiClient(request);

    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Clean up avatar from storage before deleting profile
    await supabase.storage.from('avatars').remove([`${user.id}.webp`]);

    const { error: deleteError } = await supabase.from('profiles').delete().eq('id', user.id);

    if (deleteError) {
      console.error('Profile DELETE error:', deleteError);
      return NextResponse.json({ error: 'Failed to delete profile' }, { status: 500 });
    }

    await supabase.auth.signOut();

    return NextResponse.json({ success: true, message: 'Account deleted successfully' });
  } catch (error) {
    console.error('Profile DELETE error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
