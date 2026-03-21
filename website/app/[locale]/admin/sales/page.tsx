import { createAdminClient } from '@/lib/supabase/server';

export default async function AdminSalesPage() {
  const supabase = await createAdminClient();

  // Fetch wallet purchases
  const { data: purchases } = await supabase
    .from('wallet_ledger')
    .select('id, user_id, amount_micro, created_at, metadata')
    .eq('type', 'PURCHASE')
    .order('created_at', { ascending: false })
    .limit(100);

  const rows = purchases ?? [];
  const totalRevenueMicro = rows.reduce((sum, r) => sum + (r.amount_micro ?? 0), 0);
  const totalRevenue = (totalRevenueMicro / 1_000_000).toFixed(2);
  const avgOrder =
    rows.length > 0 ? (totalRevenueMicro / rows.length / 1_000_000).toFixed(2) : '0.00';

  const hasLsKey = !!process.env.LEMON_SQUEEZY_API_KEY;

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-bold">Sales</h1>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <SummaryCard label="Total Revenue" value={`$${totalRevenue}`} />
        <SummaryCard label="Total Purchases" value={rows.length.toLocaleString()} />
        <SummaryCard label="Avg Order" value={`$${avgOrder}`} />
      </div>

      {/* LemonSqueezy Status */}
      {!hasLsKey && (
        <div className="rounded-lg border border-yellow-500/30 bg-yellow-500/10 px-4 py-3 text-sm text-yellow-200">
          LemonSqueezy API key not configured. Add <code className="font-mono">LEMON_SQUEEZY_API_KEY</code> to
          environment variables to see external order data.
        </div>
      )}

      {/* Wallet Purchases Table */}
      <div>
        <h2 className="text-lg font-semibold mb-3">Wallet Purchases</h2>
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Date</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Amount</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Gateway</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">User ID</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {rows.map((row) => {
                const meta = (row.metadata ?? {}) as Record<string, unknown>;
                return (
                  <tr key={row.id} className="hover:bg-white/5">
                    <td className="px-4 py-3 text-gray-300">
                      {new Date(row.created_at).toLocaleDateString('en-US', {
                        month: 'short',
                        day: 'numeric',
                        year: 'numeric',
                      })}
                    </td>
                    <td className="px-4 py-3 text-green-400 font-mono">
                      +${(row.amount_micro / 1_000_000).toFixed(2)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="inline-block px-2 py-0.5 rounded text-xs bg-white/10">
                        {String(meta.gateway ?? 'unknown')}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500">
                      {row.user_id?.slice(0, 8)}...
                    </td>
                  </tr>
                );
              })}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-gray-500">
                    No purchases yet
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

function SummaryCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl border border-white/10 bg-white/5 p-5">
      <p className="text-sm text-gray-400 mb-1">{label}</p>
      <p className="text-2xl font-bold">{value}</p>
    </div>
  );
}
