'use client';
import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';

const BAR_COUNT = 24;

// Deterministic pseudo-random delays so bars look organic but SSR-stable
const BAR_DELAYS = Array.from({ length: BAR_COUNT }, (_, i) => `${(i * 0.13) % 1.2}s`);
const BAR_DURATIONS = Array.from(
  { length: BAR_COUNT },
  (_, i) => `${0.4 + ((i * 7 + 3) % 5) * 0.12}s`
);

export function TTSSection() {
  const t = useTranslations('TTSSection');
  const SENTENCE = t('sampleText');
  const WORDS = SENTENCE.split(' ');

  const [wordIdx, setWordIdx] = useState(0);
  const [progress, setProgress] = useState(0);
  const [isPlaying, setIsPlaying] = useState(true);

  // Cycle words at ~320ms per word
  useEffect(() => {
    if (!isPlaying) return;
    const id = setInterval(() => {
      setWordIdx(i => {
        const next = (i + 1) % WORDS.length;
        if (next === 0) {
          // Brief pause before restarting
          setIsPlaying(false);
          setTimeout(() => {
            setProgress(0);
            setIsPlaying(true);
          }, 2000);
        }
        return next;
      });
      setProgress(p => Math.min(p + 100 / WORDS.length, 100));
    }, 320);
    return () => clearInterval(id);
  }, [isPlaying, WORDS.length]);

  return (
    <div id="tts-track" className="relative h-[150vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-r from-primary/5 to-transparent opacity-30" aria-hidden="true"></div>
        <div className="section-container grid md:grid-cols-2 gap-6 md:gap-12 items-center relative z-10">
          {/* Card (Left) */}
          <div className="card border-primary/20 bg-black/50 p-4 md:p-6 font-mono text-xs shadow-2xl relative overflow-hidden flex flex-col gap-4 order-last md:order-first">
            <div className="absolute -top-3 -right-3 w-24 h-24 bg-primary/20 blur-2xl rounded-full" aria-hidden="true"></div>
            <div className="flex justify-between items-center border-b border-white/10 pb-2">
              <span className="text-muted uppercase tracking-widest">{t('engineLabel')}</span>
              <span className={`w-3 h-3 rounded-full transition-colors duration-500 ${
                isPlaying ? 'bg-green-500 animate-pulse' : 'bg-yellow-500'
              }`}></span>
            </div>

            <div className="space-y-4 px-2 py-4">
              {/* Waveform Visualizer */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-primary">●</span> {t('audioOutput')}
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded flex items-center justify-center gap-[3px] h-[64px]">
                  {BAR_DELAYS.map((delay, i) => (
                    <div
                      key={i}
                      className="w-[3px] rounded-full bg-primary/70"
                      style={{
                        height: isPlaying ? undefined : '4px',
                        animation: isPlaying
                          ? `tts-bar ${BAR_DURATIONS[i]} ease-in-out ${delay} infinite alternate`
                          : 'none',
                        transition: 'height 0.3s ease',
                      }}
                    ></div>
                  ))}
                </div>
              </div>

              {/* Text being spoken — word highlight */}
              <div className="flex flex-col gap-2">
                <label className="text-[10px] text-muted uppercase flex items-center gap-2">
                  <span className="text-green-400">●</span> {t('speaking')}
                </label>
                <div className="bg-white/5 border border-white/10 p-3 rounded min-h-[60px]">
                  <p className="text-sm leading-relaxed">
                    {WORDS.map((word, i) => (
                      <span
                        key={i}
                        className={`transition-colors duration-150 ${
                          i === wordIdx && isPlaying
                            ? 'text-primary font-bold'
                            : i < wordIdx || !isPlaying
                              ? 'text-muted'
                              : 'text-white/30'
                        }`}
                      >
                        {word}{i < WORDS.length - 1 ? ' ' : ''}
                      </span>
                    ))}
                  </p>
                </div>
              </div>

              {/* Progress bar */}
              <div className="flex flex-col gap-2">
                <div className="flex justify-between text-[10px] text-muted">
                  <span>{t('progress')}</span>
                  <span>{Math.round(progress)}%</span>
                </div>
                <div className="h-1 bg-white/10 rounded-full overflow-hidden">
                  <div
                    className="h-full bg-primary rounded-full transition-all duration-300 ease-linear"
                    style={{ width: `${progress}%` }}
                  ></div>
                </div>
              </div>
            </div>

            <div className="mt-auto pt-4 border-t border-white/10 flex justify-between text-[10px] text-muted">
              <span>{t('voice')}</span>
              <span className="italic">{t('engineNote')}</span>
            </div>
          </div>

          {/* Text (Right) */}
          <div>
            <div className="inline-block mb-4 px-3 py-1 bg-white/5 border border-white/10 rounded-full text-white text-[10px] font-mono tracking-widest">
              {t('badge')}
            </div>
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white text-balance">
              {t('title')}
            </h2>
            <p className="text-lg md:text-xl text-muted mb-6 md:mb-8">
              {t('subtitle')}
            </p>
            <ul className="space-y-4 text-muted">
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip1Label')}</strong> — {t('chip1Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip2Label')}</strong> — {t('chip2Desc')}
              </li>
              <li className="flex items-center gap-3">
                <span className="text-primary">✓</span> <strong>{t('chip3Label')}</strong> — {t('chip3Desc')}
              </li>
            </ul>
          </div>
        </div>
      </div>

      {/* CSS Keyframes for waveform bars */}
      <style jsx>{`
        @keyframes tts-bar {
          0% {
            height: 4px;
          }
          100% {
            height: 36px;
          }
        }
      `}</style>
    </div>
  );
}
