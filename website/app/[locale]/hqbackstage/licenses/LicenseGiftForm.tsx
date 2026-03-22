'use client';

import { useState } from 'react';

export function LicenseGiftForm() {
  const [email, setEmail] = useState('');
  const [tier, setTier] = useState('starter');
  const [creditUsd, setCreditUsd] = useState('');
  const [result, setResult] = useState<{ success: boolean; message: string; licenseKey?: string } | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setResult(null);
    setLoading(true);

    try {
      const walletCreditMicro = creditUsd ? Math.round(parseFloat(creditUsd) * 1_000_000) : 0;

      const res = await fetch('/api/hqbackstage/licenses/gift', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, tier, walletCreditMicro }),
      });

      const data = await res.json();

      if (res.ok) {
        setResult({
          success: true,
          message: data.message,
          licenseKey: data.licenseKey,
        });
        setEmail('');
        setCreditUsd('');
      } else {
        setResult({ success: false, message: data.error ?? 'Gift failed' });
      }
    } catch {
      setResult({ success: false, message: 'Network error' });
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="rounded-xl border border-white/10 bg-white/5 p-6">
      <h2 className="text-lg font-semibold mb-4">Gift a License</h2>
      <form onSubmit={handleSubmit} className="flex flex-wrap gap-3 items-end">
        <div className="flex-1 min-w-[200px]">
          <label className="block text-xs text-gray-400 mb-1">Email</label>
          <input
            type="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="user@example.com"
            className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-sm focus:outline-none focus:border-white/30"
          />
        </div>
        <div className="w-32">
          <label className="block text-xs text-gray-400 mb-1">Tier</label>
          <select
            value={tier}
            onChange={(e) => setTier(e.target.value)}
            className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-sm focus:outline-none focus:border-white/30"
          >
            <option value="starter">Starter</option>
            <option value="power">Power</option>
          </select>
        </div>
        <div className="w-32">
          <label className="block text-xs text-gray-400 mb-1">Credit (USD)</label>
          <input
            type="number"
            step="0.01"
            min="0"
            value={creditUsd}
            onChange={(e) => setCreditUsd(e.target.value)}
            placeholder="0.00"
            className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-sm focus:outline-none focus:border-white/30"
          />
        </div>
        <button
          type="submit"
          disabled={loading}
          className="px-5 py-2 rounded-lg bg-purple-600 text-white text-sm font-medium hover:bg-purple-500 disabled:opacity-50 transition-colors"
        >
          {loading ? 'Gifting...' : 'Gift'}
        </button>
      </form>

      {result && (
        <div
          className={`mt-3 px-4 py-2 rounded-lg text-sm ${
            result.success
              ? 'bg-green-500/10 border border-green-500/30 text-green-200'
              : 'bg-red-500/10 border border-red-500/30 text-red-200'
          }`}
        >
          {result.message}
          {result.licenseKey && (
            <span className="block mt-1 font-mono text-xs">{result.licenseKey}</span>
          )}
        </div>
      )}
    </div>
  );
}
