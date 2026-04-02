'use client';

import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { Link } from '@/i18n/navigation';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

const MAX_IMAGE_WIDTH = 1920;
const COMPRESSION_QUALITY = 0.82;

async function compressImage(file: File): Promise<Blob> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement('canvas');
      let { width, height } = img;
      if (width > MAX_IMAGE_WIDTH) {
        height = Math.round((height * MAX_IMAGE_WIDTH) / width);
        width = MAX_IMAGE_WIDTH;
      }
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext('2d');
      if (!ctx) return reject(new Error('Canvas not supported'));
      ctx.drawImage(img, 0, 0, width, height);
      canvas.toBlob(
        (blob) => (blob ? resolve(blob) : reject(new Error('Compression failed'))),
        'image/webp',
        COMPRESSION_QUALITY,
      );
    };
    img.onerror = () => reject(new Error('Failed to load image'));
    img.src = URL.createObjectURL(file);
  });
}

interface BlogPost {
  id: string;
  slug: string;
  status: string;
  title_en: string | null;
  hook_en: string | null;
  body_en: string | null;
  closing_en: string | null;
  title_es: string | null;
  hook_es: string | null;
  body_es: string | null;
  closing_es: string | null;
  voice_id: string | null;
  voice_label_en: string | null;
  voice_label_es: string | null;
  image_url_en: string | null;
  image_url_es: string | null;
  image_prompt: string | null;
  image_anchor: string | null;
  thematic_arc: string | null;
  headlines_used: string[] | null;
  newsletter_sources: string[] | null;
  run_date: string | null;
  created_at: string;
  published_at: string | null;
  updated_at: string | null;
}

