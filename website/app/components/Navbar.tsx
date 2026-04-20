'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import Image from 'next/image';
import { createClient } from '@/lib/supabase/client';
import { useTranslations } from 'next-intl';
import { LanguageSwitcher } from './LanguageSwitcher';

export function Navbar() {
  const t = useTranslations('Navbar');
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isSignedIn, setIsSignedIn] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      const hasHero = !!document.getElementById('hero-track');
      const threshold = hasHero ? window.innerHeight : 20;
      setIsScrolled(window.scrollY > threshold);
    };

    window.addEventListener('scroll', handleScroll);

    // Check auth state
    const supabase = createClient();
    supabase.auth.getUser().then(({ data: { user } }) => {
      setIsSignedIn(!!user);
    });

    const { data: { subscription } } = supabase.auth.onAuthStateChange((_event, session) => {
      setIsSignedIn(!!session?.user);
    });

    return () => {
      window.removeEventListener('scroll', handleScroll);
      subscription.unsubscribe();
    };
  }, []);

  return (
    <header
      className={`fixed z-[60] transition-all duration-500 ${isScrolled
          ? 'top-3 left-4 right-4'
          : 'top-0 left-0 right-0'
        }`}
    >
      <a href="#main-content" className="sr-only focus:not-sr-only focus:absolute focus:top-4 focus:left-4 focus:z-[100] focus:px-4 focus:py-2 focus:bg-white focus:text-black focus:rounded">
        Skip to content
      </a>
      <nav
        className={`mx-auto h-16 flex items-center transition-all duration-500 ${isScrolled
            ? 'max-w-6xl bg-background/80 backdrop-blur-xl shadow-lg border border-white/10 rounded-2xl'
            : 'bg-transparent border-b border-white/5'
          }`}
      >
        <div className="w-full flex justify-between items-center px-8">
        {/* Logo — Link for SPA navigation from other routes; onClick handles
             the "already on /" case by scrolling to top manually, since the
             router treats a same-route Link click as a no-op. */}
        <Link
          href="/"
          className="text-xl font-bold tracking-tight text-white flex items-center gap-2"
          onClick={(e) => {
            if (typeof window === 'undefined') return;
            const path = window.location.pathname.replace(/\/$/, '');
            const isLocaleRoot = /^\/(en|es)?$/.test(path);
            if (isLocaleRoot) {
              e.preventDefault();
              window.scrollTo({ top: 0, behavior: 'smooth' });
            }
          }}
        >
          <div className="w-8 h-8 rounded-lg flex items-center justify-center overflow-hidden">
            <Image src="/images/app-icon.png" alt="dIKta.me — local AI voice dictation for Windows" width={32} height={32} className="object-cover" />
          </div>
          dIKta.me
        </Link>

        {/* Desktop Links & CTA */}
        <div className="hidden md:flex items-center gap-12 text-sm font-medium">
          {/*
            Same-page anchor links use a plain <a> instead of next/link's <Link>.
            On the App Router, <Link href="/#x"> suppresses scroll when the
            user is already on "/" (it sees "same route, nothing to do"), so
            the click appears to do nothing. A native <a> bypasses the router
            and the browser handles the hash scroll correctly from any page.
          */}
          <a href="/#versus-track" className="text-muted hover:text-white transition-colors">
            {t('vsOthers')}
          </a>
          <a href="/#specs-track" className="text-muted hover:text-white transition-colors">
            {t('specs')}
          </a>
          <a href="/#pricing" className="text-muted hover:text-white transition-colors">
            {t('pricing')}
          </a>
          <Link href="/docs" className="text-primary hover:text-white transition-colors">
            {t('docs')}
          </Link>
          <Link href="/blog" className="text-muted hover:text-white transition-colors">
            {t('blog')}
          </Link>
          <Link href="/roadmap" className="text-muted hover:text-white transition-colors">
            {t('roadmap')}
          </Link>
          <LanguageSwitcher />
          {isSignedIn ? (
            <Link href="/dashboard" className="btn-primary py-2.5 px-8 text-xs shadow-none hover:shadow-glow">
              {t('dashboard')}
            </Link>
          ) : (
            <Link href="/waitlist" className="btn-primary py-2.5 px-8 text-xs shadow-none hover:shadow-glow">
              {t('signUp')}
            </Link>
          )}
        </div>

        {/* Mobile Menu Toggle */}
        <button
          onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
          className="md:hidden p-2 text-white"
          aria-label={t('toggleMenu')}
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
      </div>

      {/* Mobile Menu */}
      <div
        className={`${isMobileMenuOpen ? 'block' : 'hidden'
          } md:hidden absolute top-full left-0 w-full p-4 flex flex-col gap-4 ${isScrolled
            ? 'bg-background/80 backdrop-blur-xl border border-t-0 border-white/10 rounded-b-2xl mt-0'
            : 'bg-surface border-b border-white/10'
          }`}
      >
        <a href="/#versus-track" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('vsOthers')}
        </a>
        <a href="/#specs-track" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('specs')}
        </a>
        <a href="/#pricing" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('pricing')}
        </a>
        <Link href="/docs" className="text-primary hover:text-white" onClick={() => setIsMobileMenuOpen(false)}>
          {t('docsMobile')}
        </Link>
        <Link href="/blog" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('blog')}
        </Link>
        <Link href="/roadmap" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('roadmap')}
        </Link>
        <LanguageSwitcher />
        {isSignedIn ? (
          <Link href="/dashboard" className="btn-primary w-full justify-center" onClick={() => setIsMobileMenuOpen(false)}>
            {t('dashboard')}
          </Link>
        ) : (
          <Link href="/waitlist" className="btn-primary w-full justify-center" onClick={() => setIsMobileMenuOpen(false)}>
            {t('signUp')}
          </Link>
        )}
      </div>
      </nav>
    </header>
  );
}
