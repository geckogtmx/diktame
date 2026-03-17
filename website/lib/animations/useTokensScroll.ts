'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Tokens section scroll-driven animation.
 * Cycles through 3 models with key typing based on scroll progress through 300vh section.
 *
 * Phases:
 *  0.00–0.20  Model 1 types in (gemma3:4b Local) — no key highlights
 *  0.20–0.45  Model 2 types in (claude-sonnet-4.5 Anthropic) + Anthropic key types
 *  0.45–0.70  Model 3 types in (gemini-2.0-flash Google) + Google key types
 *  0.70–1.00  Hold at final state
 */
export function useTokensScroll() {
  const [modelIdx, setModelIdx] = useState(0);
  const [modelProgress, setModelProgress] = useState(0);
  const [keyProgress, setKeyProgress] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const track = document.getElementById('tokens-track');
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

      if (progress < 0.20) {
        // Model 1: gemma3:4b (Local)
        setModelIdx(0);
        setModelProgress(Math.min(progress / 0.15, 1));
        setKeyProgress(0);
      } else if (progress < 0.45) {
        // Model 2: claude-sonnet-4.5 (Anthropic)
        setModelIdx(1);
        const p = progress - 0.20;
        setModelProgress(Math.min(p / 0.10, 1));
        setKeyProgress(p < 0.10 ? 0 : Math.min((p - 0.10) / 0.15, 1));
      } else if (progress < 0.70) {
        // Model 3: gemini-2.0-flash (Google)
        setModelIdx(2);
        const p = progress - 0.45;
        setModelProgress(Math.min(p / 0.10, 1));
        setKeyProgress(p < 0.10 ? 0 : Math.min((p - 0.10) / 0.15, 1));
      } else {
        // Hold at final state
        setModelIdx(2);
        setModelProgress(1);
        setKeyProgress(1);
      }
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll();

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { modelIdx, modelProgress, keyProgress };
}
