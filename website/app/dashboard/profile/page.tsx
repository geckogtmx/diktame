'use client';

export const dynamic = 'force-dynamic';
import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { createClient } from '@/lib/supabase/client';

interface Profile {
  id: string;
  email: string | undefined;
  name: string | null;
  trialWordsQuota: number;
  trialExpiresAt: string;
  hasCustomGeminiKey: boolean;
  createdAt: string;
  updatedAt: string;
}

export default function ProfilePage() {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Form state
  const [name, setName] = useState('');
  const [customGeminiKey, setCustomGeminiKey] = useState('');
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const router = useRouter();

  useEffect(() => {
    fetchProfile();
  }, []);

  async function fetchProfile() {
    try {
      setLoading(true);
      const response = await fetch('/api/profile');

      if (!response.ok) {
        throw new Error('Failed to fetch profile');
      }

      const data = await response.json();
      setProfile(data);
      setName(data.name || '');
      // Don't populate customGeminiKey for security
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load profile');
    } finally {
      setLoading(false);
    }
  }

  async function handleSave() {
    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      const response = await fetch('/api/profile', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          name: name.trim() || null,
          customGeminiKey: customGeminiKey.trim() || undefined,
        }),
      });

      if (!response.ok) {
        throw new Error('Failed to update profile');
      }

      const updatedProfile = await response.json();
      setProfile(updatedProfile);
      setSuccess('Profile updated successfully!');
      setCustomGeminiKey(''); // Clear the input after saving
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update profile');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    try {
      setDeleting(true);
      setError(null);

      const response = await fetch('/api/profile', {
        method: 'DELETE',
      });

      if (!response.ok) {
        throw new Error('Failed to delete account');
      }

      // Sign out and redirect to home
      await createClient().auth.signOut();
      router.push('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete account');
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-[#020617] text-white flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-[#2563eb] mx-auto mb-4"></div>
          <p className="text-[#94a3b8]">Loading profile...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#020617] text-white">
      {/* Header */}
      <div className="border-b border-white/10 bg-black/20">
        <div className="max-w-4xl mx-auto px-4 py-6">
          <button
            onClick={() => router.push('/dashboard')}
            className="text-[#2563eb] hover:text-white mb-4 flex items-center gap-2"
          >
            ← Back to Dashboard
          </button>
          <h1 className="text-3xl font-bold">Profile Settings</h1>
          <p className="text-[#94a3b8] mt-2">Manage your account and API keys</p>
        </div>
      </div>

      {/* Content */}
      <div className="max-w-4xl mx-auto px-4 py-8">
        {error && (
          <div className="mb-6 p-4 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400">
            {error}
          </div>
        )}

        {success && (
          <div className="mb-6 p-4 bg-green-500/10 border border-green-500/20 rounded-lg text-green-400">
            {success}
          </div>
        )}

        {/* Account Information */}
        <div className="card p-6 mb-6">
          <h2 className="text-xl font-bold mb-4">Account Information</h2>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-[#94a3b8] mb-2">Email</label>
              <input
                type="email"
                value={profile?.email || ''}
                disabled
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white/50 cursor-not-allowed"
              />
              <p className="text-xs text-[#94a3b8] mt-1">Email cannot be changed</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-[#94a3b8] mb-2">Name</label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Your name"
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white focus:outline-none focus:border-[#2563eb]"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-[#94a3b8] mb-2">Account Created</label>
              <input
                type="text"
                value={profile ? new Date(profile.createdAt).toLocaleDateString() : ''}
                disabled
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white/50 cursor-not-allowed"
              />
            </div>
          </div>
        </div>

        {/* API Key Management */}
        <div className="card p-6 mb-6">
          <h2 className="text-xl font-bold mb-4">Custom Gemini API Key</h2>
          <p className="text-[#94a3b8] mb-4">
            Add your own Gemini API key to bypass trial limits. Your key is encrypted and never shared.
          </p>

          {profile?.hasCustomGeminiKey && (
            <div className="mb-4 p-3 bg-green-500/10 border border-green-500/20 rounded-lg text-green-400 text-sm">
              ✓ Custom API key is active
            </div>
          )}

          <div>
            <label className="block text-sm font-medium text-[#94a3b8] mb-2">
              Gemini API Key
            </label>
            <input
              type="password"
              value={customGeminiKey}
              onChange={(e) => setCustomGeminiKey(e.target.value)}
              placeholder={profile?.hasCustomGeminiKey ? '••••••••••••••••' : 'Enter your Gemini API key'}
              className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white focus:outline-none focus:border-[#2563eb] font-mono text-sm"
            />
            <p className="text-xs text-[#94a3b8] mt-1">
              Leave blank to use managed trial credits. Enter a new key to update.
            </p>
          </div>
        </div>

        {/* Save Button */}
        <button
          onClick={handleSave}
          disabled={saving}
          className="w-full py-3 bg-[#2563eb] hover:bg-[#1d4ed8] text-white font-bold rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed mb-6"
        >
          {saving ? 'Saving...' : 'Save Changes'}
        </button>

        {/* Danger Zone */}
        <div className="card p-6 border-2 border-red-500/20">
          <h2 className="text-xl font-bold text-red-400 mb-4">Danger Zone</h2>
          <p className="text-[#94a3b8] mb-4">
            Once you delete your account, there is no going back. All your data will be permanently deleted.
          </p>

          {!showDeleteConfirm ? (
            <button
              onClick={() => setShowDeleteConfirm(true)}
              className="px-6 py-2 bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/20 rounded-lg transition-colors"
            >
              Delete Account
            </button>
          ) : (
            <div className="space-y-4">
              <p className="text-red-400 font-medium">Are you absolutely sure?</p>
              <div className="flex gap-4">
                <button
                  onClick={handleDelete}
                  disabled={deleting}
                  className="px-6 py-2 bg-red-500 hover:bg-red-600 text-white font-bold rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {deleting ? 'Deleting...' : 'Yes, Delete My Account'}
                </button>
                <button
                  onClick={() => setShowDeleteConfirm(false)}
                  disabled={deleting}
                  className="px-6 py-2 bg-white/5 hover:bg-white/10 text-white border border-white/10 rounded-lg transition-colors"
                >
                  Cancel
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
