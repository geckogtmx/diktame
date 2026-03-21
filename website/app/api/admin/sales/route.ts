import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    // Fetch wallet purchases (all PURCHASE type transactions)
    const { data: purchases } = await supabase
      .from('wallet_ledger')
      .select('id, user_id, amount_micro, created_at, metadata')
      .eq('type', 'PURCHASE')
      .order('created_at', { ascending: false })
      .limit(100);

    const rows = purchases ?? [];
    const totalRevenueMicro = rows.reduce((sum, r) => sum + (r.amount_micro ?? 0), 0);

    // Try to fetch LemonSqueezy orders if API key is configured
    let lemonSqueezyOrders: unknown[] = [];
    const lsApiKey = process.env.LEMON_SQUEEZY_API_KEY;
    if (lsApiKey) {
      try {
        const res = await fetch('https://api.lemonsqueezy.com/v1/orders?page[size]=50', {
          headers: {
            Authorization: `Bearer ${lsApiKey}`,
            Accept: 'application/vnd.api+json',
          },
          next: { revalidate: 300 },
        });
        if (res.ok) {
          const json = await res.json();
          lemonSqueezyOrders = json.data ?? [];
        }
      } catch (err) {
        console.error('Failed to fetch LemonSqueezy orders:', err);
      }
    }

    return NextResponse.json({
      walletPurchases: rows,
      totalRevenueMicro,
      totalRevenueUsd: (totalRevenueMicro / 1_000_000).toFixed(2),
      purchaseCount: rows.length,
      lemonSqueezy: {
        orders: lemonSqueezyOrders,
        configured: !!lsApiKey,
      },
    });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Admin sales error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
