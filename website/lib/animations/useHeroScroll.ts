'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for hero section word carousel scroll animation
 * Translates words vertically based on scroll progress through 200vh section
 * Extracted from sitex scroll logic
 */
export function useHeroScroll() {
  const [translateY, setTranslateY] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const heroTrack = document.getElementById('hero-track');
      const heroWords = document.getElementById('hero-words');

      if (!heroTrack || !heroWords) return;

      const rect = heroTrack.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      let progress = 0;
      if (scrollTop > 0) {
        progress = Math.min(scrollTop / scrollableHeight, 1);
      }

      // Translate by -5.7em at 100% scroll (4 words × 1.9em spacing - 1.6em offset)
      const translateValue = progress * -5.7;
      setTranslateY(translateValue);
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { translateY };
}
