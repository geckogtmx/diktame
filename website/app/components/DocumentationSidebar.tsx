"use client";

import Link from 'next/link';
import { usePathname } from 'next/navigation';

const navigation = [
  {
    title: 'Overview',
    links: [
      { href: '/docs/getting-started', label: 'Getting Started' },
      { href: '/docs/troubleshooting', label: 'Troubleshooting' },
    ]
  },
  {
    title: 'Core Features',
    links: [
      { href: '/docs/features/dictation', label: 'Dictation' },
      { href: '/docs/features/refine', label: 'Refine' },
      { href: '/docs/features/quick-chat', label: 'Quick Chat' },
    ]
  },
  {
    title: 'Utility Pipelines',
    links: [
      { href: '/docs/features/ask', label: 'Ask' },
      { href: '/docs/features/translate', label: 'Translate' },
      { href: '/docs/features/note', label: 'Note' },
      { href: '/docs/features/oops', label: 'Oops' },
    ]
  },
  {
    title: 'Settings',
    links: [
      { href: '/docs/settings/general', label: 'General' },
      { href: '/docs/settings/account', label: 'Account' },
      { href: '/docs/settings/ai-engine', label: 'AI Engine' },
      { href: '/docs/settings/api-keys', label: 'API Keys' },
      { href: '/docs/settings/audio', label: 'Audio' },
      { href: '/docs/settings/control-panel', label: 'Control Panel' },
      { href: '/docs/settings/dictation-modes', label: 'Dictation Modes' },
      { href: '/docs/settings/hotkeys', label: 'Hotkeys' },
      { href: '/docs/settings/modes', label: 'Utility Modes' },
      { href: '/docs/settings/ollama', label: 'Ollama' },
      { href: '/docs/settings/privacy', label: 'Privacy' },
      { href: '/docs/settings/snippets', label: 'Snippets' },
    ]
  },
  {
    title: 'Developer Guide',
    links: [
      { href: '/docs/dev/setup', label: 'Environment Setup' },
      { href: '/docs/dev/migration/v1-to-v2-guide', label: 'V1 to V2 Migration' },
      { href: '/docs/dev/architecture/audio-pipeline', label: 'Audio Pipeline' },
      { href: '/docs/dev/architecture/ui-mvvm', label: 'UI MVVM' },
      { href: '/docs/dev/architecture/threat-model', label: 'Threat Model' },
      { href: '/docs/dev/api/stt-providers', label: 'STT Providers API' },
      { href: '/docs/dev/api/llm-providers', label: 'LLM Providers API' },
    ]
  }
];

export function DocumentationSidebar() {
  const pathname = usePathname();

  return (
    <nav className="w-full lg:w-64 shrink-0 px-4 lg:px-0 mb-12 lg:mb-0">
      <div className="sticky top-32 lg:h-[calc(100vh-8rem)] lg:overflow-y-auto pr-6 custom-scrollbar pb-10">
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
