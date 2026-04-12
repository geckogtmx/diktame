'use client';

import { useEffect } from 'react';
import { trackFbViewContent } from '@/app/components/MetaPixel';

export function FbViewContent({ slug, title }: { slug: string; title: string }) {
  useEffect(() => {
    trackFbViewContent({ content_name: title, content_ids: [slug], content_category: 'blog' });
  }, [slug, title]);

  return null;
}
