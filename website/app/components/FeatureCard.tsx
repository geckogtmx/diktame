import React from 'react';
import { GlassCard } from './GlassCard';

interface FeatureCardProps {
  number: string;
  title: string;
  description: string;
  hotkey: string;
  icon?: React.ReactNode;
  isVisible?: boolean;
}

export function FeatureCard({
  number,
  title,
  description,
  hotkey,
  icon,
  isVisible = true,
}: FeatureCardProps) {
  return (
    <GlassCard
      className={`transition-all duration-700 ${
        isVisible
          ? 'opacity-100 translate-y-0'
          : 'opacity-0 translate-y-8'
      }`}
    >
      <div className="flex items-start gap-4">
        <div className="text-4xl font-bold text-blue-400/40">{number}</div>
        <div className="flex-1">
          <h3 className="text-xl font-bold text-white mb-2">{title}</h3>
          <p className="text-gray-400 mb-4">{description}</p>
          <div className="inline-block px-3 py-1 rounded-lg bg-blue-500/10 border border-blue-500/20">
            <code className="text-sm text-blue-300 font-mono">{hotkey}</code>
          </div>
        </div>
        {icon && <div className="text-4xl">{icon}</div>}
      </div>
    </GlassCard>
  );
}
