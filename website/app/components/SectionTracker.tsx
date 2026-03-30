'use client';

import { useEffect, useRef } from 'react';
import { trackSectionView } from '@/lib/analytics';

/**
 * Invisible wrapper that fires a GA4 event when a section scrolls into view.
 * Uses IntersectionObserver at 50% threshold. Fires once per session per section.
 */
export default function SectionTracker({
  sectionId,
  children,
}: {
  sectionId: string;
  children: React.ReactNode;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const fired = useRef(false);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !fired.current) {
          fired.current = true;
          trackSectionView(sectionId);
        }
      },
      { threshold: 0.5 },
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, [sectionId]);

  return <div ref={ref}>{children}</div>;
}
