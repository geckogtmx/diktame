'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import Image from 'next/image';
import { createClient } from '@/lib/supabase/client';

export function Navbar() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isSignedIn, setIsSignedIn] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
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
    <nav
      className={`fixed top-0 w-full z-[60] h-16 flex items-center transition-all duration-300 ${isScrolled
          ? 'bg-[#020617]/90 backdrop-blur-xl shadow-lg border-b border-white/5'
          : 'bg-[#020617] border-b border-white/5'
        }`}
    >
      <div className="section-container w-full flex justify-between items-center px-8">
        {/* Logo */}
        <Link href="/" className="text-xl font-bold tracking-tight text-white flex items-center gap-2">
          <div className="w-8 h-8 rounded-lg flex items-center justify-center overflow-hidden">
            <Image src="/images/app-icon.png" alt="dIKta.me Icon" width={32} height={32} className="object-cover" />
          </div>
          dIKta.me
        </Link>

        {/* Desktop Links & CTA */}
        <div className="hidden md:flex items-center gap-12 text-sm font-medium">
          <Link href="/#core-track" className="text-[#94a3b8] hover:text-white transition-colors">
            Features
          </Link>
          <Link href="/#versus-track" className="text-[#94a3b8] hover:text-white transition-colors">
            vs Others
          </Link>
          <Link href="/#specs-track" className="text-[#94a3b8] hover:text-white transition-colors">
            Specs
          </Link>
          <Link href="/#pricing" className="text-[#94a3b8] hover:text-white transition-colors">
            Pricing
          </Link>
          <Link href="/docs" className="text-[#2563eb] hover:text-white transition-colors">
            Docs
          </Link>
          {isSignedIn ? (
            <Link href="/dashboard" className="btn-primary py-2.5 px-8 text-xs shadow-none hover:shadow-glow">
              Dashboard
            </Link>
          ) : (
            <Link href="/waitlist" className="btn-primary py-2.5 px-8 text-xs shadow-none hover:shadow-glow">
              Sign Up
            </Link>
          )}
        </div>

        {/* Mobile Menu Toggle */}
        <button
          onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
          className="md:hidden p-2 text-white"
          aria-label="Toggle menu"
        >
          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
      </div>

      {/* Mobile Menu */}
      <div
        className={`${isMobileMenuOpen ? 'block' : 'hidden'
          } md:hidden absolute top-full left-0 w-full bg-[#0f172a] border-b border-white/10 p-4 flex flex-col gap-4`}
      >
        <Link href="/#core-track" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          Features
        </Link>
        <Link href="/#versus-track" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          vs Others
        </Link>
        <Link href="/#specs-track" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          Specs
        </Link>
        <Link href="/#pricing" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          Pricing
        </Link>
        <Link href="/docs" className="text-[#2563eb] hover:text-white" onClick={() => setIsMobileMenuOpen(false)}>
          Documentation
        </Link>
        {isSignedIn ? (
          <Link href="/dashboard" className="btn-primary w-full justify-center" onClick={() => setIsMobileMenuOpen(false)}>
            Dashboard
          </Link>
        ) : (
          <Link href="/waitlist" className="btn-primary w-full justify-center" onClick={() => setIsMobileMenuOpen(false)}>
            Sign Up
          </Link>
        )}
      </div>
    </nav>
  );
}
