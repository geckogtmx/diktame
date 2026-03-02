'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Core Arsenal section pair reveal animation
 * Reveals card pairs based on scroll progress through 400vh section
 * Extracted from sitex scroll logic
 */
export function useCoreArsenalScroll() {
  const [activePair, setActivePair] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const coreTrack = document.getElementById('core-track');
      if (!coreTrack) return;

      const rect = coreTrack.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      if (scrollTop < 0) {
        setActivePair(0);
        return;
      }

      if (scrollTop > scrollableHeight) {
        setActivePair(2); // Both pairs visible
        return;
      }

      const progress = scrollTop / scrollableHeight;

      // Reveal pair 1 at 0-0.5, pair 2 at 0.5-1.0
      if (progress < 0.5) {
        setActivePair(1);
      } else {
        setActivePair(2);
      }
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { activePair };
}
