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
      <nav
        className={`mx-auto h-16 flex items-center transition-all duration-500 ${isScrolled
            ? 'max-w-6xl bg-background/80 backdrop-blur-xl shadow-lg border border-white/10 rounded-2xl'
            : 'bg-transparent border-b border-white/5'
          }`}
      >
        <div className="w-full flex justify-between items-center px-8">
        {/* Logo */}
        <Link href="/" className="text-xl font-bold tracking-tight text-white flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center overflow-hidden">
            <Image src="/images/app-icon.png" alt="dIKta.me Icon" width={32} height={32} className="object-cover" />
          </div>
          dIKta.me
        </Link>

        {/* Desktop Links & CTA */}
        <div className="hidden md:flex items-center gap-12 text-sm font-medium">
          <Link href="/#core-track" className="text-muted hover:text-white transition-colors">
            {t('features')}
          </Link>
          <Link href="/#versus-track" className="text-muted hover:text-white transition-colors">
            {t('vsOthers')}
          </Link>
          <Link href="/#specs-track" className="text-muted hover:text-white transition-colors">
            {t('specs')}
          </Link>
          <Link href="/#pricing" className="text-muted hover:text-white transition-colors">
            {t('pricing')}
          </Link>
          <Link href="/docs" className="text-primary hover:text-white transition-colors">
            {t('docs')}
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
        <Link href="/#core-track" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('features')}
        </Link>
        <Link href="/#versus-track" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('vsOthers')}
        </Link>
        <Link href="/#specs-track" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('specs')}
        </Link>
        <Link href="/#pricing" className="text-white hover:text-primary" onClick={() => setIsMobileMenuOpen(false)}>
          {t('pricing')}
        </Link>
        <Link href="/docs" className="text-primary hover:text-white" onClick={() => setIsMobileMenuOpen(false)}>
          {t('docsMobile')}
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
