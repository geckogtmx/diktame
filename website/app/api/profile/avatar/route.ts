import { createApiClient } from '@/lib/supabase/api';
import { NextResponse } from 'next/server';

const MAX_FILE_SIZE = 2 * 1024 * 1024; // 2MB
const ALLOWED_TYPES = ['image/webp', 'image/png', 'image/jpeg'];

export async function POST(request: Request) {
  try {
    const supabase = await createApiClient(request);

    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    const formData = await request.formData();
    const file = formData.get('avatar');

    if (!file || !(file instanceof Blob)) {
      return NextResponse.json({ error: 'No avatar file provided' }, { status: 400 });
    }

    if (file.size > MAX_FILE_SIZE) {
      return NextResponse.json({ error: 'File too large (max 2MB)' }, { status: 400 });
    }

    if (!ALLOWED_TYPES.includes(file.type)) {
      return NextResponse.json({ error: 'Invalid file type. Use WebP, PNG, or JPEG.' }, { status: 400 });
    }

    const fileName = `${user.id}.webp`;
    const buffer = Buffer.from(await file.arrayBuffer());

    const { error: uploadError } = await supabase.storage
      .from('avatars')
      .upload(fileName, buffer, {
        contentType: file.type,
        upsert: true,
      });

    if (uploadError) {
      console.error('Avatar upload error:', uploadError);
      return NextResponse.json({ error: 'Failed to upload avatar' }, { status: 500 });
    }

    // Build public URL with cache-bust param
    const publicUrl = `${process.env.NEXT_PUBLIC_SUPABASE_URL}/storage/v1/object/public/avatars/${fileName}?t=${Date.now()}`;

    // Update profile with avatar URL
    const { error: updateError } = await supabase
      .from('profiles')
      .update({ avatar_url: publicUrl, updated_at: new Date().toISOString() })
      .eq('id', user.id);

    if (updateError) {
      console.error('Avatar profile update error:', updateError);
      return NextResponse.json({ error: 'Failed to update profile' }, { status: 500 });
    }

    return NextResponse.json({ avatarUrl: publicUrl });
  } catch (error) {
    console.error('Avatar POST error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}

export async function DELETE(request: Request) {
  try {
    const supabase = await createApiClient(request);

    const {
      data: { user },
      error: authError,
    } = await supabase.auth.getUser();

    if (authError || !user) {
      return NextResponse.json({ error: 'Unauthorized' }, { status: 401 });
    }

    // Remove from storage (ignore errors — file may not exist)
    await supabase.storage.from('avatars').remove([`${user.id}.webp`]);

    // Clear avatar_url in profile
    const { error: updateError } = await supabase
      .from('profiles')
      .update({ avatar_url: null, updated_at: new Date().toISOString() })
      .eq('id', user.id);

    if (updateError) {
      console.error('Avatar delete profile update error:', updateError);
      return NextResponse.json({ error: 'Failed to update profile' }, { status: 500 });
    }

    return NextResponse.json({ success: true });
  } catch (error) {
    console.error('Avatar DELETE error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
