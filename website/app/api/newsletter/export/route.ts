// GDPR data portability endpoint.
// GET /api/newsletter/export?token=UNSUBSCRIBE_TOKEN
// Returns the subscriber's row + all webhook events attributed to them as a
// downloadable JSON file. Authenticated via the persistent unsubscribe_token
// that already lives in every email footer — same trust model as the
// preference center.

import { NextResponse } from 'next/server';
import { createAdminClient } from '@/lib/supabase/server';

export const dynamic = 'force-dynamic';

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const token = searchParams.get('token');

  if (!token) {
    return NextResponse.json({ error: 'Missing token' }, { status: 400 });
  }

  const supabase = await createAdminClient();

  const { data: subscriber, error: subErr } = await supabase
    .from('newsletter_subscribers')
    .select('*')
    .eq('unsubscribe_token', token)
    .maybeSingle();

  if (subErr) {
    console.error('Export select error:', subErr);
    return NextResponse.json({ error: 'Database error' }, { status: 500 });
  }
  if (!subscriber) {
    return NextResponse.json({ error: 'Invalid token' }, { status: 404 });
  }

  const { data: events } = await supabase
    .from('newsletter_events')
    .select('event_type, resend_event_id, event_data, created_at')
    .eq('subscriber_id', subscriber.id)
    .order('created_at', { ascending: true });

  // Redact token fields before returning — they are meaningless to the user
  // and carrying them in an export file is an unnecessary risk if the export
  // is forwarded anywhere.
  const payload = {
    exported_at: new Date().toISOString(),
    subscriber: {
      ...subscriber,
      confirm_token: subscriber.confirm_token ? '[REDACTED]' : null,
      unsubscribe_token: '[REDACTED]',
    },
    events: events ?? [],
    meta: {
      data_controller: 'dIKta.me',
      address: 'San Francisco 1826-C-101, Del Valle, 03100, CDMX, México',
      contact: 'privacy@dikta.me',
      note: 'This is a complete copy of the personal data we hold about this subscription. To delete it, use the preference center link in any newsletter email footer.',
    },
  };

  const filename = `dikta-me-newsletter-data-${subscriber.id}.json`;
  return new NextResponse(JSON.stringify(payload, null, 2), {
    status: 200,
    headers: {
      'Content-Type': 'application/json',
      'Content-Disposition': `attachment; filename="${filename}"`,
      'Cache-Control': 'no-store',
    },
  });
}
