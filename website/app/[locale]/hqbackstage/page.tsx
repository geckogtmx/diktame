import { createAdminClient } from '@/lib/supabase/server';

export default async function AdminOverviewPage() {
  const supabase = await createAdminClient();

  // Fetch KPI data in parallel
  const [usersResult, revenueResult, licensesResult, recentSignups] = await Promise.all([
    supabase.from('profiles').select('id', { count: 'exact', head: true }),
    supabase.from('wallet_ledger').select('amount_micro').eq('type', 'PURCHASE'),
    supabase.from('licenses').select('id', { count: 'exact', head: true }).eq('status', 'active'),
    supabase
      .from('profiles')
      .select('id, email, name, created_at')
      .order('created_at', { ascending: false })
      .limit(10),
  ]);

  const totalUsers = usersResult.count ?? 0;

  const totalRevenueMicro = (revenueResult.data ?? []).reduce(
    (sum, row) => sum + (row.amount_micro ?? 0),
    0,
  );
  const totalRevenue = (totalRevenueMicro / 1_000_000).toFixed(2);

  // licenses table might not exist yet — handle gracefully
  const activeLicenses = licensesResult.error ? 0 : (licensesResult.count ?? 0);

  const signups = recentSignups.data ?? [];

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-bold">Admin Overview</h1>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard label="Total Users" value={totalUsers.toLocaleString()} />
        <KpiCard label="Total Revenue" value={`$${totalRevenue}`} />
        <KpiCard label="Active Licenses" value={activeLicenses.toLocaleString()} />
        <KpiCard
          label="Outstanding Credits"
          value="—"
          subtitle="Complex aggregate — see Supabase"
        />
      </div>

      {/* Recent Signups */}
      <div>
        <h2 className="text-lg font-semibold mb-3">Recent Signups</h2>
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Email</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Name</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Signed Up</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {signups.map((user) => (
                <tr key={user.id} className="hover:bg-white/5">
                  <td className="px-4 py-3 font-mono text-xs">{user.email}</td>
                  <td className="px-4 py-3">{user.name || '—'}</td>
                  <td className="px-4 py-3 text-gray-400">
                    {new Date(user.created_at).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric',
                      year: 'numeric',
                    })}
                  </td>
                </tr>
              ))}
              {signups.length === 0 && (
                <tr>
                  <td colSpan={3} className="px-4 py-6 text-center text-gray-500">
                    No users yet
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

function KpiCard({
  label,
  value,
  subtitle,
}: {
  label: string;
  value: string;
  subtitle?: string;
}) {
  return (
    <div className="rounded-xl border border-white/10 bg-white/5 p-5">
      <p className="text-sm text-gray-400 mb-1">{label}</p>
      <p className="text-2xl font-bold">{value}</p>
      {subtitle && <p className="text-xs text-gray-500 mt-1">{subtitle}</p>}
    </div>
  );
}
