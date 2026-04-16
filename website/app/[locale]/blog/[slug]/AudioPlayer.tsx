'use client';

import { useRef, useState, useCallback } from 'react';

function formatTime(seconds: number): string {
  if (!isFinite(seconds) || seconds < 0) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

export function AudioPlayer({ src }: { src: string }) {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [playing, setPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);

  const toggle = useCallback(() => {
    const el = audioRef.current;
    if (!el) return;
    if (playing) {
      el.pause();
    } else {
      void el.play();
    }
    setPlaying(!playing);
  }, [playing]);

  const handleTimeUpdate = () => {
    setCurrentTime(audioRef.current?.currentTime ?? 0);
  };

  const handleLoadedMetadata = () => {
    setDuration(audioRef.current?.duration ?? 0);
  };

  const handleEnded = () => setPlaying(false);

  const handleScrub = (e: React.ChangeEvent<HTMLInputElement>) => {
    const t = Number(e.target.value);
    if (audioRef.current) audioRef.current.currentTime = t;
    setCurrentTime(t);
  };

  const progress = duration > 0 ? (currentTime / duration) * 100 : 0;

  return (
    <div className="flex items-center gap-3 rounded-xl border border-white/10 bg-white/[0.03] px-4 py-3">
      {/* Hidden native audio element */}
      <audio
        ref={audioRef}
        src={src}
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={handleLoadedMetadata}
        onEnded={handleEnded}
        preload="metadata"
      />

      {/* Label */}
      <span className="hidden sm:block text-xs text-gray-500 shrink-0 whitespace-nowrap">
        Listen
      </span>

      {/* Play / Pause */}
      <button
        onClick={toggle}
        aria-label={playing ? 'Pause' : 'Play'}
        className="shrink-0 w-8 h-8 flex items-center justify-center rounded-full bg-orange-500/20 text-orange-300 hover:bg-orange-500/30 transition-colors"
      >
        {playing ? (
          /* Pause icon */
          <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 24 24">
            <path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z" />
          </svg>
        ) : (
          /* Play icon */
          <svg className="w-4 h-4 translate-x-px" fill="currentColor" viewBox="0 0 24 24">
            <path d="M8 5v14l11-7z" />
          </svg>
        )}
      </button>

      {/* Current time */}
      <span className="text-xs text-gray-500 shrink-0 w-8 text-right tabular-nums">
        {formatTime(currentTime)}
      </span>

      {/* Scrubber */}
      <div className="relative flex-1 h-1.5 group">
        <div className="absolute inset-0 rounded-full bg-white/10" />
        <div
          className="absolute inset-y-0 left-0 rounded-full bg-orange-500/60"
          style={{ width: `${progress}%` }}
        />
        <input
          type="range"
          min={0}
          max={duration || 0}
          step={0.1}
          value={currentTime}
          onChange={handleScrub}
          className="absolute inset-0 w-full opacity-0 cursor-pointer h-full"
          aria-label="Seek"
        />
      </div>

      {/* Duration */}
      <span className="text-xs text-gray-500 shrink-0 w-8 tabular-nums">
        {formatTime(duration)}
      </span>

      {/* Download */}
      <a
        href={src}
        download
        aria-label="Download audio"
        className="shrink-0 text-gray-500 hover:text-gray-300 transition-colors"
        title="Download audio file"
      >
        <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5M16.5 12 12 16.5m0 0L7.5 12m4.5 4.5V3" />
        </svg>
      </a>
    </div>
  );
}
