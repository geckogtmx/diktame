'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Ask Mode section typewriter animation
 * Animates input/output width based on scroll progress through 200vh section
 * Identical logic to Bilingual section
 * Extracted from sitex scroll logic
 */
export function useAskModeScroll() {
  const [inputWidth, setInputWidth] = useState(0);
  const [outputWidth, setOutputWidth] = useState(0);
  const [showInputCursor, setShowInputCursor] = useState(false);
  const [showOutputCursor, setShowOutputCursor] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      const askTrack = document.getElementById('ask-track');
      if (!askTrack) return;

      const rect = askTrack.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      let progress = 0;
      if (scrollTop > 0) {
        progress = Math.min(scrollTop / scrollableHeight, 1);
      }

      // Input animates from 0 to 100% during first 45% of scroll
      const inputProgress = Math.min(progress / 0.45, 1);
      setInputWidth(inputProgress * 100);
      setShowInputCursor(progress > 0 && progress < 0.45);

      // Output animates from 0 to 100% during last 45% of scroll (after 55%)
      const outputProgress = progress < 0.55 ? 0 : (progress - 0.55) / 0.45;
      setOutputWidth(Math.min(outputProgress, 1) * 100);
      setShowOutputCursor(progress > 0.55 && progress < 1);
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { inputWidth, outputWidth, showInputCursor, showOutputCursor };
}
