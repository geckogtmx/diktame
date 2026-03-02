'use client';

import { useEffect, useRef, useState } from 'react';

export function useStickyScroll() {
  const [scrollPercentage, setScrollPercentage] = useState(0);
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleScroll = () => {
      if (!containerRef.current) return;

      const container = containerRef.current;
      const rect = container.getBoundingClientRect();
      const containerTop = rect.top;
      const containerHeight = rect.height;
      const windowHeight = window.innerHeight;

      // Calculate how much of the container has scrolled past the viewport
      let percentage = 0;

      if (containerTop <= 0 && rect.bottom > windowHeight) {
        // Container is actively scrolling through viewport
        percentage = Math.abs(containerTop) / (containerHeight - windowHeight);
      } else if (containerTop > 0) {
        // Container hasn't reached viewport yet
        percentage = 0;
      } else if (rect.bottom <= windowHeight) {
        // Container has completely passed through viewport
        percentage = 1;
      }

      setScrollPercentage(Math.min(1, Math.max(0, percentage)));
    };

    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return { containerRef, scrollPercentage };
}
