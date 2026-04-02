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
  linkedin_url: string | null;
  twitter_url: string | null;
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
        <div className="flex items-center gap-3">
          {post.status === 'published' && (
            <a
              href={`/blog/${post.slug}`}
              target="_blank"
              rel="noopener noreferrer"
              className="px-4 py-2 rounded-lg text-sm font-medium bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
            >
              View
            </a>
          )}
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

      {/* Hook for X + Image Prompt */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <XHookCard hookEn={post.hook_en} hookEs={post.hook_es} slug={post.slug} />
        {post.image_prompt && (
          <div className="rounded-lg border border-white/10 bg-white/5 p-4">
            <h3 className="text-sm font-medium text-gray-400 mb-2">Image Prompt</h3>
            <p className="text-sm text-gray-300 whitespace-pre-wrap">{post.image_prompt}</p>
          </div>
        )}
      </div>

      {/* Two-Column Content */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <ContentColumn
          lang="EN"
          title={post.title_en}
          hook={post.hook_en}
          body={post.body_en}
          closing={post.closing_en}
          postId={post.id}
          onUpdate={(fields) => setPost((prev) => ({ ...prev, ...fields }))}
        />
        <ContentColumn
          lang="ES"
          title={post.title_es}
          hook={post.hook_es}
          body={post.body_es}
          closing={post.closing_es}
          postId={post.id}
          onUpdate={(fields) => setPost((prev) => ({ ...prev, ...fields }))}
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
            <div className="flex gap-3 items-start">
              <span className="text-gray-500 shrink-0">LinkedIn:</span>
              <SocialUrlField
                value={post.linkedin_url}
                fieldKey="linkedin_url"
                placeholder="https://www.linkedin.com/pulse/..."
                postId={post.id}
                onSave={(url) => setPost((prev) => ({ ...prev, linkedin_url: url }))}
              />
            </div>
            <div className="flex gap-3 items-start">
              <span className="text-gray-500 shrink-0">X / Twitter:</span>
              <SocialUrlField
                value={post.twitter_url}
                fieldKey="twitter_url"
                placeholder="https://x.com/yourhandle/status/..."
                postId={post.id}
                onSave={(url) => setPost((prev) => ({ ...prev, twitter_url: url }))}
              />
            </div>
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
  postId,
  onUpdate,
}: {
  lang: string;
  title: string | null;
  hook: string | null;
  body: string | null;
  closing: string | null;
  postId: string;
  onUpdate: (fields: Record<string, string>) => void;
}) {
  const suffix = lang === 'EN' ? 'en' : 'es';

  return (
    <div className="rounded-lg border border-white/10 bg-white/5 p-5 space-y-4">
      <h2 className="text-lg font-semibold text-gray-300">{lang}</h2>

      <EditableField
        label="Title"
        value={title}
        fieldKey={`title_${suffix}`}
        postId={postId}
        onSave={onUpdate}
        inputType="input"
        displayClass="text-white font-medium"
      />

      <EditableField
        label="Hook"
        value={hook}
        fieldKey={`hook_${suffix}`}
        postId={postId}
        onSave={onUpdate}
        inputType="textarea"
        rows={3}
        displayClass="text-gray-300 italic"
      />

      {body && (
        <EditableField
          label="Body"
          value={body}
          fieldKey={`body_${suffix}`}
          postId={postId}
          onSave={onUpdate}
          inputType="textarea"
          rows={20}
          renderMarkdown
        />
      )}

      <EditableField
        label="Closing"
        value={closing}
        fieldKey={`closing_${suffix}`}
        postId={postId}
        onSave={onUpdate}
        inputType="textarea"
        rows={6}
        displayClass="text-gray-300"
      />

      {!title && !hook && !body && !closing && (
        <p className="text-gray-500 italic">No content yet</p>
      )}
    </div>
  );
}

