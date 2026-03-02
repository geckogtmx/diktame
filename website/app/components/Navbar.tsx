'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';

export function Navbar() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
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
          <div className="w-8 h-8 bg-[#2563eb] rounded-lg flex items-center justify-center">
            <svg className="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z"
              />
            </svg>
          </div>
          dIKta.me
        </Link>

        {/* Desktop Links & CTA */}
        <div className="hidden md:flex items-center gap-12 text-sm font-medium">
          <a href="#core-track" className="text-[#94a3b8] hover:text-white transition-colors">
            Features
          </a>
          <a href="#versus-track" className="text-[#94a3b8] hover:text-white transition-colors">
            vs Others
          </a>
          <a href="#pricing" className="text-[#94a3b8] hover:text-white transition-colors">
            Pricing
          </a>
          <Link href="/docs" className="text-[#2563eb] hover:text-white transition-colors">
            Docs
          </Link>
          <Link href="/login" className="btn-primary py-2.5 px-8 text-xs shadow-none hover:shadow-glow">
            Sign Up
          </Link>
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
        <a href="#core-track" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          Features
        </a>
        <a href="#versus-track" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          vs Others
        </a>
        <a href="#pricing" className="text-white hover:text-[#2563eb]" onClick={() => setIsMobileMenuOpen(false)}>
          Pricing
        </a>
        <Link href="/docs" className="text-[#2563eb] hover:text-white" onClick={() => setIsMobileMenuOpen(false)}>
          Documentation
        </Link>
        <Link href="/login" className="btn-primary w-full justify-center" onClick={() => setIsMobileMenuOpen(false)}>
          Sign Up
        </Link>
      </div>
    </nav>
  );
}
