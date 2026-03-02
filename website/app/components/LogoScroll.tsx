'use client';

const apps = [
  'TERMINAL',
  'CURSOR',
  'ANTIGRAVITY',
  'GOOGLE DOCS',
  'WHATSAPP',
  'NOTEPAD',
  'VS CODE',
  'SLACK',
  'EXCEL',
  'DISCORD',
  'OUTLOOK',
  'CHROME',
  'ARC',
];

export function LogoScroll() {
  return (
    <>
      {/* Header Text */}
      <div className="text-center mt-32 mb-8 text-white/40 font-mono text-xs uppercase tracking-[0.3em] reveal">
        Use it with...
      </div>

      {/* Infinite Scroll Section */}
      <section className="py-12 border-y border-white/5 bg-black/20 overflow-hidden">
        <div className="w-full inline-flex flex-nowrap overflow-hidden [mask-image:_linear-gradient(to_right,transparent_0,_black_128px,_black_calc(100%-128px),transparent_100%)]">
          <ul className="flex items-center justify-center md:justify-start [&_li]:mx-8 [&_img]:max-w-none animate-infinite-scroll text-2xl font-bold text-white/20 whitespace-nowrap">
            {apps.map((app, index) => (
              <li key={index}>{app}</li>
            ))}
          </ul>
          <ul
            className="flex items-center justify-center md:justify-start [&_li]:mx-8 [&_img]:max-w-none animate-infinite-scroll text-2xl font-bold text-white/20 whitespace-nowrap"
            aria-hidden="true"
          >
            {apps.map((app, index) => (
              <li key={index}>{app}</li>
            ))}
          </ul>
        </div>
      </section>
    </>
  );
}
