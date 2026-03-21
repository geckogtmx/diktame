import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';

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

    const { data: profile } = await supabase
      .from('profiles')
      .select('name, custom_gemini_key, license_tier, avatar_url, created_at, updated_at')
      .eq('id', user.id)
      .single();

    return NextResponse.json({
      id: user.id,
      email: user.email,
      name: profile?.name ?? '',
      avatarUrl: profile?.avatar_url ?? null,
      createdAt: profile?.created_at ?? user.created_at,
      hasCustomGeminiKey: !!profile?.custom_gemini_key,
      licenseTier: profile?.license_tier ?? 'free',
    });
  } catch (error) {
    console.error('Account GET error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
