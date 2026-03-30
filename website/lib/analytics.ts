// Google Analytics 4 event tracking utility.
// Typed wrappers around gtag() — safe no-op when GA isn't loaded (dev, ad-blockers).

export const GA_ID = 'G-VYLRMHENMK';

type GTagEvent = {
  action: string;
  category?: string;
  label?: string;
  value?: number;
  [key: string]: unknown;
};

function gtag(event: GTagEvent) {
  if (typeof window === 'undefined' || !window.gtag) return;
  const { action, ...params } = event;
  window.gtag('event', action, params);
}

// ── Conversion Events (key funnel steps) ─────────────────────────────

/** Waitlist form submitted successfully */
export function trackWaitlistSignup(location: string) {
  gtag({ action: 'sign_up_waitlist', category: 'conversion', label: location });
}

/** Waitlist invite sent (email, twitter, whatsapp) */
export function trackWaitlistInvite(method: 'email' | 'twitter' | 'whatsapp') {
  gtag({ action: 'waitlist_invite_sent', category: 'conversion', label: method });
}

/** User logged in */
export function trackLogin(method: string) {
  gtag({ action: 'login', category: 'conversion', label: method });
}

/** User signed out */
export function trackSignOut() {
  gtag({ action: 'sign_out', category: 'engagement' });
}

// ── Engagement Events ────────────────────────────────────────────────

/** CTA button clicked (e.g., "Get Power License" on pricing section) */
export function trackCtaClick(label: string, location: string) {
  gtag({ action: 'cta_click', category: 'engagement', label: `${label}|${location}` });
}

/** External link clicked (GitHub, Ko-fi, LinkedIn, etc.) */
export function trackExternalLink(url: string, label: string) {
  gtag({ action: 'external_link', category: 'engagement', label, url });
}

/** FAQ item expanded */
export function trackFaqExpand(question: string) {
  gtag({ action: 'faq_expand', category: 'engagement', label: question.slice(0, 100) });
}

/** Features modal opened from pricing section */
export function trackFeaturesModalOpen() {
  gtag({ action: 'features_modal_open', category: 'engagement' });
}

/** Language switched */
export function trackLanguageSwitch(from: string, to: string) {
  gtag({ action: 'language_switch', category: 'engagement', label: `${from}_to_${to}` });
}

/** Landing page section scrolled into view */
export function trackSectionView(sectionId: string) {
  gtag({ action: 'section_view', category: 'scroll_depth', label: sectionId });
}

// ── Dashboard Events ─────────────────────────────────────────────────

/** Profile updated */
export function trackProfileUpdate() {
  gtag({ action: 'profile_update', category: 'dashboard' });
}

/** Avatar uploaded */
export function trackAvatarUpload() {
  gtag({ action: 'avatar_upload', category: 'dashboard' });
}

/** Account deleted */
export function trackAccountDelete() {
  gtag({ action: 'account_delete', category: 'dashboard' });
}

/** Wallet history viewed */
export function trackWalletView() {
  gtag({ action: 'wallet_view', category: 'dashboard' });
}

// ── Type augmentation for window.gtag ────────────────────────────────

declare global {
  interface Window {
    gtag: (...args: unknown[]) => void;
  }
}
