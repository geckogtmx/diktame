'use client';

import Link from 'next/link';

export function Footer() {
  return (
    <footer className="py-12 border-t border-white/5 bg-surface/30">
      <div className="section-container flex flex-col md:flex-row justify-between items-center gap-6">
        <div className="flex flex-col gap-2">
          <div className="text-muted text-sm">© 2026 dIKta.me. All rights reserved.</div>
          <div className="text-[10px] text-muted/50 font-mono uppercase tracking-[0.2em]">
            Co-authored by Human & AI (Gemini Studio, Antigravity, Claude Code)
          </div>
        </div>
        <div className="flex gap-6 text-sm">
          <Link href="/about" className="text-muted hover:text-white transition-colors">
            About
          </Link>
          <Link href="/privacy" className="text-muted hover:text-white transition-colors">
            Privacy
          </Link>
          <Link href="/terms" className="text-muted hover:text-white transition-colors">
            Terms
          </Link>
          <a
            href="https://github.com/geckogtmx/diktame"
            target="_blank"
            rel="noopener noreferrer"
            className="text-muted hover:text-white transition-colors"
          >
            GitHub
          </a>
        </div>
      </div>
    </footer>
  );
}
