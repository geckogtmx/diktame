'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Core Arsenal section reveal animation
 * Reveals pillars first, then flavors row based on scroll progress through 300vh section
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
        setActivePair(2); // Everything visible
        return;
      }

      const progress = scrollTop / scrollableHeight;

      // Reveal pillars at 0-0.4, flavors at 0.4-1.0
      if (progress < 0.4) {
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
