import React from 'react';

interface SectionHeadingProps {
  title: string;
  description?: string;
  centered?: boolean;
  className?: string;
}

export function SectionHeading({
  title,
  description,
  centered = false,
  className = '',
}: SectionHeadingProps) {
  return (
    <div className={`${centered ? 'text-center' : ''} ${className}`}>
      <h2 className="text-4xl sm:text-5xl font-bold text-white mb-4">
        {title}
      </h2>
      {description && (
        <p className="text-lg text-gray-400 max-w-2xl">
          {description}
        </p>
      )}
    </div>
  );
}
