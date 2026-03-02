'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Versus section row stagger animation
 * Sequentially reveals table rows based on scroll progress through 250vh section
 * Extracted from sitex scroll logic
 */
export function useVersusScroll() {
  const [visibleRows, setVisibleRows] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const versusTrack = document.getElementById('versus-track');
      if (!versusTrack) return;

      const rect = versusTrack.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      if (scrollTop < 0) {
        setVisibleRows(0);
        return;
      }

      if (scrollTop > scrollableHeight) {
        setVisibleRows(7); // All 7 rows visible
        return;
      }

      const progress = scrollTop / scrollableHeight;

      // Calculate visible rows: 0-7 based on progress
      const rows = Math.min(Math.floor(progress * 7) + 1, 7);
      setVisibleRows(rows);
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { visibleRows };
}
