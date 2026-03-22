import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

/** Generate a formatted license key: DKTM-XXXX-XXXX-XXXX */
function generateLicenseKey(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let key = 'DKTM';
  for (let i = 0; i < 3; i++) {
    key += '-';
    for (let j = 0; j < 4; j++) {
      key += chars[Math.floor(Math.random() * chars.length)];
    }
  }
  return key;
}

export async function POST(request: Request) {
  try {
    const { supabase } = await requireAdmin(request);

    const body = await request.json();
    const { email, tier, walletCreditMicro } = body as {
      email: string;
      tier: string;
      walletCreditMicro?: number;
    };

    if (!email || !tier) {
      return NextResponse.json({ error: 'Missing email or tier' }, { status: 400 });
    }

    if (!['starter', 'power'].includes(tier)) {
      return NextResponse.json({ error: 'Invalid tier (must be starter or power)' }, { status: 400 });
    }

    const licenseKey = generateLicenseKey();

    // Look up user by email
    const { data: profile } = await supabase
      .from('profiles')
      .select('id')
      .eq('email', email)
      .maybeSingle();

    if (profile) {
      // User exists — create license immediately
      await supabase.from('licenses').insert({
        user_id: profile.id,
        key: licenseKey,
        status: 'active',
        tier,
      });

      // Grant wallet credit if specified
      if (walletCreditMicro && walletCreditMicro > 0) {
        await supabase.rpc('credit_wallet_balance', {
          p_user_id: profile.id,
          p_amount_micro: walletCreditMicro,
          p_type: 'GRANT',
          p_metadata: { reason: 'admin_gift', gifted_by: 'admin' },
          p_order_ref: `gift_${licenseKey}`,
        });
      }

      // Send email notification if Resend is configured
      if (process.env.RESEND_API_KEY) {
        try {
          await fetch('https://api.resend.com/emails', {
            method: 'POST',
            headers: {
              Authorization: `Bearer ${process.env.RESEND_API_KEY}`,
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              from: 'dIKta.me <noreply@dikta.me>',
              to: email,
              subject: 'You received a dIKta.me license!',
              html: `<p>You've been gifted a <strong>${tier}</strong> license for dIKta.me!</p><p>Your license key: <code>${licenseKey}</code></p><p>Sign in at <a href="https://dikta.me">dikta.me</a> to get started.</p>`,
            }),
          });
        } catch (err) {
          console.error('Failed to send gift email:', err);
        }
      }

      return NextResponse.json({
        success: true,
        pending: false,
        licenseKey,
        message: `License gifted to ${email}`,
      });
    } else {
      // User doesn't exist — create pending gift
      await supabase.from('pending_gifts').insert({
        email,
        license_key: licenseKey,
        tier,
        wallet_credit_micro: walletCreditMicro ?? 0,
      });

      return NextResponse.json({
        success: true,
        pending: true,
        licenseKey,
        message: `Gift pending — will be claimed when ${email} signs up`,
      });
    }
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Admin gift error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
