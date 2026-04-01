import { requireAdmin } from '@/lib/admin';
import { NextResponse } from 'next/server';

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
const ALLOWED_TYPES = ['image/webp', 'image/png', 'image/jpeg'];

export async function POST(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
) {
  try {
    const { supabase } = await requireAdmin(request);
    const { id } = await params;

    const formData = await request.formData();
    const file = formData.get('image');
    const lang = formData.get('lang') as string;

    if (!file || !(file instanceof Blob)) {
      return NextResponse.json({ error: 'No image file provided' }, { status: 400 });
    }

    if (file.size > MAX_FILE_SIZE) {
      return NextResponse.json({ error: 'File too large (max 5MB)' }, { status: 400 });
    }

    if (!ALLOWED_TYPES.includes(file.type)) {
      return NextResponse.json({ error: 'Invalid file type. Use WebP, PNG, or JPEG.' }, { status: 400 });
    }

    if (!lang || (lang !== 'en' && lang !== 'es')) {
      return NextResponse.json({ error: 'lang must be "en" or "es"' }, { status: 400 });
    }

    // Get the post's slug for the file name
    const { data: post, error: fetchError } = await supabase
      .from('blog_posts')
      .select('slug')
      .eq('id', id)
      .single();

    if (fetchError || !post) {
      return NextResponse.json({ error: 'Post not found' }, { status: 404 });
    }

    // Validate slug format to prevent path traversal
    if (!/^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(post.slug)) {
      return NextResponse.json({ error: 'Post has invalid slug format' }, { status: 400 });
    }

    const fileName = `${post.slug}-${lang}.webp`;
    const buffer = Buffer.from(await file.arrayBuffer());

    const { error: uploadError } = await supabase.storage
      .from('blog-images')
      .upload(fileName, buffer, {
        contentType: file.type,
        upsert: true,
      });

    if (uploadError) {
      console.error('Blog image upload error:', uploadError);
      return NextResponse.json({ error: 'Failed to upload image' }, { status: 500 });
    }

    const publicUrl = `${process.env.NEXT_PUBLIC_SUPABASE_URL}/storage/v1/object/public/blog-images/${fileName}?t=${Date.now()}`;

    // Update the appropriate image column based on language
    const column = lang === 'en' ? 'image_url_en' : 'image_url_es';
    const { error: updateError } = await supabase
      .from('blog_posts')
      .update({ [column]: publicUrl, updated_at: new Date().toISOString() })
      .eq('id', id);

    if (updateError) {
      console.error('Blog image update error:', updateError);
      return NextResponse.json({ error: 'Failed to update post' }, { status: 500 });
    }

    return NextResponse.json({ url: publicUrl });
  } catch (error) {
    if (error instanceof Response) return error;
    console.error('Blog image POST error:', error);
    return NextResponse.json({ error: 'Internal server error' }, { status: 500 });
  }
}
