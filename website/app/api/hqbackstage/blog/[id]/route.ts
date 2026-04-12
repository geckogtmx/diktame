import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { supabase } = await requireAdmin(request);
    const { id } = await params;

    const { data: post, error } = await supabase
      .from('blog_posts')
      .select('*')
      .eq('id', id)
      .single();

    if (error) {
      return NextResponse.json({ error: 'Post not found' }, { status: 404 });
    }

    return NextResponse.json(post);
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Blog GET error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function PATCH(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { supabase } = await requireAdmin(request);
    const { id } = await params;
    const body = await request.json();

    // Whitelist allowed fields to prevent mass assignment
    const allowedFields = [
      'title_en', 'title_es', 'hook_en', 'hook_es',
      'body_en', 'body_es', 'closing_en', 'closing_es',
      'status', 'voice_id', 'voice_label_en', 'voice_label_es',
      'image_prompt', 'image_anchor', 'thematic_arc',
      'headlines_used', 'newsletter_sources', 'run_date',
      'linkedin_url', 'twitter_url', 'twitter_hook_en', 'twitter_hook_es',
      'facebook_post_en', 'facebook_post_es', 'facebook_post_en_b', 'facebook_post_es_b',
      'facebook_image_url_en', 'facebook_image_url_es', 'facebook_post_id', 'meta_campaign_id',
    ];

    const updates: Record<string, unknown> = {};
    for (const field of allowedFields) {
      if (field in body) updates[field] = body[field];
    }
    updates.updated_at = new Date().toISOString();

    // Validate status if provided
    if (body.status) {
      const validStatuses = ['draft', 'published', 'archived'];
      if (!validStatuses.includes(body.status)) {
        return NextResponse.json({ error: 'Invalid status value' }, { status: 400 });
      }
    }

    // Handle published_at based on status changes
    if (body.status === 'published') {
      updates.published_at = new Date().toISOString();
    } else if (body.status && body.status !== 'published') {
      updates.published_at = null;
    }

    const { data: post, error } = await supabase
      .from('blog_posts')
      .update(updates)
      .eq('id', id)
      .select()
      .single();

    if (error) {
      console.error('Blog PATCH error:', error);
      return NextResponse.json({ error: 'Failed to update post' }, { status: 500 });
    }

    return NextResponse.json(post);
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Blog PATCH error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
