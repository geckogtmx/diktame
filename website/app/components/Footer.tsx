'use client';

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
        <div className="flex gap-6">
          <a href="#" className="text-muted hover:text-white transition-colors">
            Twitter
          </a>
          <a href="#" className="text-muted hover:text-white transition-colors">
            GitHub
          </a>
          <a href="#" className="text-muted hover:text-white transition-colors">
            Discord
          </a>
        </div>
      </div>
    </footer>
  );
}
