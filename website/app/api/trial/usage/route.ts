import { createClient } from '@/lib/supabase/server';
import { NextResponse } from 'next/server';

interface UsageRequest {
  provider: 'gemini' | 'anthropic' | 'openai' | 'deepseek';
  model: string;
  wordsUsed: number;
  tokensUsed?: number;
  cost?: number;
}

export async function POST(request: Request) {
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
    const body = (await request.json()) as UsageRequest;
    const { provider, model, wordsUsed, tokensUsed = 0, cost = 0 } = body;

    // Validate required fields
    if (!provider || !model || typeof wordsUsed !== 'number' || wordsUsed <= 0) {
      return NextResponse.json({ error: 'Invalid request body' }, { status: 400 });
    }

    // Fetch the user's profile to check trial quota
    const { data: profile, error: profileError } = await supabase
      .from('profiles')
      .select('trial_words_quota, custom_gemini_key')
      .eq('id', user.id)
      .single();

    if (profileError || !profile) {
      return NextResponse.json({ error: 'Profile not found' }, { status: 404 });
    }

    // If user has custom key, skip quota check
    if (profile.custom_gemini_key) {
      // Still record usage but don't enforce quota
      const { error: insertError } = await supabase.from('api_usage').insert({
        user_id: user.id,
        provider,
        model,
        words_used: wordsUsed,
        tokens_used: tokensUsed,
        cost,
      });

      if (insertError) {
        console.error('Error recording usage:', insertError);
        return NextResponse.json({ error: 'Failed to record usage' }, { status: 500 });
      }

      return NextResponse.json({
        success: true,
        quotaRemaining: null,
        message: 'Using custom API key',
      });
    }

    // Calculate total words used so far
    const { data: usageData, error: usageError } = await supabase
      .from('api_usage')
      .select('words_used')
      .eq('user_id', user.id);

    if (usageError) {
      console.error('Error fetching usage data:', usageError);
      return NextResponse.json({ error: 'Failed to fetch usage data' }, { status: 500 });
    }

    const totalWordsUsed =
      usageData?.reduce((sum, record) => sum + (record.words_used || 0), 0) || 0;
    const newTotal = totalWordsUsed + wordsUsed;

    // Check if quota would be exceeded
    if (newTotal > profile.trial_words_quota) {
      return NextResponse.json(
        {
          error: 'Trial quota exceeded',
          wordsUsed: totalWordsUsed,
          wordsQuota: profile.trial_words_quota,
          quotaRemaining: Math.max(0, profile.trial_words_quota - totalWordsUsed),
        },
        { status: 403 }
      );
    }

    // Record the usage
    const { error: insertError } = await supabase.from('api_usage').insert({
      user_id: user.id,
      provider,
      model,
      words_used: wordsUsed,
      tokens_used: tokensUsed,
      cost,
    });

    if (insertError) {
      console.error('Error recording usage:', insertError);
      return NextResponse.json({ error: 'Failed to record usage' }, { status: 500 });
    }

    // Update budget tracker
    const { error: budgetError } = await supabase.rpc('update_budget', {
      p_user_id: user.id,
      p_cost: cost,
    });

    if (budgetError) {
      console.error('Error updating budget:', budgetError);
      // Don't fail the request, just log the error
    }

    return NextResponse.json({
      success: true,
      wordsUsed: newTotal,
      wordsQuota: profile.trial_words_quota,
      quotaRemaining: profile.trial_words_quota - newTotal,
    });
  } catch (error) {
    console.error('Trial usage error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
