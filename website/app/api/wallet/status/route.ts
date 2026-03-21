// Wallet balance endpoint — returns current balance in microdollars.
// Supports both Bearer token (C# app) and cookie auth (web dashboard).
// Backup for the wallet-status Edge Function.

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

    const { data: row } = await supabase
      .from('wallet_ledger')
      .select('balance_after_micro')
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .limit(1)
      .single();

    return NextResponse.json({
      balance_micro: row?.balance_after_micro ?? 0,
    });
  } catch (error) {
    console.error('Wallet status error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
