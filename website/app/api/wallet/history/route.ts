// Wallet transaction history — returns recent ledger entries with pagination.
// Supports both Bearer token (C# app) and cookie auth (web dashboard).

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

    const url = new URL(request.url);
    const limit = Math.min(
      Math.max(parseInt(url.searchParams.get('limit') ?? '50', 10) || 50, 1),
      100
    );
    const offset = Math.max(
      parseInt(url.searchParams.get('offset') ?? '0', 10) || 0,
      0
    );

    const { data: transactions, error } = await supabase
      .from('wallet_ledger')
      .select(
        'id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata'
      )
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .range(offset, offset + limit - 1);

    if (error) {
      console.error('Wallet history query error:', error);
      return NextResponse.json(
        { error: 'Failed to fetch transactions' },
        { status: 500 }
      );
    }

    return NextResponse.json({
      transactions: transactions ?? [],
      limit,
      offset,
    });
  } catch (error) {
    console.error('Wallet history error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
