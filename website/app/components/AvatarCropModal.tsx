'use client';

import { useState, useCallback } from 'react';
import Cropper from 'react-easy-crop';
import type { Area, Point } from 'react-easy-crop';

interface AvatarCropModalProps {
  imageSrc: string;
  onSave: (blob: Blob) => Promise<void>;
  onCancel: () => void;
  labels: {
    title: string;
    zoom: string;
    save: string;
    cancel: string;
  };
}

async function getCroppedImg(imageSrc: string, pixelCrop: Area): Promise<Blob> {
  const image = new Image();
  image.src = imageSrc;
  await new Promise<void>((resolve, reject) => {
    image.onload = () => resolve();
    image.onerror = reject;
  });

  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 256;
  const ctx = canvas.getContext('2d')!;

  ctx.drawImage(
    image,
    pixelCrop.x,
    pixelCrop.y,
    pixelCrop.width,
    pixelCrop.height,
    0,
    0,
    256,
    256,
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

export function AvatarCropModal({ imageSrc, onSave, onCancel, labels }: AvatarCropModalProps) {
  const [crop, setCrop] = useState<Point>({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedAreaPixels, setCroppedAreaPixels] = useState<Area | null>(null);
  const [saving, setSaving] = useState(false);

  const onCropComplete = useCallback((_croppedArea: Area, pixels: Area) => {
    setCroppedAreaPixels(pixels);
  }, []);

  async function handleSave() {
    if (!croppedAreaPixels) return;
    setSaving(true);
    try {
      const blob = await getCroppedImg(imageSrc, croppedAreaPixels);
      await onSave(blob);
    } catch {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm">
      <div className="bg-[#1a1a2e] border border-white/10 rounded-2xl w-full max-w-md mx-4 overflow-hidden">
        {/* Header */}
        <div className="px-6 pt-5 pb-3">
          <h3 className="text-lg font-semibold text-white">{labels.title}</h3>
        </div>

        {/* Cropper area */}
        <div className="relative w-full aspect-square bg-black/50">
          <Cropper
            image={imageSrc}
            crop={crop}
            zoom={zoom}
            aspect={1}
            cropShape="round"
            showGrid={false}
            onCropChange={setCrop}
            onZoomChange={setZoom}
            onCropComplete={onCropComplete}
          />
        </div>

        {/* Zoom slider */}
        <div className="px-6 py-4">
          <label className="block text-xs text-gray-400 mb-2">{labels.zoom}</label>
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

        {/* Actions */}
        <div className="px-6 pb-5 flex gap-3">
          <button
            onClick={onCancel}
            disabled={saving}
            className="flex-1 py-2.5 bg-white/5 hover:bg-white/10 border border-white/10 text-white rounded-lg transition-colors text-sm font-medium disabled:opacity-50"
          >
            {labels.cancel}
          </button>
          <button
            onClick={handleSave}
            disabled={saving || !croppedAreaPixels}
            className="flex-1 py-2.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors text-sm font-medium disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {saving ? '...' : labels.save}
          </button>
        </div>
      </div>
    </div>
  );
}
