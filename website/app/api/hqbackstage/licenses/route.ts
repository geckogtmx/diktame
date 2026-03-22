import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    const url = new URL(request.url);
    const status = url.searchParams.get('status');

    let query = supabase
      .from('licenses')
      .select('id, user_id, key, status, tier, machine_id, order_ref, created_at, expires_at');

    if (status) {
      query = query.eq('status', status);
    }

    const { data: licenses, error } = await query
      .order('created_at', { ascending: false })
      .limit(200);

    if (error) {
      return NextResponse.json({ error: 'Failed to fetch licenses' }, { status: 500 });
    }

    // Resolve emails for each user_id
    const userIds = [...new Set((licenses ?? []).map((l) => l.user_id).filter(Boolean))];
    const { data: profiles } = userIds.length > 0
      ? await supabase.from('profiles').select('id, email').in('id', userIds)
      : { data: [] };

    const emailMap = new Map((profiles ?? []).map((p) => [p.id, p.email]));

    const enriched = (licenses ?? []).map((l) => ({
      ...l,
      email: emailMap.get(l.user_id) ?? null,
    }));

    // Fetch pending gifts
    const { data: pendingGifts } = await supabase
      .from('pending_gifts')
      .select('id, email, license_key, tier, wallet_credit_micro, created_at, claimed_at')
      .order('created_at', { ascending: false })
      .limit(50);

    return NextResponse.json({
      licenses: enriched,
      pendingGifts: pendingGifts ?? [],
    });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Admin licenses error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    const { id } = await request.json();
    if (!id) {
      return NextResponse.json({ error: 'Missing license id' }, { status: 400 });
    }

    const { error } = await supabase
      .from('licenses')
      .update({ status: 'revoked' })
      .eq('id', id);

    if (error) {
      return NextResponse.json({ error: 'Failed to revoke license' }, { status: 500 });
    }

    return NextResponse.json({ success: true });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Admin license revoke error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
