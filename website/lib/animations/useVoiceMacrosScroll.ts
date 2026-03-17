'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Voice Macros section scroll-driven animation.
 * Drives trigger→output typewriter for two macros based on scroll progress through 300vh section.
 *
 * Phases:
 *  0.00–0.12  Trigger 1 types in
 *  0.12–0.15  Pause (arrow highlights)
 *  0.15–0.40  Output 1 types in
 *  0.40–0.50  Hold
 *  0.50–0.60  Trigger 2 types in
 *  0.60–0.63  Pause (arrow highlights)
 *  0.63–0.90  Output 2 types in
 *  0.90–1.00  Hold
 */
export function useVoiceMacrosScroll() {
  const [triggerProgress, setTriggerProgress] = useState(0);
  const [outputProgress, setOutputProgress] = useState(0);
  const [arrowActive, setArrowActive] = useState(false);
  const [macroIdx, setMacroIdx] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const track = document.getElementById('macros-track');
      if (!track) return;

      const rect = track.getBoundingClientRect();
      const viewportHeight = window.innerHeight;
      const trackHeight = rect.height;
      const scrollableHeight = trackHeight - viewportHeight;
      const scrollTop = -rect.top;

      let progress = 0;
      if (scrollTop > 0) {
        progress = Math.min(scrollTop / scrollableHeight, 1);
      }

      if (progress < 0.50) {
        // Macro 1
        setMacroIdx(0);
        setTriggerProgress(Math.min(progress / 0.12, 1));
        setArrowActive(progress >= 0.12);
        setOutputProgress(
          progress < 0.15 ? 0 : Math.min((progress - 0.15) / 0.25, 1)
        );
      } else {
        // Macro 2
        setMacroIdx(1);
        const p = progress - 0.50;
        setTriggerProgress(Math.min(p / 0.10, 1));
        setArrowActive(p >= 0.10);
        setOutputProgress(
          p < 0.13 ? 0 : Math.min((p - 0.13) / 0.27, 1)
        );
      }
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll();

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { triggerProgress, outputProgress, arrowActive, macroIdx };
}
