import { createAdminClient } from '@/lib/supabase/server';
import { LicenseGiftForm } from './LicenseGiftForm';

export default async function AdminLicensesPage() {
  const supabase = await createAdminClient();

  // Fetch licenses with user emails
  const { data: licenses } = await supabase
    .from('licenses')
    .select('id, user_id, key, status, tier, machine_id, order_ref, created_at, expires_at')
    .order('created_at', { ascending: false })
    .limit(200);

  const rows = licenses ?? [];

  // Resolve emails
  const userIds = [...new Set(rows.map((l) => l.user_id).filter(Boolean))];
  const { data: profiles } = userIds.length > 0
    ? await supabase.from('profiles').select('id, email').in('id', userIds)
    : { data: [] };
  const emailMap = new Map((profiles ?? []).map((p) => [p.id, p.email]));

  // Fetch pending gifts
  const { data: pendingGifts } = await supabase
    .from('pending_gifts')
    .select('id, email, license_key, tier, wallet_credit_micro, created_at, claimed_at')
    .order('created_at', { ascending: false })
    .limit(50);

  const gifts = pendingGifts ?? [];

  return (
    <div className="space-y-8">
      <h1 className="text-2xl font-bold">Licenses</h1>

      {/* Gift Form */}
      <LicenseGiftForm />

      {/* Licenses Table */}
      <div>
        <h2 className="text-lg font-semibold mb-3">
          Active Licenses ({rows.filter((l) => l.status === 'active').length})
        </h2>
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="w-full text-sm">
            <thead className="bg-white/5">
              <tr>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Key</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">User</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Tier</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Status</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Machine</th>
                <th className="text-left px-4 py-3 font-medium text-gray-400">Created</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-white/5">
              {rows.map((license) => (
                <tr key={license.id} className="hover:bg-white/5">
                  <td className="px-4 py-3 font-mono text-xs">{license.key}</td>
                  <td className="px-4 py-3 text-xs">
                    {emailMap.get(license.user_id) ?? license.user_id?.slice(0, 8) ?? '—'}
                  </td>
                  <td className="px-4 py-3">
                    <TierBadge tier={license.tier} />
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={license.status} />
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-500">
                    {license.machine_id?.slice(0, 12) ?? '—'}
                  </td>
                  <td className="px-4 py-3 text-gray-400">
                    {new Date(license.created_at).toLocaleDateString('en-US', {
                      month: 'short',
                      day: 'numeric',
                    })}
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-6 text-center text-gray-500">
                    No licenses yet
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pending Gifts */}
      {gifts.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-3">
            Pending Gifts ({gifts.filter((g) => !g.claimed_at).length} unclaimed)
          </h2>
          <div className="overflow-x-auto rounded-lg border border-white/10">
            <table className="w-full text-sm">
              <thead className="bg-white/5">
                <tr>
                  <th className="text-left px-4 py-3 font-medium text-gray-400">Email</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-400">Key</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-400">Tier</th>
                  <th className="text-right px-4 py-3 font-medium text-gray-400">Credit</th>
                  <th className="text-left px-4 py-3 font-medium text-gray-400">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {gifts.map((gift) => (
                  <tr key={gift.id} className="hover:bg-white/5">
                    <td className="px-4 py-3 font-mono text-xs">{gift.email}</td>
                    <td className="px-4 py-3 font-mono text-xs">{gift.license_key}</td>
                    <td className="px-4 py-3">
                      <TierBadge tier={gift.tier} />
                    </td>
                    <td className="px-4 py-3 text-right font-mono">
                      {gift.wallet_credit_micro > 0
                        ? `$${(gift.wallet_credit_micro / 1_000_000).toFixed(2)}`
                        : '—'}
                    </td>
                    <td className="px-4 py-3">
                      {gift.claimed_at ? (
                        <span className="text-green-400 text-xs">Claimed</span>
                      ) : (
                        <span className="text-yellow-400 text-xs">Pending</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}

function TierBadge({ tier }: { tier: string }) {
  const colors: Record<string, string> = {
    free: 'bg-gray-500/20 text-gray-300',
    starter: 'bg-blue-500/20 text-blue-300',
    power: 'bg-purple-500/20 text-purple-300',
  };
  return (
    <span className={`inline-block px-2 py-0.5 rounded text-xs ${colors[tier] ?? colors.free}`}>
      {tier}
    </span>
  );
}

function StatusBadge({ status }: { status: string }) {
  const colors: Record<string, string> = {
    active: 'bg-green-500/20 text-green-300',
    revoked: 'bg-red-500/20 text-red-300',
    expired: 'bg-yellow-500/20 text-yellow-300',
  };
  return (
    <span className={`inline-block px-2 py-0.5 rounded text-xs ${colors[status] ?? 'bg-gray-500/20 text-gray-300'}`}>
      {status}
    </span>
  );
}
