'use client';

import { useEffect, useRef, useState } from 'react';

interface UseScrollRevealOptions {
  threshold?: number;
  once?: boolean;
  /**
   * Shrinks (negative) or expands (positive) the observer's "viewport" so the
   * trigger fires later or earlier in the scroll. E.g. "0px 0px -20% 0px"
   * treats the viewport as ending 20% above its real bottom, so elements must
   * scroll further up before firing.
   */
  rootMargin?: string;
}

export function useScrollReveal(options: UseScrollRevealOptions = {}) {
  const { threshold = 0.2, once = true, rootMargin } = options;
  const [isVisible, setIsVisible] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const currentRef = ref.current;
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setIsVisible(true);
          if (once && currentRef) {
            observer.unobserve(currentRef);
          }
        } else if (!once) {
          setIsVisible(false);
        }
      },
      { threshold, rootMargin },
    );

    if (currentRef) {
      observer.observe(currentRef);
    }

    return () => {
      if (currentRef) {
        observer.unobserve(currentRef);
      }
    };
  }, [threshold, once, rootMargin]);

  return { ref, isVisible };
}
