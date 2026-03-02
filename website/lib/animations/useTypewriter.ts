'use client';

import { useEffect, useState } from 'react';

interface UseTypewriterOptions {
  text: string;
  speed?: number;
  startAnimation?: boolean;
  loop?: boolean;
}

export function useTypewriter({
  text,
  speed = 50,
  startAnimation = true,
  loop = false,
}: UseTypewriterOptions) {
  const [displayedText, setDisplayedText] = useState('');
  const [isCompleted, setIsCompleted] = useState(false);
  const [isAnimating, setIsAnimating] = useState(startAnimation);

  useEffect(() => {
    if (!isAnimating) {
      return;
    }

    let index = 0;
    const interval = setInterval(() => {
      if (index < text.length) {
        setDisplayedText(text.substring(0, index + 1));
        index++;
      } else {
        setIsCompleted(true);
        if (loop) {
          index = 0;
          setDisplayedText('');
        } else {
          clearInterval(interval);
        }
      }
    }, speed);

    return () => clearInterval(interval);
  }, [text, speed, isAnimating, loop]);

  return {
    displayedText,
    isCompleted,
    setIsAnimating,
    restart: () => {
      setDisplayedText('');
      setIsCompleted(false);
      setIsAnimating(true);
    },
  };
}
