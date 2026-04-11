export const dynamic = 'force-dynamic';
import { createClient } from '@/lib/supabase/server';
import { redirect } from 'next/navigation';
import { setRequestLocale, getTranslations } from 'next-intl/server';
import { Link } from '@/i18n/navigation';

/** Format microdollars as credits (1 credit = 1,000 microdollars) */
function formatCredits(micro: number): string {
  return Math.round(micro / 1000).toLocaleString();
}

/** Color class for credit balance */
function balanceColor(micro: number): string {
  const credits = micro / 1000;
  if (credits >= 1000) return 'text-green-400';
  if (credits >= 500) return 'text-yellow-400';
  return 'text-red-400';
}

export default async function DashboardPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations('DashboardPage');
  const supabase = await createClient();

  const {
    data: { user },
    error,
  } = await supabase.auth.getUser();

  if (error || !user) {
    redirect(`/${locale}/login`);
  }

  // Get user profile
  const { data: profile } = await supabase
    .from('profiles')
    .select('name')
    .eq('id', user.id)
    .single();

  // Get wallet balance
  const { data: walletRow } = await supabase
    .from('wallet_ledger')
    .select('balance_after_micro')
    .eq('user_id', user.id)
    .order('created_at', { ascending: false })
    .limit(1)
    .single();
  const balanceMicro = walletRow?.balance_after_micro ?? 0;

  // Get transaction count + last transaction date
  const { count: txCount } = await supabase
    .from('wallet_ledger')
    .select('id', { count: 'exact', head: true })
    .eq('user_id', user.id);

  const { data: lastTx } = await supabase
    .from('wallet_ledger')
    .select('created_at')
    .eq('user_id', user.id)
    .order('created_at', { ascending: false })
    .limit(1)
    .single();

  // Try to get active license (table may not exist yet — handle gracefully)
  let licenseTier = 'Free';
  let licenseExpiry: string | null = null;
  try {
    const { data: license } = await supabase
      .from('licenses')
      .select('tier, expires_at')
      .eq('user_id', user.id)
      .eq('status', 'active')
      .limit(1)
      .single();
    if (license) {
      licenseTier = license.tier.charAt(0).toUpperCase() + license.tier.slice(1);
      licenseExpiry = license.expires_at;
    }
  } catch {
    // licenses table doesn't exist yet — show Free
  }

  const displayName =
    user.user_metadata?.full_name || profile?.name || t('welcomeFallback');

  return (
    <div className="px-6 py-8 max-w-4xl">
      {/* Header */}
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-2xl font-bold">
            {t('welcomePrefix')}
            {displayName}
          </h1>
          <p className="text-gray-400 mt-1 text-sm">{user.email}</p>
        </div>
        <form action="/auth/signout" method="post">
          <button className="px-4 py-2 text-sm bg-white/5 hover:bg-white/10 border border-white/10 rounded-lg transition-colors">
            {t('signOut')}
          </button>
        </form>
      </div>

      {/* Cards grid */}
      <div className="grid md:grid-cols-3 gap-5 mb-8">
        {/* Wallet Balance Card */}
        <div className="bg-white/5 backdrop-blur-xl rounded-xl p-5 border border-white/10">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-medium text-gray-400">
              {t('walletTitle')}
            </h3>
            <svg
              className="w-5 h-5 text-gray-500"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1.5}
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M21 12a2.25 2.25 0 0 0-2.25-2.25H15a3 3 0 1 1-6 0H5.25A2.25 2.25 0 0 0 3 12m18 0v6a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 18v-6m18 0V9M3 12V9m18 0a2.25 2.25 0 0 0-2.25-2.25H5.25A2.25 2.25 0 0 0 3 9m18 0V6a2.25 2.25 0 0 0-2.25-2.25H5.25A2.25 2.25 0 0 0 3 6v3"
              />
            </svg>
          </div>
          <p className={`text-3xl font-bold mb-3 ${balanceColor(balanceMicro)}`}>
            {formatCredits(balanceMicro)}
          </p>
          <div className="flex gap-2">
            <Link
              href="/dashboard/wallet"
              className="text-xs text-blue-400 hover:text-blue-300 transition-colors"
            >
              {t('viewHistory')}
            </Link>
          </div>
        </div>

        {/* License Status Card */}
        <div className="bg-white/5 backdrop-blur-xl rounded-xl p-5 border border-white/10">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-medium text-gray-400">
              {t('licenseTitle')}
            </h3>
            <svg
              className="w-5 h-5 text-gray-500"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1.5}
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M9 12.75 11.25 15 15 9.75m-3-7.036A11.959 11.959 0 0 1 3.598 6 11.99 11.99 0 0 0 3 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285Z"
              />
            </svg>
          </div>
          <p className="text-2xl font-bold mb-1">{licenseTier}</p>
          {licenseExpiry ? (
            <p className="text-xs text-gray-400">
              Expires {new Date(licenseExpiry).toLocaleDateString()}
            </p>
          ) : (
            <p className="text-xs text-gray-400">
              {licenseTier === 'Free' ? t('upgradeHint') : 'Perpetual license'}
            </p>
          )}
        </div>

        {/* Recent Activity Card */}
        <div className="bg-white/5 backdrop-blur-xl rounded-xl p-5 border border-white/10">
          <div className="flex items-center justify-between mb-3">
            <h3 className="text-sm font-medium text-gray-400">
              {t('activityTitle')}
            </h3>
            <svg
              className="w-5 h-5 text-gray-500"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1.5}
              stroke="currentColor"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
              />
            </svg>
          </div>
          <p className="text-2xl font-bold mb-1">{txCount ?? 0}</p>
          <p className="text-xs text-gray-400">
            {t('transactionsLabel')}
          </p>
          {lastTx && (
            <p className="text-xs text-gray-500 mt-2">
              {t('lastActivity')}{' '}
              {new Date(lastTx.created_at).toLocaleDateString()}
            </p>
          )}
        </div>
      </div>

      {/* Quick Actions */}
      <h3 className="text-sm font-medium text-gray-400 mb-3">
        {t('quickActions')}
      </h3>
      <div className="grid md:grid-cols-3 gap-4">
        <Link
          href="/waitlist"
          className="bg-white/5 rounded-xl p-5 border border-white/10 hover:border-blue-500/50 transition-colors"
        >
          <h3 className="font-semibold text-sm mb-1">{t('getAppTitle')}</h3>
          <p className="text-xs text-gray-400">{t('getAppDesc')}</p>
        </Link>
        <Link
          href="/docs"
          className="bg-white/5 rounded-xl p-5 border border-white/10 hover:border-blue-500/50 transition-colors"
        >
          <h3 className="font-semibold text-sm mb-1">
            {t('documentationTitle')}
          </h3>
          <p className="text-xs text-gray-400">{t('documentationDesc')}</p>
        </Link>
        <Link
          href="/dashboard/profile"
          className="bg-white/5 rounded-xl p-5 border border-white/10 hover:border-blue-500/50 transition-colors"
        >
          <h3 className="font-semibold text-sm mb-1">{t('settingsTitle')}</h3>
          <p className="text-xs text-gray-400">{t('settingsDesc')}</p>
        </Link>
      </div>
    </div>
  );
}
