import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    const { data: posts, error } = await supabase
      .from('blog_posts')
      .select(
        'id, slug, title_en, meta_campaign_id, published_at, facebook_post_en, facebook_post_es, facebook_image_url_en, facebook_image_url_es, facebook_post_id',
      )
      .in('status', ['published', 'draft'])
      .order('published_at', { ascending: false, nullsFirst: false });

    if (error) {
      console.error('Campaigns fetch error:', error);
      return NextResponse.json({ error: 'Failed to fetch' }, { status: 500 });
    }

    return NextResponse.json(posts ?? []);
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Campaigns GET error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