export function BlogPostEditor({ post: initialPost }: { post: BlogPost }) {
  const router = useRouter();
  const [post, setPost] = useState(initialPost);
  const [publishing, setPublishing] = useState(false);
  const [uploadingEn, setUploadingEn] = useState(false);
  const [uploadingEs, setUploadingEs] = useState(false);
  const [metadataOpen, setMetadataOpen] = useState(false);

  const statusColors: Record<string, string> = {
    draft: 'bg-yellow-500/20 text-yellow-300',
    published: 'bg-green-500/20 text-green-300',
    archived: 'bg-gray-500/20 text-gray-300',
  };

  const handlePublishToggle = async () => {
    const newStatus = post.status === 'published' ? 'draft' : 'published';
    const action = newStatus === 'published' ? 'publish' : 'unpublish';

    if (!confirm(`Are you sure you want to ${action} this post?`)) return;

    setPublishing(true);
    try {
      const res = await fetch(`/api/hqbackstage/blog/${post.id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ status: newStatus }),
      });
      if (res.ok) {
        const data = await res.json();
        setPost((prev) => ({
          ...prev,
          status: data.status ?? newStatus,
          published_at: data.published_at ?? prev.published_at,
        }));
        router.refresh();
      } else {
        const err = await res.text();
        alert(`Failed to ${action}: ${err}`);
      }
    } catch (e) {
      alert(`Failed to ${action}: ${e}`);
    } finally {
      setPublishing(false);
    }
  };

  const handleImageDrop = useCallback(
    async (lang: 'en' | 'es', e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      const file = e.dataTransfer.files[0];
      if (!file || !file.type.startsWith('image/')) return;

      const setter = lang === 'en' ? setUploadingEn : setUploadingEs;
      setter(true);

      try {
        const compressed = await compressImage(file);
        const formData = new FormData();
        formData.append('image', compressed, `${lang}.webp`);
        formData.append('lang', lang);

        const res = await fetch(`/api/hqbackstage/blog/${post.id}/image`, {
          method: 'POST',
          body: formData,
        });

        if (res.ok) {
          const data = await res.json();
          setPost((prev) => ({
            ...prev,
            [`image_url_${lang}`]: data.url,
          }));
          router.refresh();
        } else {
          const err = await res.text();
          alert(`Upload failed: ${err}`);
        }
      } catch (err) {
        alert(`Upload failed: ${err}`);
      } finally {
        setter(false);
      }
    },
    [post.id, router],
  );

  return (
    <div className="space-y-8">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link
            href="/hqbackstage/blog"
            className="text-gray-400 hover:text-white transition-colors text-sm"
          >
            &larr; Back to Blog
          </Link>
          <h1 className="text-2xl font-bold">{post.title_en || 'Untitled'}</h1>
          <span
            className={`inline-block px-2 py-0.5 rounded text-xs ${statusColors[post.status] ?? 'bg-gray-500/20 text-gray-300'}`}
          >
            {post.status}
          </span>
        </div>
        <button
          onClick={handlePublishToggle}
          disabled={publishing}
          className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50 ${
            post.status === 'published'
              ? 'bg-yellow-500/20 text-yellow-300 hover:bg-yellow-500/30'
              : 'bg-green-500/20 text-green-300 hover:bg-green-500/30'
          }`}
        >
          {publishing
            ? 'Updating...'
            : post.status === 'published'
              ? 'Unpublish'
              : 'Publish'}
        </button>
      </div>

      {/* Image Upload Zones */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <ImageDropZone
          lang="en"
          imageUrl={post.image_url_en}
          uploading={uploadingEn}
          onDrop={(e) => handleImageDrop('en', e)}
        />
        <ImageDropZone
          lang="es"
          imageUrl={post.image_url_es}
          uploading={uploadingEs}
          onDrop={(e) => handleImageDrop('es', e)}
        />
      </div>

      {/* Image Prompt */}
      {post.image_prompt && (
        <div className="rounded-lg border border-white/10 bg-white/5 p-4">
          <h3 className="text-sm font-medium text-gray-400 mb-2">Image Prompt</h3>
          <p className="text-sm text-gray-300 whitespace-pre-wrap">{post.image_prompt}</p>
        </div>
      )}

      {/* Two-Column Content */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ContentColumn
          lang="EN"
          title={post.title_en}
          hook={post.hook_en}
          body={post.body_en}
          closing={post.closing_en}
        />
        <ContentColumn
          lang="ES"
          title={post.title_es}
          hook={post.hook_es}
          body={post.body_es}
          closing={post.closing_es}
        />
      </div>

      {/* Metadata (collapsible) */}
      <div className="rounded-lg border border-white/10">
        <button
          onClick={() => setMetadataOpen(!metadataOpen)}
          className="w-full flex items-center justify-between px-4 py-3 text-sm font-medium text-gray-400 hover:text-white transition-colors"
        >
          <span>Metadata</span>
          <span className="text-xs">{metadataOpen ? '▲' : '▼'}</span>
        </button>
        {metadataOpen && (
          <div className="border-t border-white/10 px-4 py-4 space-y-3 text-sm">
            <MetadataRow label="Slug" value={post.slug} />
            <MetadataRow label="Voice ID" value={post.voice_id} />
            <MetadataRow label="Thematic Arc" value={post.thematic_arc} />
            <MetadataRow
              label="Run Date"
              value={
                post.run_date
                  ? new Date(post.run_date).toLocaleDateString('en-US', {
                      month: 'long',
                      day: 'numeric',
                      year: 'numeric',
                    })
                  : null
              }
            />
            <MetadataRow label="Image Anchor" value={post.image_anchor} />
            {post.headlines_used && post.headlines_used.length > 0 && (
              <div>
                <span className="text-gray-500">Headlines Used:</span>
                <ul className="mt-1 space-y-1">
                  {post.headlines_used.map((h, i) => (
                    <li key={i} className="text-gray-300 text-xs pl-3">
                      &bull; {h}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            <MetadataRow
              label="Created"
              value={new Date(post.created_at).toLocaleString('en-US')}
            />
            <MetadataRow
              label="Published"
              value={post.published_at ? new Date(post.published_at).toLocaleString('en-US') : null}
            />
            <MetadataRow
              label="Updated"
              value={post.updated_at ? new Date(post.updated_at).toLocaleString('en-US') : null}
            />
          </div>
        )}
      </div>
    </div>
  );
}

function ContentColumn({
  lang,
  title,
  hook,
  body,
  closing,
}: {
  lang: string;
  title: string | null;
  hook: string | null;
  body: string | null;
  closing: string | null;
}) {
  return (
    <div className="rounded-lg border border-white/10 bg-white/5 p-5 space-y-4">
      <h2 className="text-lg font-semibold text-gray-300">{lang}</h2>

      <div>
        <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Title</h3>
        <p className="text-white font-medium">{title || '—'}</p>
      </div>

      {hook && (
        <div>
          <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Hook</h3>
          <p className="text-gray-300 italic">{hook}</p>
        </div>
      )}

      {body && (
        <div>
          <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">Body</h3>
          <div className="prose prose-invert prose-sm max-w-none text-gray-300 [&_h1]:text-white [&_h2]:text-white [&_h3]:text-white [&_strong]:text-white [&_a]:text-blue-400">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{body}</ReactMarkdown>
          </div>
        </div>
      )}

      {closing && (
        <div>
          <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wider mb-1">
            Closing
          </h3>
          <p className="text-gray-300">{closing}</p>
        </div>
      )}

      {!title && !hook && !body && !closing && (
        <p className="text-gray-500 italic">No content yet</p>
      )}
    </div>
  );
}

function ImageDropZone({
  lang,
  imageUrl,
  uploading,
  onDrop,
}: {
  lang: string;
  imageUrl: string | null;
  uploading: boolean;
  onDrop: (e: React.DragEvent<HTMLDivElement>) => void;
}) {
  const [dragOver, setDragOver] = useState(false);

  return (
    <div
      onDrop={(e) => {
        setDragOver(false);
        onDrop(e);
      }}
      onDragOver={(e) => {
        e.preventDefault();
        setDragOver(true);
      }}
      onDragLeave={() => setDragOver(false)}
      className={`rounded-lg border-2 border-dashed p-4 transition-colors ${
        dragOver
          ? 'border-blue-400 bg-blue-500/10'
          : imageUrl
            ? 'border-white/10 bg-white/5'
            : 'border-white/20 bg-white/5'
      }`}
    >
      <h3 className="text-sm font-medium text-gray-400 mb-2">
        Image ({lang.toUpperCase()})
      </h3>
      {uploading ? (
        <div className="flex items-center justify-center py-8 text-gray-400 text-sm">
          Uploading...
        </div>
      ) : imageUrl ? (
        <img
          src={imageUrl}
          alt={`Blog image ${lang.toUpperCase()}`}
          className="rounded-md max-h-48 object-cover w-full"
        />
      ) : (
        <div className="flex items-center justify-center py-8 text-gray-500 text-sm">
          Drop {lang.toUpperCase()} image here
        </div>
      )}
    </div>
  );
}

function MetadataRow({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="flex gap-3">
      <span className="text-gray-500 shrink-0">{label}:</span>
      <span className="text-gray-300 break-all">{value || '—'}</span>
    </div>
  );
}
