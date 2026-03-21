// Trial usage endpoint — DEPRECATED.
// The wallet system (wallet-proxy Edge Function) handles usage tracking.

import { NextResponse } from 'next/server';

export async function POST() {
  return NextResponse.json(
    {
      error: 'Gone',
      message:
        'Trial usage tracking has been replaced by the wallet system. ' +
        'Use the wallet-proxy Edge Function for API access.',
    },
    { status: 410 }
  );
}
