'use client';

import { useEffect } from 'react';
import { useTranslations } from 'next-intl';

interface FeaturesModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function FeaturesModal({ isOpen, onClose }: FeaturesModalProps) {
  const t = useTranslations('FeaturesModal');

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [isOpen]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 md:p-8">
      <div className="absolute inset-0 bg-black/80 backdrop-blur-xl" onClick={onClose}></div>
      <div className="relative w-full max-w-4xl bg-[#0a0a0a] border border-white/10 rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Modal Header */}
        <div className="p-6 border-b border-white/5 flex justify-between items-center bg-black/20">
          <div>
            <h3 className="text-2xl font-bold text-white">{t('title')}</h3>
            <p className="text-sm text-muted">{t('subtitle')}</p>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-white/5 rounded-full transition-colors group">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              className="w-6 h-6 text-muted group-hover:text-white"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 md:p-10 overflow-y-auto">
          <div className="grid md:grid-cols-2 gap-12">
            {/* Column 1 */}
            <div className="space-y-10">
              {/* Core */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('coreFunctionality')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('corePushToTalk')}</strong> — {t('corePushToTalkDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreWhisper')}</strong> — {t('coreWhisperDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreLocalIntel')}</strong> — {t('coreLocalIntelDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreAutoInject')}</strong> — {t('coreAutoInjectDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreAutoEnter')}</strong> — {t('coreAutoEnterDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreAudioDuck')}</strong> — {t('coreAudioDuckDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreSoundFeedback')}</strong> — {t('coreSoundFeedbackDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreOops')}</strong> — {t('coreOopsDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreTts')}</strong> — {t('coreTtsDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('coreStreaming')}</strong> — {t('coreStreamingDesc')}
                  </li>
                </ul>
              </section>

              {/* Modes */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('voiceModes')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('modesStandard')}</strong> — {t('modesStandardDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesProfessional')}</strong> — {t('modesProfessionalDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesPrompt')}</strong> — {t('modesPromptDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesQuickChat')}</strong> — {t('modesQuickChatDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesAsk')}</strong> — {t('modesAskDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesRefine')}</strong> — {t('modesRefineDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesTranslate')}</strong> — {t('modesTranslateDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('modesSnippets')}</strong> — {t('modesSnippetsDesc')}
                  </li>
                </ul>
              </section>

              {/* Intelligence */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('aiIntelligence')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('aiLocalBrain')}</strong> — {t('aiLocalBrainDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('aiFreedom')}</strong> — {t('aiFreedomDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('aiModelAgnostic')}</strong> — {t('aiModelAgnosticDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('aiContextAware')}</strong> — {t('aiContextAwareDesc')}
                  </li>
                </ul>
              </section>
            </div>

            {/* Column 2 */}
            <div className="space-y-10">
              {/* Privacy & Security */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('privacySecurity')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('privAirGapped')}</strong> — {t('privAirGappedDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('privPiiScrub')}</strong> — {t('privPiiScrubDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('privEncrypted')}</strong> — {t('privEncryptedDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('privZeroCloud')}</strong> — {t('privZeroCloudDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('privNoMarkup')}</strong> — {t('privNoMarkupDesc')}
                  </li>
                </ul>
              </section>

              {/* Technical */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('technical')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('techBinary')}</strong> — {t('techBinaryDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('techNative')}</strong> — {t('techNativeDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('techPill')}</strong> — {t('techPillDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('techOpen')}</strong> — {t('techOpenDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('techSource')}</strong> — {t('techSourceDesc')}
                  </li>
                </ul>
              </section>

              {/* Future Roadmap */}
              <section>
                <h4 className="text-primary font-mono text-xs uppercase tracking-widest mb-4">{t('roadmap')}</h4>
                <ul className="space-y-2 text-sm text-muted">
                  <li>
                    <strong className="text-white">{t('roadConnectors')}</strong> — {t('roadConnectorsDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('roadMeetings')}</strong> — {t('roadMeetingsDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('roadVision')}</strong> — {t('roadVisionDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('roadMemory')}</strong> — {t('roadMemoryDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('roadStreamDeck')}</strong> — {t('roadStreamDeckDesc')}
                  </li>
                  <li>
                    <strong className="text-white">{t('roadCrossPlatform')}</strong> — {t('roadCrossPlatformDesc')}
                  </li>
                </ul>
              </section>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
