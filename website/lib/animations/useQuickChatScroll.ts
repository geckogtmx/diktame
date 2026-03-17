'use client';

import { useEffect, useState } from 'react';

/**
 * Hook for Quick Chat section scroll-driven animation.
 * Drives a 5-phase chat conversation replay based on scroll progress through 300vh section.
 *
 * Phases:
 *  0.00–0.20  User message 1 types in
 *  0.20–0.55  AI response 1 fades in
 *  0.55–0.65  User message 2 types in
 *  0.65–0.75  "Thinking..." spinner visible
 *  0.75–1.00  AI response 2 fades in
 */
export function useQuickChatScroll() {
  const [userMsg1Progress, setUserMsg1Progress] = useState(0);
  const [aiMsg1Progress, setAiMsg1Progress] = useState(0);
  const [userMsg2Progress, setUserMsg2Progress] = useState(0);
  const [thinkingVisible, setThinkingVisible] = useState(false);
  const [aiMsg2Progress, setAiMsg2Progress] = useState(0);

  useEffect(() => {
    const handleScroll = () => {
      const track = document.getElementById('quickchat-track');
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

      // Phase 1: User message 1 types in (0.00–0.20)
      setUserMsg1Progress(Math.min(progress / 0.20, 1));

      // Phase 2: AI response 1 fades in (0.20–0.55)
      setAiMsg1Progress(
        progress < 0.20 ? 0 : Math.min((progress - 0.20) / 0.35, 1)
      );

      // Phase 3: User message 2 types in (0.55–0.65)
      setUserMsg2Progress(
        progress < 0.55 ? 0 : Math.min((progress - 0.55) / 0.10, 1)
      );

      // Phase 4: Thinking spinner (0.65–0.75)
      setThinkingVisible(progress >= 0.65 && progress < 0.75);

      // Phase 5: AI response 2 fades in (0.75–1.00)
      setAiMsg2Progress(
        progress < 0.75 ? 0 : Math.min((progress - 0.75) / 0.25, 1)
      );
    };

    window.addEventListener('scroll', handleScroll);
    handleScroll(); // Initial calculation

    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return {
    userMsg1Progress,
    aiMsg1Progress,
    userMsg2Progress,
    thinkingVisible,
    aiMsg2Progress,
  };
}
