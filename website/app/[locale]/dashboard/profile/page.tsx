'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { createClient } from '@/lib/supabase/client';
import { useTranslations } from 'next-intl';
import { AvatarCropModal } from '@/app/components/AvatarCropModal';
import { trackProfileUpdate, trackAvatarUpload, trackAccountDelete } from '@/lib/analytics';

interface Profile {
  id: string;
  email: string | undefined;
  name: string | null;
  avatarUrl: string | null;
  walletBalanceMicro: number;
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

  // Avatar crop state
  const [cropModalOpen, setCropModalOpen] = useState(false);
  const [selectedImageSrc, setSelectedImageSrc] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const router = useRouter();
  const t = useTranslations('ProfilePage');

  useEffect(() => {
    fetchProfile();
  }, []);

  async function fetchProfile() {
    try {
      setLoading(true);
      const response = await fetch('/api/profile');

      if (response.status === 401) {
        router.push('/login');
        return;
      }
      if (!response.ok) {
        throw new Error('Failed to fetch profile');
      }

      const data = await response.json();
      setProfile(data);
      setName(data.name || '');
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
        }),
      });

      if (!response.ok) {
        const errData = await response.json().catch(() => ({}));
        throw new Error(errData.detail || errData.error || 'Failed to update profile');
      }

      const updatedProfile = await response.json();
      setProfile(updatedProfile);
      setSuccess(t('profileUpdated'));
      trackProfileUpdate();

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
      trackAccountDelete();
      await createClient().auth.signOut();
      router.push('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete account');
      setDeleting(false);
    }
  }

  function onFileSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    // Validate size client-side (2MB)
    if (file.size > 2 * 1024 * 1024) {
      setError(t('fileTooLarge'));
      return;
    }

    const objectUrl = URL.createObjectURL(file);
    setSelectedImageSrc(objectUrl);
    setCropModalOpen(true);

    // Reset input so the same file can be re-selected
    e.target.value = '';
  }

  async function handleAvatarSave(blob: Blob) {
    setError(null);
    setSuccess(null);

    const formData = new FormData();
    formData.append('avatar', blob, 'avatar.webp');

    const response = await fetch('/api/profile/avatar', {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      const data = await response.json().catch(() => ({}));
      throw new Error(data.error || 'Failed to upload avatar');
    }

    const { avatarUrl: newUrl } = await response.json();
    setProfile((prev) => prev ? { ...prev, avatarUrl: newUrl } : prev);
    setCropModalOpen(false);
    if (selectedImageSrc) URL.revokeObjectURL(selectedImageSrc);
    setSelectedImageSrc(null);
    setSuccess(t('avatarUpdated'));
    trackAvatarUpload();
  }

  function handleCropCancel() {
    setCropModalOpen(false);
    if (selectedImageSrc) URL.revokeObjectURL(selectedImageSrc);
    setSelectedImageSrc(null);
  }

  // Avatar initial letter
  const avatarInitial = (profile?.name || profile?.email || '?')[0].toUpperCase();

  if (loading) {
    return (
      <div className="px-6 py-8 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-500 mx-auto mb-4"></div>
          <p className="text-gray-400">{t('loading')}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="px-6 py-8 max-w-4xl">
      {/* Header */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold">{t('pageTitle')}</h1>
        <p className="text-gray-400 text-sm mt-1">{t('pageSubtitle')}</p>
      </div>

      <div>
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

        {/* Avatar Section */}
        <div className="card p-6 mb-6 flex flex-col items-center">
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="relative group cursor-pointer rounded-full focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 focus:ring-offset-[#0a0a1a]"
          >
            {profile?.avatarUrl ? (
              <img
                src={profile.avatarUrl}
                alt="Profile"
                className="w-24 h-24 rounded-full object-cover"
              />
            ) : (
              <div className="w-24 h-24 rounded-full bg-blue-600/20 flex items-center justify-center text-3xl font-bold text-blue-400">
                {avatarInitial}
              </div>
            )}
            {/* Hover overlay */}
            <div className="absolute inset-0 rounded-full bg-black/50 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity">
              <svg className="w-6 h-6 text-white" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="M6.827 6.175A2.31 2.31 0 0 1 5.186 7.23c-.38.054-.757.112-1.134.175C2.999 7.58 2.25 8.507 2.25 9.574V18a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9.574c0-1.067-.75-1.994-1.802-2.169a47.865 47.865 0 0 0-1.134-.175 2.31 2.31 0 0 1-1.64-1.055l-.822-1.316a2.192 2.192 0 0 0-1.736-1.039 48.774 48.774 0 0 0-5.232 0 2.192 2.192 0 0 0-1.736 1.039l-.821 1.316Z" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M16.5 12.75a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Z" />
              </svg>
            </div>
          </button>
          <p className="text-xs text-gray-500 mt-3">{t('clickToUpload')}</p>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="hidden"
            onChange={onFileSelected}
          />
        </div>

        {/* Avatar Crop Modal */}
        {cropModalOpen && selectedImageSrc && (
          <AvatarCropModal
            imageSrc={selectedImageSrc}
            onSave={handleAvatarSave}
            onCancel={handleCropCancel}
            labels={{
              title: t('cropTitle'),
              zoom: t('cropZoom'),
              save: t('cropSave'),
              cancel: t('cropCancel'),
            }}
          />
        )}

        {/* Account Information */}
        <div className="card p-6 mb-6">
          <h2 className="text-xl font-bold mb-4">{t('accountInfoTitle')}</h2>
          <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-muted mb-2">{t('emailLabel')}</label>
              <input
                type="email"
                value={profile?.email || ''}
                disabled
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white/50 cursor-not-allowed"
              />
              <p className="text-xs text-muted mt-1">{t('emailNote')}</p>
            </div>

            <div>
              <label className="block text-sm font-medium text-muted mb-2">{t('nameLabel')}</label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder={t('namePlaceholder')}
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white focus:outline-none focus:border-primary"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-muted mb-2">{t('accountCreated')}</label>
              <input
                type="text"
                value={profile ? new Date(profile.createdAt).toLocaleDateString() : ''}
                disabled
                className="w-full px-4 py-2 bg-white/5 border border-white/10 rounded-lg text-white/50 cursor-not-allowed"
              />
            </div>
          </div>
        </div>

        {/* Save Button */}
        <button
          onClick={handleSave}
          disabled={saving}
          className="w-full py-3 bg-primary hover:bg-primary/80 text-white font-bold rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed mb-6"
        >
          {saving ? t('saving') : t('saveChanges')}
        </button>

        {/* Danger Zone */}
        <div className="card p-6 border-2 border-red-500/20">
          <h2 className="text-xl font-bold text-red-400 mb-4">{t('dangerZoneTitle')}</h2>
          <p className="text-muted mb-4">
            {t('dangerZoneDesc')}
          </p>

          {!showDeleteConfirm ? (
            <button
              onClick={() => setShowDeleteConfirm(true)}
              className="px-6 py-2 bg-red-500/10 hover:bg-red-500/20 text-red-400 border border-red-500/20 rounded-lg transition-colors"
            >
              {t('deleteAccount')}
            </button>
          ) : (
            <div className="space-y-4">
              <p className="text-red-400 font-medium">{t('deleteConfirm')}</p>
              <div className="flex gap-4">
                <button
                  onClick={handleDelete}
                  disabled={deleting}
                  className="px-6 py-2 bg-red-500 hover:bg-red-600 text-white font-bold rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {deleting ? t('deleting') : t('deleteConfirmYes')}
                </button>
                <button
                  onClick={() => setShowDeleteConfirm(false)}
                  disabled={deleting}
                  className="px-6 py-2 bg-white/5 hover:bg-white/10 text-white border border-white/10 rounded-lg transition-colors"
                >
                  {t('cancel')}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
