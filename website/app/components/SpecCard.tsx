import React from 'react';
import { GlassCard } from './GlassCard';

interface SpecCardProps {
  icon: string;
  title: string;
  description: string;
  isVisible?: boolean;
}

export function SpecCard({ icon, title, description, isVisible = true }: SpecCardProps) {
  return (
    <GlassCard
      className={`transition-all duration-500 ${
        isVisible
          ? 'opacity-100 scale-100'
          : 'opacity-0 scale-95'
      }`}
    >
      <div className="text-3xl mb-3">{icon}</div>
      <h3 className="font-semibold text-white mb-2">{title}</h3>
      <p className="text-sm text-gray-400">{description}</p>
    </GlassCard>
  );
}
