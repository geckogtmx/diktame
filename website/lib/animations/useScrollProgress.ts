'use client';

import { useEffect, useRef, useState } from 'react';

export function useScrollProgress() {
  const [progress, setProgress] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleScroll = () => {
      if (!containerRef.current) return;

      const container = containerRef.current;
      const { top, bottom, height } = container.getBoundingClientRect();
      const viewportHeight = window.innerHeight;

      // Calculate progress: 0 = top of viewport, 1 = bottom of viewport
      if (bottom < 0) {
        setProgress(1);
      } else if (top > viewportHeight) {
        setProgress(0);
      } else {
        const visibleTop = Math.max(0, -top);
        const calculatedProgress = visibleTop / height;
        setProgress(Math.min(1, Math.max(0, calculatedProgress)));
      }
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { containerRef, progress };
}
