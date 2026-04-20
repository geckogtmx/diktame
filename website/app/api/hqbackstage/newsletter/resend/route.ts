import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

// POST /api/hqbackstage/newsletter/resend
// Body: { post_id: string, locale: 'en' | 'es', force?: boolean }
//
// Invokes the newsletter-send edge function for the given (post, locale).
// - force=false (default): only proceeds if no newsletter_sends row exists
//   for that (post_id, locale). Safe for backfilling missed sends.
// - force=true: deletes any existing row first, then re-invokes. Meant for
//   retrying failed/partial sends. Double-email risk — UI must confirm.
export async function POST(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);
    const body = await request.json().catch(() => ({}));

    const postId = body.post_id;
    const locale = body.locale;
    const force = body.force === true;

    if (typeof postId !== 'string' || !postId) {
      return NextResponse.json({ error: 'post_id required' }, { status: 400 });
    }
    if (locale !== 'en' && locale !== 'es') {
      return NextResponse.json({ error: "locale must be 'en' or 'es'" }, { status: 400 });
    }

    const { data: existing, error: lookupErr } = await supabase
      .from('newsletter_sends')
      .select('id, status, started_at')
      .eq('post_id', postId)
      .eq('locale', locale)
      .maybeSingle();

    if (lookupErr) {
      console.error('resend: send lookup error', lookupErr);
      return NextResponse.json({ error: 'Database error' }, { status: 500 });
    }

    if (existing && !force) {
      return NextResponse.json(
        {
          error: 'A send row already exists for this post/locale. Pass force:true to override.',
          existing,
        },
        { status: 409 },
      );
    }

    if (existing && force) {
      const { error: delErr } = await supabase
        .from('newsletter_sends')
        .delete()
        .eq('id', existing.id);
      if (delErr) {
        console.error('resend: delete existing row error', delErr);
        return NextResponse.json({ error: 'Could not clear existing send row' }, { status: 500 });
      }
    }

    const functionsUrl = `${process.env.NEXT_PUBLIC_SUPABASE_URL}/functions/v1/newsletter-send`;
    const authHeader = `Bearer ${process.env.SUPABASE_SERVICE_ROLE_KEY}`;
    const res = await fetch(functionsUrl, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: authHeader },
      body: JSON.stringify({ post_id: postId, locale }),
    });

    const payload = await res.json().catch(() => ({}));
    if (!res.ok) {
      console.error(`resend: newsletter-send returned ${res.status}`, payload);
      return NextResponse.json(
        { error: 'newsletter-send failed', status: res.status, upstream: payload },
        { status: 502 },
      );
    }

    return NextResponse.json(payload);
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Resend POST error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
