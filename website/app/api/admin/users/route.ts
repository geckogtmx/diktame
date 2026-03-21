import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    const url = new URL(request.url);
    const search = url.searchParams.get('search') ?? '';
    const page = Math.max(1, parseInt(url.searchParams.get('page') ?? '1', 10));
    const limit = Math.min(100, parseInt(url.searchParams.get('limit') ?? '20', 10));
    const offset = (page - 1) * limit;

    // Build query
    let query = supabase
      .from('profiles')
      .select('id, email, name, is_admin, created_at', { count: 'exact' });

    if (search) {
      query = query.ilike('email', `%${search}%`);
    }

    const { data: users, count, error } = await query
      .order('created_at', { ascending: false })
      .range(offset, offset + limit - 1);

    if (error) {
      return NextResponse.json({ error: 'Failed to fetch users' }, { status: 500 });
    }

    // Fetch latest wallet balance for each user
    const usersWithBalance = await Promise.all(
      (users ?? []).map(async (user) => {
        const { data: row } = await supabase
          .from('wallet_ledger')
          .select('balance_after_micro')
          .eq('user_id', user.id)
          .order('created_at', { ascending: false })
          .limit(1)
          .maybeSingle();

        return {
          ...user,
          walletBalanceMicro: row?.balance_after_micro ?? 0,
        };
      }),
    );

    return NextResponse.json({
      users: usersWithBalance,
      total: count ?? 0,
      page,
      limit,
    });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Admin users error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
