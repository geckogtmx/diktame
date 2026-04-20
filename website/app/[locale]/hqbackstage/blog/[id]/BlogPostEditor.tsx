'use client';

import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { Link } from '@/i18n/navigation';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import Cropper from 'react-easy-crop';
import type { Area, Point } from 'react-easy-crop';
import ResendButton from '../../newsletter/ResendButton';

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
  slug_es: string | null;
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
  twitter_hook_en: string | null;
  twitter_hook_es: string | null;
  facebook_post_en: string | null;
  facebook_post_es: string | null;
  facebook_post_en_b: string | null;
  facebook_post_es_b: string | null;
  facebook_image_url_en: string | null;
  facebook_image_url_es: string | null;
  facebook_post_id: string | null;
  meta_campaign_id: string | null;
  headlines_used: string[] | null;
  newsletter_sources: string[] | null;
  run_date: string | null;
  audio_url_en: string | null;
  audio_url_es: string | null;
  created_at: string;
  published_at: string | null;
  updated_at: string | null;
}

export function BlogPostEditor({
  post: initialPost,
  subscriberCounts = { en: 0, es: 0 },
}: {
  post: BlogPost;
  subscriberCounts?: { en: number; es: number };
}) {
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

  const isFirstPublish = post.status !== 'published' && post.published_at == null;

  const handlePublishToggle = async () => {
    const newStatus = post.status === 'published' ? 'draft' : 'published';
    const action = newStatus === 'published' ? 'publish' : 'unpublish';

    const confirmMsg =
      newStatus === 'published' && isFirstPublish
        ? `Publishing will send the newsletter to ${subscriberCounts.en} EN + ${subscriberCounts.es} ES subscribers. Continue?`
        : `Are you sure you want to ${action} this post?`;
    if (!confirm(confirmMsg)) return;

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
          {isFirstPublish && (
            <span className="text-xs text-gray-400">
              Will email {subscriberCounts.en} EN + {subscriberCounts.es} ES subscribers
            </span>
          )}
          {post.status === 'published' && (
            <>
              <div className="flex items-center gap-2 text-xs">
                <span className="text-gray-500">Newsletter:</span>
                <ResendButton postId={post.id} locale="en" label="Send EN" />
                <ResendButton postId={post.id} locale="es" label="Send ES" />
              </div>
              <a
                href={`/blog/${post.slug}`}
                target="_blank"
                rel="noopener noreferrer"
                className="px-4 py-2 rounded-lg text-sm font-medium bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
              >
                View
              </a>
            </>
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
        <XHookCard
          hookEn={post.hook_en}
          hookEs={post.hook_es}
          twitterHookEn={post.twitter_hook_en}
          twitterHookEs={post.twitter_hook_es}
          slug={post.slug}
          postId={post.id}
          onUpdate={(fields) => setPost((prev) => ({ ...prev, ...fields }))}
        />
        {post.image_prompt && (
          <div className="rounded-lg border border-white/10 bg-white/5 p-4">
            <h3 className="text-sm font-medium text-gray-400 mb-2">Image Prompt</h3>
            <p className="text-sm text-gray-300 whitespace-pre-wrap">{post.image_prompt}</p>
          </div>
        )}
      </div>

      {/* Facebook Post Copy */}
      <FacebookPostCard
        postEn={post.facebook_post_en}
        postEs={post.facebook_post_es}
        postEnB={post.facebook_post_en_b}
        postEsB={post.facebook_post_es_b}
        postId={post.id}
        onUpdate={(fields) => setPost((prev) => ({ ...prev, ...fields }))}
      />

      {/* Facebook Image Crop (1200×630) */}
      <FacebookImageCrop
        postId={post.id}
        imageUrlEn={post.image_url_en}
        imageUrlEs={post.image_url_es}
        fbImageUrlEn={post.facebook_image_url_en}
        fbImageUrlEs={post.facebook_image_url_es}
        onUpdate={(fields) => setPost((prev) => ({ ...prev, ...fields }))}
      />

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
            <div className="flex gap-3 items-start">
              <span className="text-gray-500 shrink-0">Slug (ES):</span>
              <SocialUrlField
                value={post.slug_es}
                fieldKey="slug_es"
                placeholder="la-semana-que-el-umbral-se-movio"
                postId={post.id}
                onSave={(val) => setPost((prev) => ({ ...prev, slug_es: val }))}
              />
            </div>
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

function autoXHook(hook: string | null, slug: string, maxLen = 280): string {
  if (!hook) return '';
  const url = `https://dikta.me/blog/${slug}`;
  const available = maxLen - url.length - 2; // 2 for \n\n
  // Try first complete sentence
  const sentenceMatch = hook.match(/^[^.!?]+[.!?]/);
  if (sentenceMatch && sentenceMatch[0].length <= available) {
    return `${sentenceMatch[0]}\n\n${url}`;
  }
  // Try up to first semicolon or em-dash
  const clauseMatch = hook.match(/^[^;—]+[;—]/);
  if (clauseMatch && clauseMatch[0].length <= available) {
    return `${clauseMatch[0].replace(/[;—]\s*$/, '.')}\n\n${url}`;
  }
  // Fallback: trim to available space at word boundary
  let text = hook;
  if (text.length > available) {
    text = text.slice(0, available - 1).replace(/\s+\S*$/, '') + '\u2026';
  }
  return `${text}\n\n${url}`;
}

function XHookCard({
  hookEn,
  hookEs,
  twitterHookEn,
  twitterHookEs,
  slug,
  postId,
  onUpdate,
}: {
  hookEn: string | null;
  hookEs: string | null;
  twitterHookEn: string | null;
  twitterHookEs: string | null;
  slug: string;
  postId: string;
  onUpdate: (fields: Record<string, string | null>) => void;
}) {
  const [lang, setLang] = useState<'en' | 'es'>('en');
  const [copied, setCopied] = useState(false);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);

  const fieldKey = lang === 'en' ? 'twitter_hook_en' : 'twitter_hook_es';
  const savedHook = lang === 'en' ? twitterHookEn : twitterHookEs;
  const blogHook = lang === 'en' ? hookEn : hookEs;
  const url = `https://dikta.me/blog/${slug}`;

  // Use saved hook if available, otherwise auto-generate
  const displayText = savedHook ?? autoXHook(blogHook, slug);
  const [draft, setDraft] = useState(displayText);

  // Update draft when language changes
  const handleLangSwitch = () => {
    const nextLang = lang === 'en' ? 'es' : 'en';
    setLang(nextLang);
    const nextSaved = nextLang === 'en' ? twitterHookEn : twitterHookEs;
    const nextBlog = nextLang === 'en' ? hookEn : hookEs;
    setDraft(nextSaved ?? autoXHook(nextBlog, slug));
    setEditing(false);
  };

  // Full text for copy = draft text (which may or may not include URL)
  const copyText = draft.includes('dikta.me') ? draft : `${draft}\n\n${url}`;
  const charCount = copyText.length;

  const handleCopy = async () => {
    await navigator.clipboard.writeText(copyText);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      const res = await fetch(`/api/hqbackstage/blog/${postId}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ [fieldKey]: draft || null }),
      });
      if (res.ok) {
        onUpdate({ [fieldKey]: draft || null });
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

  const handleReset = () => {
    setDraft(autoXHook(blogHook, slug));
    setEditing(true);
  };

  return (
    <div className="rounded-lg border border-white/10 bg-white/5 p-4">
      <div className="flex items-center justify-between mb-2">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-medium text-gray-400">Hook for X</h3>
          {savedHook && <span className="text-[10px] text-green-400/60">saved</span>}
          {!savedHook && <span className="text-[10px] text-gray-600">auto</span>}
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={handleLangSwitch}
            className="text-xs px-2 py-0.5 rounded border border-white/10 text-gray-400 hover:text-white hover:border-white/30 transition-colors"
          >
            {lang === 'en' ? 'ES' : 'EN'}
          </button>
          {!editing && (
            <button
              onClick={() => { setDraft(savedHook ?? autoXHook(blogHook, slug)); setEditing(true); }}
              className="text-gray-600 hover:text-gray-300 transition-colors"
              title="Edit"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125" />
              </svg>
            </button>
          )}
          <button
            onClick={handleCopy}
            className="text-xs px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
          >
            {copied ? 'Copied' : 'Copy'}
          </button>
        </div>
      </div>
      {editing ? (
        <div className="space-y-2">
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={4}
            className="w-full bg-white/5 border border-white/20 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-400 resize-y"
          />
          <div className="flex items-center gap-2">
            <button onClick={handleSave} disabled={saving} className="px-2 py-1 rounded text-xs bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 disabled:opacity-50">
              {saving ? '...' : 'Save'}
            </button>
            <button onClick={() => setEditing(false)} className="px-2 py-1 rounded text-xs bg-white/5 text-gray-400 hover:bg-white/10">
              Cancel
            </button>
            <button onClick={handleReset} className="px-2 py-1 rounded text-xs bg-white/5 text-gray-500 hover:bg-white/10" title="Reset to auto-generated">
              Reset
            </button>
          </div>
        </div>
      ) : (
        <p className="text-sm text-gray-300 whitespace-pre-wrap mb-2">{displayText}</p>
      )}
      <p className={`text-xs mt-1 ${charCount > 280 ? 'text-red-400' : 'text-gray-500'}`}>
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

// ── FacebookPostCard ──────────────────────────────────────────────��─────────
function FacebookPostCard({
  postEn,
  postEs,
  postEnB,
  postEsB,
  postId,
  onUpdate,
}: {
  postEn: string | null;
  postEs: string | null;
  postEnB: string | null;
  postEsB: string | null;
  postId: string;
  onUpdate: (fields: Record<string, string | null>) => void;
}) {
  const [lang, setLang] = useState<'en' | 'es'>('en');
  const [variant, setVariant] = useState<'a' | 'b'>('a');
  const [copied, setCopied] = useState(false);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [fbState, setFbState] = useState({
    facebook_post_en: postEn,
    facebook_post_es: postEs,
    facebook_post_en_b: postEnB,
    facebook_post_es_b: postEsB,
  });

  const fieldKey =
    lang === 'en' && variant === 'a' ? 'facebook_post_en' :
    lang === 'en' && variant === 'b' ? 'facebook_post_en_b' :
    lang === 'es' && variant === 'a' ? 'facebook_post_es' :
    'facebook_post_es_b';

  const currentText = fbState[fieldKey as keyof typeof fbState];
  const [draft, setDraft] = useState(currentText ?? '');

  // Sync draft when lang/variant switches
  const handleLangSwitch = (nextLang: 'en' | 'es') => {
    setLang(nextLang);
    setEditing(false);
    const key = nextLang === 'en' && variant === 'a' ? 'facebook_post_en' :
                nextLang === 'en' && variant === 'b' ? 'facebook_post_en_b' :
                nextLang === 'es' && variant === 'a' ? 'facebook_post_es' :
                'facebook_post_es_b';
    setDraft(fbState[key as keyof typeof fbState] ?? '');
  };

  const handleVariantSwitch = (nextVariant: 'a' | 'b') => {
    setVariant(nextVariant);
    setEditing(false);
    const key = lang === 'en' && nextVariant === 'a' ? 'facebook_post_en' :
                lang === 'en' && nextVariant === 'b' ? 'facebook_post_en_b' :
                lang === 'es' && nextVariant === 'a' ? 'facebook_post_es' :
                'facebook_post_es_b';
    setDraft(fbState[key as keyof typeof fbState] ?? '');
  };

  const handleCopy = async () => {
    const text = editing ? draft : (currentText ?? '');
    if (!text) return;
    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      const res = await fetch(`/api/hqbackstage/blog/${postId}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ [fieldKey]: draft || null }),
      });
      if (res.ok) {
        const newState = { ...fbState, [fieldKey]: draft || null };
        setFbState(newState);
        onUpdate({ [fieldKey]: draft || null });
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

  const charCount = (editing ? draft : currentText ?? '').length;
  const hasCopy = !!(fbState.facebook_post_en || fbState.facebook_post_es);

  return (
    <div className="rounded-lg border border-white/10 bg-white/5 p-4">
      {/* Header row */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <svg className="w-4 h-4 text-blue-400" fill="currentColor" viewBox="0 0 24 24">
            <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
          </svg>
          <h3 className="text-sm font-medium text-gray-400">Facebook Posts</h3>
          {hasCopy && <span className="text-[10px] text-green-400/60">saved</span>}
        </div>
        <div className="flex items-center gap-2">
          {/* Lang toggle */}
          <div className="flex rounded border border-white/10 overflow-hidden text-xs">
            <button
              onClick={() => handleLangSwitch('en')}
              className={`px-2 py-0.5 transition-colors ${lang === 'en' ? 'bg-blue-500/30 text-blue-300' : 'text-gray-500 hover:text-gray-300'}`}
            >
              EN
            </button>
            <button
              onClick={() => handleLangSwitch('es')}
              className={`px-2 py-0.5 transition-colors ${lang === 'es' ? 'bg-blue-500/30 text-blue-300' : 'text-gray-500 hover:text-gray-300'}`}
            >
              ES
            </button>
          </div>
          {/* Variant toggle */}
          <div className="flex rounded border border-white/10 overflow-hidden text-xs">
            <button
              onClick={() => handleVariantSwitch('a')}
              className={`px-2 py-0.5 transition-colors ${variant === 'a' ? 'bg-purple-500/30 text-purple-300' : 'text-gray-500 hover:text-gray-300'}`}
            >
              A
            </button>
            <button
              onClick={() => handleVariantSwitch('b')}
              className={`px-2 py-0.5 transition-colors ${variant === 'b' ? 'bg-purple-500/30 text-purple-300' : 'text-gray-500 hover:text-gray-300'}`}
            >
              B
            </button>
          </div>
          {!editing && currentText && (
            <button
              onClick={() => { setDraft(currentText); setEditing(true); }}
              className="text-gray-600 hover:text-gray-300 transition-colors"
              title="Edit"
            >
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L6.832 19.82a4.5 4.5 0 0 1-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 0 1 1.13-1.897L16.863 4.487Zm0 0L19.5 7.125" />
              </svg>
            </button>
          )}
          <button
            onClick={handleCopy}
            disabled={!currentText && !editing}
            className="text-xs px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors disabled:opacity-30"
          >
            {copied ? 'Copied!' : 'Copy'}
          </button>
        </div>
      </div>

      {/* Content */}
      {editing ? (
        <div className="space-y-2">
          <textarea
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            rows={6}
            className="w-full bg-white/5 border border-white/20 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-400 resize-y"
          />
          <div className="flex items-center gap-2">
            <button onClick={handleSave} disabled={saving} className="px-2 py-1 rounded text-xs bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 disabled:opacity-50">
              {saving ? '...' : 'Save'}
            </button>
            <button onClick={() => setEditing(false)} className="px-2 py-1 rounded text-xs bg-white/5 text-gray-400 hover:bg-white/10">
              Cancel
            </button>
          </div>
        </div>
      ) : currentText ? (
        <p className="text-sm text-gray-300 whitespace-pre-wrap">{currentText}</p>
      ) : (
        <p className="text-sm text-gray-600 italic">
          No Facebook copy yet — generated by /news-writer during the news run.
        </p>
      )}

      <p className={`text-xs mt-2 ${charCount > 500 ? 'text-yellow-500' : 'text-gray-600'}`}>
        {charCount} chars{charCount > 500 ? ' (long for FB — consider trimming)' : ''}
      </p>
    </div>
  );
}

// ── FacebookImageCrop ────────────────────────────────────────────────────────
async function getCroppedFbImg(imageSrc: string, pixelCrop: Area): Promise<Blob> {
  const image = new Image();
  image.crossOrigin = 'anonymous';
  image.src = imageSrc;
  await new Promise<void>((resolve, reject) => {
    image.onload = () => resolve();
    image.onerror = reject;
  });

  const canvas = document.createElement('canvas');
  canvas.width = 1200;
  canvas.height = 630;
  const ctx = canvas.getContext('2d')!;

  ctx.drawImage(
    image,
    pixelCrop.x,
    pixelCrop.y,
    pixelCrop.width,
    pixelCrop.height,
    0,
    0,
    1200,
    630,
  );

  return new Promise<Blob>((resolve, reject) => {
    canvas.toBlob(
      (blob) => {
        if (blob) resolve(blob);
        else reject(new Error('Canvas toBlob failed'));
      },
      'image/webp',
      0.85,
    );
  });
}

function FacebookImageCrop({
  postId,
  imageUrlEn,
  imageUrlEs,
  fbImageUrlEn,
  fbImageUrlEs,
  onUpdate,
}: {
  postId: string;
  imageUrlEn: string | null;
  imageUrlEs: string | null;
  fbImageUrlEn: string | null;
  fbImageUrlEs: string | null;
  onUpdate: (fields: Record<string, string>) => void;
}) {
  const [open, setOpen] = useState(false);
  const [cropLang, setCropLang] = useState<'en' | 'es'>('en');
  const [cropModal, setCropModal] = useState<{ lang: 'en' | 'es'; src: string } | null>(null);
  const [crop, setCrop] = useState<Point>({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState<Area | null>(null);
  const [uploading, setUploading] = useState(false);

  const onCropComplete = useCallback((_: Area, pixels: Area) => {
    setCroppedAreaPixels(pixels);
  }, []);

  const openCropModal = (lang: 'en' | 'es') => {
    const src = lang === 'en' ? imageUrlEn : imageUrlEs;
    if (!src) {
      alert(`No main image found for ${lang.toUpperCase()}. Upload a main image first.`);
      return;
    }
    setCropLang(lang);
    setCrop({ x: 0, y: 0 });
    setZoom(1);
    setCroppedAreaPixels(null);
    setCropModal({ lang, src });
  };

  const handleCropSave = async () => {
    if (!cropModal || !croppedAreaPixels) return;
    setUploading(true);
    try {
      const blob = await getCroppedFbImg(cropModal.src, croppedAreaPixels);
      const formData = new FormData();
      formData.append('image', blob, `fb-${cropLang}.webp`);
      formData.append('lang', cropLang);

      const res = await fetch(`/api/hqbackstage/blog/${postId}/facebook-image`, {
        method: 'POST',
        body: formData,
      });

      if (res.ok) {
        const data = await res.json() as { url: string };
        const field = cropLang === 'en' ? 'facebook_image_url_en' : 'facebook_image_url_es';
        onUpdate({ [field]: data.url });
        setCropModal(null);
      } else {
        const err = await res.text();
        alert(`Upload failed: ${err}`);
      }
    } catch (err) {
      alert(`Crop failed: ${err}`);
    } finally {
      setUploading(false);
    }
  };

  return (
    <>
      <div className="rounded-lg border border-white/10">
        <button
          onClick={() => setOpen(!open)}
          className="w-full flex items-center justify-between px-4 py-3 text-sm font-medium text-gray-400 hover:text-white transition-colors"
        >
          <div className="flex items-center gap-2">
            <svg className="w-4 h-4 text-blue-400" fill="currentColor" viewBox="0 0 24 24">
              <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
            </svg>
            <span>Facebook Image (1200×630)</span>
            {(fbImageUrlEn || fbImageUrlEs) && (
              <span className="text-[10px] text-green-400/60">
                {[fbImageUrlEn && 'EN', fbImageUrlEs && 'ES'].filter(Boolean).join(', ')}
              </span>
            )}
          </div>
          <span className="text-xs">{open ? '▲' : '▼'}</span>
        </button>

        {open && (
          <div className="border-t border-white/10 px-4 py-4 space-y-4">
            <p className="text-xs text-gray-500">
              Crop your main blog images to the 1200×630 aspect ratio Facebook uses for link previews and feed posts.
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* EN */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs text-gray-500">EN (1200×630)</span>
                  <button
                    onClick={() => openCropModal('en')}
                    className="text-xs px-2 py-1 rounded bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
                  >
                    {fbImageUrlEn ? '↺ Re-crop EN' : '+ Crop EN'}
                  </button>
                </div>
                {fbImageUrlEn ? (
                  <img src={fbImageUrlEn} alt="Facebook EN" className="rounded-md w-full object-cover" style={{ aspectRatio: '1200/630' }} />
                ) : imageUrlEn ? (
                  <div className="rounded-md w-full bg-white/5 border border-white/10 flex items-center justify-center text-gray-600 text-xs" style={{ aspectRatio: '1200/630' }}>
                    Not cropped yet
                  </div>
                ) : (
                  <div className="rounded-md w-full bg-white/5 border border-dashed border-white/10 flex items-center justify-center text-gray-600 text-xs" style={{ aspectRatio: '1200/630' }}>
                    Upload main EN image first
                  </div>
                )}
              </div>
              {/* ES */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs text-gray-500">ES (1200×630)</span>
                  <button
                    onClick={() => openCropModal('es')}
                    className="text-xs px-2 py-1 rounded bg-blue-500/20 text-blue-300 hover:bg-blue-500/30 transition-colors"
                  >
                    {fbImageUrlEs ? '↺ Re-crop ES' : '+ Crop ES'}
                  </button>
                </div>
                {fbImageUrlEs ? (
                  <img src={fbImageUrlEs} alt="Facebook ES" className="rounded-md w-full object-cover" style={{ aspectRatio: '1200/630' }} />
                ) : imageUrlEs ? (
                  <div className="rounded-md w-full bg-white/5 border border-white/10 flex items-center justify-center text-gray-600 text-xs" style={{ aspectRatio: '1200/630' }}>
                    Not cropped yet
                  </div>
                ) : (
                  <div className="rounded-md w-full bg-white/5 border border-dashed border-white/10 flex items-center justify-center text-gray-600 text-xs" style={{ aspectRatio: '1200/630' }}>
                    Upload main ES image first
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Crop Modal */}
      {cropModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
          <div className="bg-[#1a1a2e] border border-white/10 rounded-2xl w-full max-w-2xl mx-4 overflow-hidden">
            <div className="px-6 pt-5 pb-3">
              <h3 className="text-lg font-semibold text-white">
                Crop for Facebook — {cropModal.lang.toUpperCase()} (1200×630)
              </h3>
            </div>
            <div className="relative w-full bg-black/50" style={{ height: '360px' }}>
              <Cropper
                image={cropModal.src}
                crop={crop}
                zoom={zoom}
                aspect={1200 / 630}
                onCropChange={setCrop}
                onZoomChange={setZoom}
                onCropComplete={onCropComplete}
              />
            </div>
            <div className="px-6 py-4">
              <label className="block text-xs text-gray-400 mb-2">Zoom</label>
              <input
                type="range"
                min={1}
                max={3}
                step={0.05}
                value={zoom}
                onChange={(e) => setZoom(Number(e.target.value))}
                className="w-full accent-blue-500"
              />
            </div>
            <div className="px-6 pb-5 flex gap-3">
              <button
                onClick={() => setCropModal(null)}
                disabled={uploading}
                className="flex-1 py-2.5 bg-white/5 hover:bg-white/10 border border-white/10 text-white rounded-lg transition-colors text-sm font-medium disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                onClick={handleCropSave}
                disabled={uploading || !croppedAreaPixels}
                className="flex-1 py-2.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {uploading ? 'Saving...' : 'Save Crop'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
