import { createAdminClient } from '@/lib/supabase/server';

interface SearchParams {
  search?: string;
  page?: string;
}

export default async function AdminUsersPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const params = await searchParams;
  const search = params.search ?? '';
  const page = Math.max(1, parseInt(params.page ?? '1', 10));
  const limit = 20;
  const offset = (page - 1) * limit;

  const supabase = await createAdminClient();

  // Build query
  let query = supabase
    .from('profiles')
    .select('id, email, name, is_admin, created_at', { count: 'exact' });

  if (search) {
    query = query.ilike('email', `%${search}%`);
  }

  const { data: users, count } = await query
    .order('created_at', { ascending: false })
    .range(offset, offset + limit - 1);

  const totalPages = Math.ceil((count ?? 0) / limit);

  // Fetch wallet balances
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

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Users</h1>

      {/* Search */}
      <form className="flex gap-3">
        <input
          name="search"
          type="text"
          defaultValue={search}
          placeholder="Search by email..."
          className="flex-1 max-w-md px-4 py-2 rounded-lg bg-white/5 border border-white/10 text-white placeholder-gray-500 text-sm focus:outline-none focus:border-white/30"
        />
        <button
          type="submit"
          className="px-4 py-2 rounded-lg bg-white/10 text-sm font-medium hover:bg-white/20 transition-colors"
        >
          Search
        </button>
      </form>

      {/* Count */}
      <p className="text-sm text-gray-400">
        {count?.toLocaleString() ?? 0} user{count !== 1 ? 's' : ''} total
        {search && ` matching "${search}"`}
      </p>

      {/* Users Table */}
      <div className="overflow-x-auto rounded-lg border border-white/10">
        <table className="w-full text-sm">
          <thead className="bg-white/5">
            <tr>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Email</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Name</th>
              <th className="text-right px-4 py-3 font-medium text-gray-400">Balance</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Signed Up</th>
              <th className="text-left px-4 py-3 font-medium text-gray-400">Role</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-white/5">
            {usersWithBalance.map((user) => (
              <tr key={user.id} className="hover:bg-white/5">
                <td className="px-4 py-3 font-mono text-xs">{user.email}</td>
                <td className="px-4 py-3">{user.name || '—'}</td>
                <td className="px-4 py-3 text-right font-mono">
                  <span
                    className={
                      user.walletBalanceMicro >= 1_000_000
                        ? 'text-green-400'
                        : user.walletBalanceMicro >= 500_000
                          ? 'text-yellow-400'
                          : 'text-red-400'
                    }
                  >
                    ${(user.walletBalanceMicro / 1_000_000).toFixed(2)}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-400">
                  {new Date(user.created_at).toLocaleDateString('en-US', {
                    month: 'short',
                    day: 'numeric',
                    year: 'numeric',
                  })}
                </td>
                <td className="px-4 py-3">
                  {user.is_admin && (
                    <span className="inline-block px-2 py-0.5 rounded text-xs bg-purple-500/20 text-purple-300">
                      Admin
                    </span>
                  )}
                </td>
              </tr>
            ))}
            {usersWithBalance.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-gray-500">
                  {search ? 'No users match your search' : 'No users yet'}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center gap-2 justify-center">
          {page > 1 && (
            <a
              href={`?${new URLSearchParams({ ...(search ? { search } : {}), page: String(page - 1) })}`}
              className="px-3 py-1.5 rounded bg-white/10 text-sm hover:bg-white/20 transition-colors"
            >
              Previous
            </a>
          )}
          <span className="text-sm text-gray-400">
            Page {page} of {totalPages}
          </span>
          {page < totalPages && (
            <a
              href={`?${new URLSearchParams({ ...(search ? { search } : {}), page: String(page + 1) })}`}
              className="px-3 py-1.5 rounded bg-white/10 text-sm hover:bg-white/20 transition-colors"
            >
              Next
            </a>
          )}
        </div>
      )}
    </div>
  );
}
