import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';

export async function GET() {
  try {
    const supabase = await createClient();

    // Get the authenticated user
    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Fetch the user's profile
    const { data: profile, error: profileError } = await supabase
      .from('profiles')
      .select('*')
      .eq('id', user.id)
      .single();

    if (profileError || !profile) {
      return NextResponse.json({ error: 'Profile not found' }, { status: 404 });
    }

    // Return profile with user info
    return NextResponse.json({
      id: user.id,
      email: user.email,
      name: profile.name,
      trialWordsQuota: profile.trial_words_quota,
      trialExpiresAt: profile.trial_expires_at,
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
    const supabase = await createClient();

    // Get the authenticated user
    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Parse the request body
    const body = await request.json();
    const { name, customGeminiKey } = body;

    // Build update object
    const updates: Record<string, unknown> = {
      updated_at: new Date().toISOString(),
    };

    if (name !== undefined) {
      updates.name = name;
    }

    if (customGeminiKey !== undefined) {
      // If customGeminiKey is empty string, set to null
      updates.custom_gemini_key = customGeminiKey === '' ? null : customGeminiKey;
    }

    // Update the profile
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

    return NextResponse.json({
      id: user.id,
      email: user.email,
      name: updatedProfile.name,
      trialWordsQuota: updatedProfile.trial_words_quota,
      trialExpiresAt: updatedProfile.trial_expires_at,
      hasCustomGeminiKey: !!updatedProfile.custom_gemini_key,
      createdAt: updatedProfile.created_at,
      updatedAt: updatedProfile.updated_at,
    });
  } catch (error) {
    console.error('Profile PATCH error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function DELETE() {
  try {
    const supabase = await createClient();

    // Get the authenticated user
    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Delete the user's profile (cascades to api_usage and budget_tracker via RLS)
    const { error: deleteError } = await supabase.from('profiles').delete().eq('id', user.id);

    if (deleteError) {
      console.error('Profile DELETE error:', deleteError);
      return NextResponse.json({ error: 'Failed to delete profile' }, { status: 500 });
    }

    // Sign out the user
    await supabase.auth.signOut();

    return NextResponse.json({ success: true, message: 'Account deleted successfully' });
  } catch (error) {
    console.error('Profile DELETE error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
