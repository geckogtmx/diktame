'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Specs section group toggle animation
 * Cycles through 3 groups based on scroll progress through 400vh section
 * Extracted from sitex scroll logic
 */
export function useSpecsScroll() {
  const [activeGroup, setActiveGroup] = useState(1);

  useEffect(() => {
    const handleScroll = () => {
      const specsTrack = document.getElementById('specs-track');
      if (!specsTrack) return;

      const rect = specsTrack.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      if (scrollTop < 0) {
        setActiveGroup(1);
        return;
      }

      if (scrollTop > scrollableHeight) {
        setActiveGroup(3);
        return;
      }

      const progress = scrollTop / scrollableHeight;

      if (progress < 0.33) {
        setActiveGroup(1);
      } else if (progress < 0.66) {
        setActiveGroup(2);
      } else {
        setActiveGroup(3);
      }
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { activeGroup };
}
