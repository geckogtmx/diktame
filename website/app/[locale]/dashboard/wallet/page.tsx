export const dynamic = 'force-dynamic';
import { createClient } from '@/lib/supabase/server';
import { redirect } from 'next/navigation';
import { setRequestLocale } from 'next-intl/server';
import { Link } from '@/i18n/navigation';

const PAGE_SIZE = 20;

/** Format microdollars as USD string */
function formatUSD(micro: number): string {
  const prefix = micro >= 0 ? '+' : '';
  return `${prefix}$${(Math.abs(micro) / 1_000_000).toFixed(2)}`;
}

function formatBalance(micro: number): string {
  return `$${(micro / 1_000_000).toFixed(2)}`;
}

function balanceColor(micro: number): string {
  if (micro >= 1_000_000) return 'text-green-400';
  if (micro >= 500_000) return 'text-yellow-400';
  return 'text-red-400';
}

function amountColor(micro: number): string {
  return micro >= 0 ? 'text-green-400' : 'text-red-400';
}

/** Badge style for transaction type */
function typeBadge(type: string): string {
  switch (type) {
    case 'GRANT':
      return 'bg-green-500/10 text-green-400 border-green-500/20';
    case 'PURCHASE':
      return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    case 'USAGE':
      return 'bg-orange-500/10 text-orange-400 border-orange-500/20';
    case 'REFUND':
      return 'bg-purple-500/10 text-purple-400 border-purple-500/20';
    case 'EXPIRY':
      return 'bg-gray-500/10 text-gray-400 border-gray-500/20';
    default:
      return 'bg-gray-500/10 text-gray-400 border-gray-500/20';
  }
}

interface Transaction {
  id: string;
  amount_micro: number;
  balance_after_micro: number;
  type: string;
  created_at: string;
  expires_at: string | null;
  metadata: Record<string, unknown>;
}

export default async function WalletPage({
  params,
  searchParams,
}: {
  params: Promise<{ locale: string }>;
  searchParams: Promise<{ page?: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const { page: pageStr } = await searchParams;

  const supabase = await createClient();
  const {
    data: { user },
    error,
  } = await supabase.auth.getUser();

  if (error || !user) {
    redirect('/login');
  }

  const page = Math.max(parseInt(pageStr ?? '1', 10) || 1, 1);
  const offset = (page - 1) * PAGE_SIZE;

  // Get current balance
  const { data: balanceRow } = await supabase
    .from('wallet_ledger')
    .select('balance_after_micro')
    .eq('user_id', user.id)
    .order('created_at', { ascending: false })
    .limit(1)
    .single();
  const balanceMicro = balanceRow?.balance_after_micro ?? 0;

  // Get total count for pagination
  const { count: totalCount } = await supabase
    .from('wallet_ledger')
    .select('id', { count: 'exact', head: true })
    .eq('user_id', user.id);

  // Get page of transactions
  const { data: transactions } = await supabase
    .from('wallet_ledger')
    .select(
      'id, amount_micro, balance_after_micro, type, created_at, expires_at, metadata'
    )
    .eq('user_id', user.id)
    .order('created_at', { ascending: false })
    .range(offset, offset + PAGE_SIZE - 1);

  const rows = (transactions ?? []) as Transaction[];
  const total = totalCount ?? 0;
  const totalPages = Math.max(Math.ceil(total / PAGE_SIZE), 1);

  return (
    <div className="px-6 py-8 max-w-4xl">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Wallet</h1>
        <p className="text-gray-400 text-sm mt-1">
          Transaction history and balance
        </p>
      </div>

      {/* Balance display */}
      <div className="bg-white/5 rounded-xl p-6 border border-white/10 mb-6">
        <p className="text-sm text-gray-400 mb-1">Current Balance</p>
        <p className={`text-4xl font-bold ${balanceColor(balanceMicro)}`}>
          {formatBalance(balanceMicro)}
        </p>
        <p className="text-xs text-gray-500 mt-2">
          {total} total transaction{total !== 1 ? 's' : ''}
        </p>
      </div>

      {/* Transactions table */}
      {rows.length === 0 ? (
        <div className="bg-white/5 rounded-xl p-8 border border-white/10 text-center">
          <p className="text-gray-400">No transactions yet</p>
          <p className="text-gray-500 text-sm mt-1">
            Your wallet activity will appear here
          </p>
        </div>
      ) : (
        <div className="bg-white/5 rounded-xl border border-white/10 overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-white/10 text-gray-400 text-left">
                <th className="px-4 py-3 font-medium">Date</th>
                <th className="px-4 py-3 font-medium">Type</th>
                <th className="px-4 py-3 font-medium text-right">Amount</th>
                <th className="px-4 py-3 font-medium text-right">Balance</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((tx) => (
                <tr
                  key={tx.id}
                  className="border-b border-white/5 hover:bg-white/5 transition-colors"
                >
                  <td className="px-4 py-3 text-gray-300">
                    {new Date(tx.created_at).toLocaleDateString(undefined, {
                      month: 'short',
                      day: 'numeric',
                      year: 'numeric',
                    })}
                    <span className="text-gray-500 ml-2 text-xs">
                      {new Date(tx.created_at).toLocaleTimeString(undefined, {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-block px-2 py-0.5 rounded text-xs font-medium border ${typeBadge(tx.type)}`}
                    >
                      {tx.type}
                    </span>
                  </td>
                  <td
                    className={`px-4 py-3 text-right font-mono ${amountColor(tx.amount_micro)}`}
                  >
                    {formatUSD(tx.amount_micro)}
                  </td>
                  <td className="px-4 py-3 text-right font-mono text-gray-400">
                    {formatBalance(tx.balance_after_micro)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between px-4 py-3 border-t border-white/10">
              <p className="text-xs text-gray-400">
                Page {page} of {totalPages}
              </p>
              <div className="flex gap-2">
                {page > 1 && (
                  <Link
                    href={`/dashboard/wallet?page=${page - 1}`}
                    className="px-3 py-1.5 text-xs bg-white/5 hover:bg-white/10 border border-white/10 rounded transition-colors"
                  >
                    Previous
                  </Link>
                )}
                {page < totalPages && (
                  <Link
                    href={`/dashboard/wallet?page=${page + 1}`}
                    className="px-3 py-1.5 text-xs bg-white/5 hover:bg-white/10 border border-white/10 rounded transition-colors"
                  >
                    Next
                  </Link>
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
