import Link from 'next/link';

interface CtaSeparatorProps {
  text: string;
  ctaLabel: string;
  ctaHref: string;
  variant?: 'primary' | 'subtle';
}

export function CtaSeparator({ text, ctaLabel, ctaHref, variant = 'subtle' }: CtaSeparatorProps) {
  const isPrimary = variant === 'primary';

  return (
    <div className="w-full py-12 md:py-16">
      <div className="max-w-3xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-center gap-4 sm:gap-6 text-center">
        <p className={`text-sm md:text-base font-medium ${isPrimary ? 'text-white' : 'text-muted'}`}>
          {text}
        </p>
        <Link
          href={ctaHref}
          className={`whitespace-nowrap text-sm font-semibold px-6 py-2.5 rounded-full transition-all hover:scale-105 ${
            isPrimary
              ? 'bg-cta text-white shadow-glow'
              : 'border border-white/10 text-white hover:border-white/30 hover:bg-white/5'
          }`}
        >
          {ctaLabel}
        </Link>
      </div>
    </div>
  );
}