function EditableField({
  label,
  value,
  fieldKey,
  postId,
  onSave,
  inputType = 'input',
  rows = 3,
  displayClass = 'text-gray-300',
  renderMarkdown = false,
}: {
  label: string;
  value: string | null;
  fieldKey: string;
  postId: string;
  onSave: (fields: Record<string, string>) => void;
  inputType?: 'input' | 'textarea';
  rows?: number;
  displayClass?: string;
  renderMarkdown?: boolean;
}) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(value ?? '');
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      const res = await fetch(`/api/hqbackstage/blog/${postId}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ [fieldKey]: draft }),
      });
      if (res.ok) {
        onSave({ [fieldKey]: draft });
        setEditing(false);
      } else {
        alert('Save failed');
      }
    } catch {
      alert('Save failed');
    } finally {
      setSaving(false);
    }
  };

  const handleCancel = () => {
    setDraft(value ?? '');
    setEditing(false);
  };

  if (!value && !editing) return null;

  return (
    <div>
      <div className="flex items-center gap-2 mb-1">
        <h3 className="text-xs font-medium text-gray-500 uppercase tracking-wider">{label}</h3>
        {!editing && (
          <button
            onClick={() => { setDraft(value ?? ''); setEditing(true); }}
            className="text-gray-600 hover:text-gray-300 transition-colors"
            title={`Edit ${label}`}
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125" />
            </svg>
          </button>
        )}
      </div>
      {editing ? (
        <div className="space-y-2">
          {inputType === 'input' ? (
            <input
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              className="w-full bg-white/5 border border-white/20 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:border-blue-400"
            />
          ) : (
            <textarea
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              rows={rows}
              className="w-full bg-white/5 border border-white/20 rounded-lg px-3 py-2 text-white text-sm font-mono focus:outline-none focus:border-blue-400 resize-y"
            />
          )}
          <div className="flex gap-2">
            <button
              onClick={handleSave}
              disabled={saving}
              className="px-3 py-1 rounded text-xs font-medium bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 disabled:opacity-50"
            >
              {saving ? 'Saving...' : 'Save'}
            </button>
            <button
              onClick={handleCancel}
              className="px-3 py-1 rounded text-xs font-medium bg-white/5 text-gray-400 hover:bg-white/10"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : renderMarkdown ? (
        <div className="prose prose-invert prose-sm max-w-none text-gray-300 [&_h1]:text-white [&_h2]:text-white [&_h3]:text-white [&_strong]:text-white [&_a]:text-blue-400">
          <ReactMarkdown remarkPlugins={[remarkGfm]}>{value ?? ''}</ReactMarkdown>
        </div>
      ) : (
        <p className={displayClass}>{value}</p>
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

function makeXHook(hook: string | null, slug: string, maxLen = 280): string {
  if (!hook) return '';
  const url = `https://dikta.me/blog/${slug}`;
  const linkLen = url.length + 1; // +1 for newline
  const available = maxLen - linkLen;
  let text = hook;
  if (text.length > available) {
    text = text.slice(0, available - 1).replace(/\s+\S*$/, '') + '\u2026';
  }
  return `${text}\n${url}`;
}

function XHookCard({ hookEn, hookEs, slug }: { hookEn: string | null; hookEs: string | null; slug: string }) {
  const [lang, setLang] = useState<'en' | 'es'>('en');
  const [copied, setCopied] = useState(false);

  const hook = lang === 'en' ? hookEn : hookEs;
  const xText = makeXHook(hook, slug);
  const charCount = xText.length;

  const handleCopy = async () => {
    await navigator.clipboard.writeText(xText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="rounded-lg border border-white/10 bg-white/5 p-4">
      <div className="flex items-center justify-between mb-2">
        <h3 className="text-sm font-medium text-gray-400">Hook for X</h3>
        <div className="flex items-center gap-2">
          <button
            onClick={() => setLang(lang === 'en' ? 'es' : 'en')}
            className="text-xs px-2 py-0.5 rounded border border-white/10 text-gray-400 hover:text-white hover:border-white/30 transition-colors"
          >
            {lang === 'en' ? 'ES' : 'EN'}
          </button>
          <button
            onClick={handleCopy}
            className="text-xs px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
          >
            {copied ? 'Copied' : 'Copy'}
          </button>
        </div>
      </div>
      <p className="text-sm text-gray-300 whitespace-pre-wrap mb-2">{xText}</p>
      <p className={`text-xs ${charCount > 280 ? 'text-red-400' : 'text-gray-500'}`}>
        {charCount}/280
      </p>
    </div>
  );
}

function SocialUrlField({
  value,
  fieldKey,
  placeholder,
  postId,
  onSave,
}: {
  value: string | null;
  fieldKey: string;
  placeholder: string;
  postId: string;
  onSave: (url: string) => void;
}) {
  const [editing, setEditing] = useState(!value);
  const [draft, setDraft] = useState(value ?? '');
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    setSaving(true);
    try {
      const res = await fetch(`/api/hqbackstage/blog/${postId}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ [fieldKey]: draft || null }),
      });
      if (res.ok) {
        onSave(draft);
        if (draft) setEditing(false);
      } else {
        alert('Save failed');
      }
    } catch {
      alert('Save failed');
    } finally {
      setSaving(false);
    }
  };

  if (!editing && value) {
    return (
      <div className="flex items-center gap-2">
        <a href={value} target="_blank" rel="noopener noreferrer" className="text-blue-400 hover:text-blue-300 text-sm break-all">
          {value.length > 60 ? value.slice(0, 60) + '...' : value}
        </a>
        <button onClick={() => { setDraft(value); setEditing(true); }} className="text-gray-600 hover:text-gray-300">
          <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125" />
          </svg>
        </button>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 flex-1">
      <input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        placeholder={placeholder}
        className="flex-1 bg-white/5 border border-white/20 rounded px-2 py-1 text-sm text-white focus:outline-none focus:border-blue-400"
      />
      <button onClick={handleSave} disabled={saving} className="px-2 py-1 rounded text-xs bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 disabled:opacity-50">
        {saving ? '...' : 'Save'}
      </button>
      {value && (
        <button onClick={() => setEditing(false)} className="px-2 py-1 rounded text-xs bg-white/5 text-gray-400 hover:bg-white/10">
          Cancel
        </button>
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
