export const dynamic = 'force-dynamic';
import { createClient } from '@/lib/supabase/server';
import { redirect } from 'next/navigation';
import { setRequestLocale } from 'next-intl/server';

/** Format microdollars as credits (1 credit = 1,000 microdollars) */
function formatCredits(micro: number): string {
  const credits = Math.round(micro / 1000);
  return credits.toLocaleString() + ' credits';
}

function formatCreditsCompact(credits: number): string {
  const sign = credits >= 0 ? '+' : '';
  return `${sign}${credits.toLocaleString()} cr`;
}

function balanceColor(credits: number): string {
  if (credits >= 1000) return 'text-green-400';
  if (credits >= 500) return 'text-yellow-400';
  return 'text-red-400';
}

function amountColor(credits: number): string {
  return credits >= 0 ? 'text-green-400' : 'text-red-400';
}

/** Badge style for row type */
function typeBadge(type: string): string {
  switch (type) {
    case 'GRANT':
      return 'bg-green-500/10 text-green-400 border-green-500/20';
    case 'PURCHASE':
      return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
    case 'DAILY':
      return 'bg-orange-500/10 text-orange-400 border-orange-500/20';
    case 'REFUND':
      return 'bg-purple-500/10 text-purple-400 border-purple-500/20';
    case 'EXPIRY':
      return 'bg-gray-500/10 text-gray-400 border-gray-500/20';
    default:
      return 'bg-gray-500/10 text-gray-400 border-gray-500/20';
  }
}

function typeLabel(type: string): string {
  switch (type) {
    case 'DAILY':
      return 'Usage';
    case 'GRANT':
      return 'Grant';
    case 'PURCHASE':
      return 'Purchase';
    case 'REFUND':
      return 'Refund';
    case 'EXPIRY':
      return 'Expired';
    default:
      return type;
  }
}

interface DailySummary {
  day_date: string;
  day_label: string;
  type: string;
  credits: number;
  balance_credits: number;
  request_count: number;
  is_aggregate: boolean;
}

export default async function WalletPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);

  const supabase = await createClient();
  const {
    data: { user },
    error,
  } = await supabase.auth.getUser();

  if (error || !user) {
    redirect(`/${locale}/login`);
  }

  // Single query: daily aggregated summary via RPC
  const { data: summaries } = await supabase.rpc('get_wallet_daily_summary', {
    p_user_id: user.id,
    p_max_days: 60,
  });

  const rows = (summaries ?? []) as DailySummary[];

  // Current balance from the most recent row, or fetch separately if empty
  let balanceCredits = 0;
  if (rows.length > 0) {
    balanceCredits = rows[0].balance_credits;
  } else {
    const { data: balanceRow } = await supabase
      .from('wallet_ledger')
      .select('balance_after_micro')
      .eq('user_id', user.id)
      .order('created_at', { ascending: false })
      .limit(1)
      .single();
    balanceCredits = Math.round((balanceRow?.balance_after_micro ?? 0) / 1000);
  }

  return (
    <div className="px-6 py-8 max-w-4xl">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Wallet</h1>
        <p className="text-gray-400 text-sm mt-1">
          Credit balance and usage history
        </p>
      </div>

      {/* Balance display */}
      <div className="bg-white/5 rounded-xl p-6 border border-white/10 mb-6">
        <p className="text-sm text-gray-400 mb-1">Current Balance</p>
        <p className={`text-4xl font-bold ${balanceColor(balanceCredits)}`}>
          {balanceCredits.toLocaleString()} credits
        </p>
      </div>

      {/* Daily summary table */}
      {rows.length === 0 ? (
        <div className="bg-white/5 rounded-xl p-8 border border-white/10 text-center">
          <p className="text-gray-400">No activity yet</p>
          <p className="text-gray-500 text-sm mt-1">
            Your credit usage will appear here
          </p>
        </div>
      ) : (
        <div className="bg-white/5 rounded-xl border border-white/10 overflow-hidden">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-white/10 text-gray-400 text-left">
                <th className="px-4 py-3 font-medium">Date</th>
                <th className="px-4 py-3 font-medium">Type</th>
                <th className="px-4 py-3 font-medium text-right">Credits</th>
                <th className="px-4 py-3 font-medium text-right">Balance</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row, i) => (
                <tr
                  key={`${row.day_date}-${row.type}-${i}`}
                  className="border-b border-white/5 hover:bg-white/5 transition-colors"
                >
                  <td className="px-4 py-3 text-gray-300">
                    {row.day_label}
                    {row.is_aggregate && row.request_count > 0 && (
                      <span className="text-gray-500 ml-2 text-xs">
                        {row.request_count} request
                        {row.request_count !== 1 ? 's' : ''}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-block px-2 py-0.5 rounded text-xs font-medium border ${typeBadge(row.type)}`}
                    >
                      {typeLabel(row.type)}
                    </span>
                  </td>
                  <td
                    className={`px-4 py-3 text-right font-mono ${amountColor(row.credits)}`}
                  >
                    {formatCreditsCompact(row.credits)}
                  </td>
                  <td className="px-4 py-3 text-right font-mono text-gray-400">
                    {row.balance_credits.toLocaleString()} cr
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
