import React from 'react';
import { GlassCard } from './GlassCard';
import { Button } from './Button';

interface PricingCardProps {
  tier: string;
  price: string;
  description: string;
  features: string[];
  featured?: boolean;
  cta: string;
  ctaHref: string;
}

export function PricingCard({
  tier,
  price,
  description,
  features,
  featured = false,
  cta,
  ctaHref,
}: PricingCardProps) {
  return (
    <GlassCard
      className={`flex flex-col transition-all ${
        featured ? 'ring-2 ring-orange-500/50 lg:scale-105 lg:shadow-lg' : ''
      }`}
    >
      {featured && (
        <div className="mb-4 inline-block px-3 py-1 rounded-full text-xs font-semibold bg-orange-500/20 text-orange-300 border border-orange-500/30">
          Best Value
        </div>
      )}

      <h3 className="text-2xl font-bold text-white mb-2">{tier}</h3>
      <p className="text-sm text-gray-400 mb-6">{description}</p>

      <div className="mb-6">
        <span className="text-4xl font-bold text-white">${price}</span>
        <span className="text-gray-400 ml-2">one-time</span>
      </div>

      <Button href={ctaHref} variant={featured ? 'secondary' : 'primary'} className="mb-8 w-full text-center">
        {cta}
      </Button>

      <div className="flex-1">
        <p className="text-sm text-gray-500 mb-4 uppercase tracking-wider">Features included</p>
        <ul className="space-y-3">
          {features.map((feature) => (
            <li key={feature} className="flex gap-3 items-start text-sm text-gray-300">
              <span className="text-green-400 mt-0.5">✓</span>
              <span>{feature}</span>
            </li>
          ))}
        </ul>
      </div>
    </GlassCard>
  );
}
