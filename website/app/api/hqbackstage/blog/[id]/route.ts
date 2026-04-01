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

    const updates: Record<string, unknown> = {
      ...body,
      updated_at: new Date().toISOString(),
    };

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
