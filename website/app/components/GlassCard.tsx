import React from 'react';

interface GlassCardProps {
  children: React.ReactNode;
  className?: string;
  glow?: boolean;
}

export function GlassCard({ children, className = '', glow = false }: GlassCardProps) {
  return (
    <div
      className={`backdrop-blur-xl bg-black/40 border border-white/10 rounded-2xl p-6 sm:p-8 ${
        glow ? 'shadow-lg shadow-blue-500/10' : ''
      } ${className}`}
    >
      {children}
    </div>
  );
}
