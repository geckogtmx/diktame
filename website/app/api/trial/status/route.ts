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
      .select('trial_words_quota, trial_expires_at, custom_gemini_key')
      .eq('id', user.id)
      .single();

    if (profileError || !profile) {
      return NextResponse.json({ error: 'Profile not found' }, { status: 404 });
    }

    // Calculate words used from api_usage table
    const { data: usageData, error: usageError } = await supabase
      .from('api_usage')
      .select('words_used')
      .eq('user_id', user.id);

    if (usageError) {
      console.error('Error fetching usage data:', usageError);
      return NextResponse.json({ error: 'Failed to fetch usage data' }, { status: 500 });
    }

    // Sum up total words used
    const wordsUsed = usageData?.reduce((sum, record) => sum + (record.words_used || 0), 0) || 0;

    // Calculate days remaining
    const expiresAt = profile.trial_expires_at ? new Date(profile.trial_expires_at) : null;
    const now = new Date();
    const daysRemaining = expiresAt
      ? Math.max(0, Math.ceil((expiresAt.getTime() - now.getTime()) / (1000 * 60 * 60 * 24)))
      : 0;

    // Check if trial is active
    const hasCustomKey = !!profile.custom_gemini_key;
    const trialActive = !hasCustomKey && wordsUsed < profile.trial_words_quota && daysRemaining > 0;

    return NextResponse.json({
      wordsUsed,
      wordsQuota: profile.trial_words_quota,
      daysRemaining,
      expiresAt: profile.trial_expires_at,
      trialActive,
      hasCustomKey,
    });
  } catch (error) {
    console.error('Trial status error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
