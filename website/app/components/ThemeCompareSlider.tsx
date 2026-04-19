'use client';

import { useRef, useState } from 'react';

interface ThemeCompareSliderProps {
  before: string;
  after: string;
  beforeLabel?: string;
  afterLabel?: string;
  alt?: string;
}

export function ThemeCompareSlider({
  before,
  after,
  beforeLabel = 'Before',
  afterLabel = 'After',
  alt = 'Theme comparison',
}: ThemeCompareSliderProps) {
  const [position, setPosition] = useState(50);
  const containerRef = useRef<HTMLDivElement>(null);

  return (
    <figure className="my-8 overflow-hidden rounded-lg border border-white/10 bg-black not-prose">
      <div
        ref={containerRef}
        className="relative w-full select-none"
        style={{ aspectRatio: 'auto' }}
      >
        {/* Bottom layer: "after" image, fully visible */}
        <img
          src={after}
          alt={`${alt} — ${afterLabel}`}
          className="block w-full h-auto"
          loading="lazy"
          draggable={false}
        />

        {/* Top layer: "before" image, clipped by slider position */}
        <div
          className="absolute inset-0 overflow-hidden pointer-events-none"
          style={{ clipPath: `inset(0 ${100 - position}% 0 0)` }}
        >
          <img
            src={before}
            alt={`${alt} — ${beforeLabel}`}
            className="block w-full h-auto"
            loading="lazy"
            draggable={false}
          />
        </div>

        {/* Labels */}
        <div className="absolute top-3 left-3 bg-black/70 text-white text-xs font-medium px-2 py-1 rounded backdrop-blur-sm pointer-events-none">
          {beforeLabel}
        </div>
        <div className="absolute top-3 right-3 bg-black/70 text-white text-xs font-medium px-2 py-1 rounded backdrop-blur-sm pointer-events-none">
          {afterLabel}
        </div>

        {/* Divider line */}
        <div
          className="absolute top-0 bottom-0 w-0.5 bg-white/90 shadow-[0_0_8px_rgba(255,255,255,0.5)] pointer-events-none"
          style={{ left: `${position}%`, transform: 'translateX(-50%)' }}
        >
          <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-10 h-10 bg-white rounded-full shadow-lg flex items-center justify-center">
            <svg viewBox="0 0 24 24" className="w-5 h-5 text-black" fill="currentColor">
              <path d="M8 7l-5 5 5 5V7zm8 10l5-5-5-5v10z" />
            </svg>
          </div>
        </div>

        {/* Range input overlay — full width, invisible but captures drag */}
        <input
          type="range"
          min={0}
          max={100}
          value={position}
          onChange={(e) => setPosition(Number(e.target.value))}
          className="absolute inset-0 w-full h-full opacity-0 cursor-ew-resize"
          aria-label={`Compare ${beforeLabel} and ${afterLabel}`}
        />
      </div>
    </figure>
  );
}
