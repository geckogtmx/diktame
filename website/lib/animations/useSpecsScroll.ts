'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Specs section group toggle animation
 * Toggles between group 1 and group 2 based on scroll progress through 300vh section
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
        setActiveGroup(2);
        return;
      }

      const progress = scrollTop / scrollableHeight;

      // Toggle at 50% threshold
      if (progress < 0.5) {
        setActiveGroup(1);
      } else {
        setActiveGroup(2);
      }
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { activeGroup };
}
