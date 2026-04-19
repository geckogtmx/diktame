"use client";

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { useTranslations } from 'next-intl';

export function DocumentationSidebar() {
  const t = useTranslations('DocumentationSidebar');
  const pathname = usePathname();

  const navigation = [
    {
      title: t('overview'),
      links: [
        { href: '/docs/getting-started', label: t('gettingStarted') },
        { href: '/docs/dashboard', label: t('webDashboard') },
        { href: '/docs/troubleshooting', label: t('troubleshooting') },
      ]
    },
    {
      title: t('threeInputs'),
      links: [
        { href: '/docs/inputs/voice', label: t('voiceInput') },
        { href: '/docs/inputs/text-selection', label: t('textSelectionInput') },
        { href: '/docs/inputs/screen', label: t('screenInput') },
      ]
    },
    {
      title: t('infiniteOutput'),
      links: [
        { href: '/docs/features/dictation', label: t('dictation') },
        { href: '/docs/features/refine', label: t('refine') },
        { href: '/docs/features/ask', label: t('ask') },
        { href: '/docs/features/translate', label: t('translate') },
        { href: '/docs/features/note', label: t('note') },
        { href: '/docs/features/vision', label: t('visionActions') },
        { href: '/docs/features/quick-chat', label: t('quickChat') },
        { href: '/docs/features/oops', label: t('oops') },
        { href: '/docs/features/macros', label: t('macros') },
        { href: '/docs/features/tts', label: t('textToSpeech') },
      ]
    },
    {
      title: t('settings'),
      links: [
        // Follows the real app nav: General → Audio → AI Engine → Pipelines → Dictation Presets → Macros → Privacy → Account.
        // Sub-tab docs are grouped directly under their parent.
        { href: '/docs/settings/general', label: t('general') },
        { href: '/docs/settings/control-panel', label: t('controlPanel') },
        { href: '/docs/settings/hotkeys', label: t('hotkeys') },
        { href: '/docs/settings/audio', label: t('audio') },
        { href: '/docs/settings/ai-engine', label: t('aiEngine') },
        { href: '/docs/settings/pipelines', label: t('pipelines') },
        { href: '/docs/settings/dictation-modes', label: t('dictationModes') },
        { href: '/docs/settings/macros', label: t('macros') },
        { href: '/docs/settings/privacy', label: t('privacy') },
        { href: '/docs/settings/account', label: t('account') },
      ]
    },
    {
      title: t('developerGuide'),
      links: [
        { href: '/docs/dev/setup', label: t('envSetup') },
        { href: '/docs/dev/migration/v1-to-v2-guide', label: t('v1ToV2Migration') },
        { href: '/docs/dev/architecture/audio-pipeline', label: t('audioPipeline') },
        { href: '/docs/dev/architecture/ui-mvvm', label: t('uiMvvm') },
        { href: '/docs/dev/architecture/threat-model', label: t('threatModel') },
        { href: '/docs/dev/api/stt-providers', label: t('sttProviders') },
        { href: '/docs/dev/api/llm-providers', label: t('llmProviders') },
        { href: '/docs/dev/api/tts-providers', label: t('ttsProviders') },
      ]
    }
  ];

  return (
    <nav className="w-full lg:w-64 shrink-0 px-4 lg:px-0 mb-12 lg:mb-0">
      <div className="sticky top-32 pr-6 pb-10">
        <div className="space-y-8">
          {navigation.map((section) => (
            <div key={section.title}>
              <h3 className="font-semibold text-gray-200 uppercase tracking-wider text-sm mb-3">
                {section.title}
              </h3>
              <ul className="space-y-2 border-l border-white/10 ml-2 pl-4">
                {section.links.map((link) => {
                  const isActive = pathname === link.href;
                  return (
                    <li key={link.href}>
                      <Link
                        href={link.href}
                        className={`block text-sm transition-colors ${
                          isActive
                            ? 'text-blue-400 font-medium'
                            : 'text-gray-400 hover:text-gray-200'
                        }`}
                      >
                        {link.label}
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </nav>
  );
}
