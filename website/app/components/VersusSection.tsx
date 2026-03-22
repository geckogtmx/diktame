'use client';

import { useVersusScroll } from '@/lib/animations/useVersusScroll';
import { useTranslations } from 'next-intl';

export function VersusSection() {
  const { visibleRows } = useVersusScroll();
  const t = useTranslations('VersusSection');

  return (
    <div id="versus-track" className="relative h-[180vh]">
      <div className="sticky top-0 h-screen flex items-center justify-center overflow-hidden">
        <div className="section-container relative z-10 w-full max-w-5xl">
          <div className="text-center mb-12">
            <h2 className="text-3xl md:text-5xl font-bold mb-6 text-white">{t('title')}</h2>
            <p className="text-muted">{t('subtitle')}</p>
          </div>

          <div className="card overflow-hidden bg-black/40 backdrop-blur-xl border-white/5 p-0">
            <table className="w-full text-left border-collapse">
              <caption className="sr-only">{t('tableCaption')}</caption>
              <thead>
                <tr className="border-b border-white/10 bg-white/5">
                  <th className="py-2 px-2 md:py-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-muted">{t('headerFeature')}</th>
                  <th className="py-2 px-2 md:py-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-muted">{t('headerOthers')}</th>
                  <th className="py-2 px-2 md:py-4 md:px-6 text-[10px] md:text-sm font-mono uppercase tracking-widest text-primary">{t('headerDiktaMe')}</th>
                </tr>
              </thead>
              <tbody className="text-xs md:text-base">
                <tr
                  id="vs-row-1"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 1 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row1Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row1Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row1DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-2"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 2 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row2Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row2Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row2DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-3"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 3 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row3Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row3Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row3DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-4"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 4 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row4Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row4Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row4DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-5"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 5 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row5Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row5Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row5DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-6"
                  className={`border-b border-white/5 transition-all duration-500 ${visibleRows >= 6 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row6Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row6Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row6DiktaMe')}</td>
                </tr>
                <tr
                  id="vs-row-7"
                  className={`transition-all duration-500 ${visibleRows >= 7 ? 'opacity-100 bg-white/5' : 'opacity-20'
                    }`}
                >
                  <td className="py-2 px-2 md:py-4 md:px-6 text-white font-medium">{t('row7Feature')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-muted">{t('row7Others')}</td>
                  <td className="py-2 px-2 md:py-4 md:px-6 text-primary font-bold">{t('row7DiktaMe')}</td>
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3} className="py-2 px-2 md:py-3 md:px-6 text-[10px] md:text-xs text-muted/60 italic">
                    {t('footnote')}
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
