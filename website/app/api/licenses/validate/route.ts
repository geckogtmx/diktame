import { createClient } from '@supabase/supabase-js';
import { NextResponse } from 'next/server';

export async function GET(request: Request) {
  const url = new URL(request.url);
  const key = url.searchParams.get('key');
  const machineId = url.searchParams.get('machine_id');

  if (!key) {
    return NextResponse.json({ error: 'Missing key parameter' }, { status: 400 });
  }

  try {
    // Service-role client to read licenses (RLS only allows users to read own)
    const supabase = createClient(
      process.env.NEXT_PUBLIC_SUPABASE_URL!,
      process.env.SUPABASE_SERVICE_ROLE_KEY!,
      { auth: { persistSession: false } }
    );

    const { data: license, error } = await supabase
      .from('licenses')
      .select('id, user_id, key, status, tier, machine_id, expires_at')
      .eq('key', key)
      .single();

    if (error || !license) {
      return NextResponse.json({ valid: false, error: 'License not found' }, { status: 404 });
    }

    if (license.status !== 'active') {
      return NextResponse.json({ valid: false, error: `License ${license.status}`, tier: license.tier });
    }

    if (license.expires_at && new Date(license.expires_at) < new Date()) {
      // Mark as expired
      await supabase.from('licenses').update({ status: 'expired' }).eq('id', license.id);
      return NextResponse.json({ valid: false, error: 'License expired', tier: license.tier });
    }

    // Bind to machine on first use
    if (machineId && !license.machine_id) {
      await supabase.from('licenses').update({ machine_id: machineId }).eq('id', license.id);
      license.machine_id = machineId;
    }

    // Check machine binding
    if (license.machine_id && machineId && license.machine_id !== machineId) {
      return NextResponse.json({
        valid: false,
        error: 'License bound to different machine',
        tier: license.tier,
        bound_machine_id: license.machine_id,
      });
    }

    return NextResponse.json({
      valid: true,
      tier: license.tier,
      machine_id: license.machine_id,
      expires_at: license.expires_at,
    });
  } catch (error) {
    console.error('License validation error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
