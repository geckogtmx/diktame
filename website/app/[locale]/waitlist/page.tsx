'use client';

import { Navbar } from '@/app/components/Navbar';
import { Footer } from '@/app/components/Footer';
import { WaitingListForm } from '@/app/components/WaitingListForm';
import { Container } from '@/app/components/Container';
import { useTranslations } from 'next-intl';

export default function WaitlistPage() {
  const t = useTranslations('WaitlistPage');
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20 overflow-hidden">
        {/* Ambient background glow */}
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[400px] bg-primary/10 rounded-full blur-[120px] pointer-events-none"></div>

        <Container>
          <div className="max-w-3xl mx-auto text-center relative z-10">
            {/* Tag/Badge */}
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/5 border border-white/10 mb-8 animate-fade-in-up">
              <span className="w-1.5 h-1.5 rounded-full bg-cta"></span>
              <span className="text-[10px] font-bold text-white/60 uppercase tracking-[0.2em]">
                {t('badge')}
              </span>
            </div>

            <h1 className="text-4xl md:text-6xl font-bold mb-6 tracking-tight animate-fade-in-up animation-delay-100">
              {t.rich('title', {
                almost: (chunks) => <span className="text-primary">{chunks}</span>
              })}
            </h1>

            <div className="space-y-6 text-lg md:text-xl text-muted mb-12 animate-fade-in-up animation-delay-200 leading-relaxed italic">
              <p>
                {t('subtitle1')}
              </p>
              <p className="not-italic text-base md:text-lg text-white/80">
                {t('subtitle2')}
              </p>
            </div>

            {/* The Form */}
            <div className="animate-fade-in-up animation-delay-300">
                <WaitingListForm />
            </div>

            {/* Why join? */}
            <div className="mt-20 grid grid-cols-1 md:grid-cols-3 gap-8 text-left animate-fade-in-up animation-delay-400">
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">🚀</div>
                    <h3 className="text-white font-bold mb-2">{t('card1Title')}</h3>
                    <p className="text-sm text-muted">{t('card1Desc')}</p>
                </div>
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">💎</div>
                    <h3 className="text-white font-bold mb-2">{t('card2Title')}</h3>
                    <p className="text-sm text-muted">{t('card2Desc')}</p>
                </div>
                <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
                    <div className="text-2xl mb-3">🛡️</div>
                    <h3 className="text-white font-bold mb-2">{t('card3Title')}</h3>
                    <p className="text-sm text-muted">{t('card3Desc')}</p>
                </div>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
