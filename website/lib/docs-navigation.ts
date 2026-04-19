// Single source of truth for docs page order.
// Both DocumentationSidebar and DocumentationFooterNav read from this.

export interface DocPage {
  /** Route path (no locale prefix) */
  href: string;
  /** i18n key under the `DocumentationSidebar` namespace */
  labelKey: string;
}

export const DOCS_ORDER: DocPage[] = [
  // Overview
  { href: '/docs/getting-started', labelKey: 'gettingStarted' },
  { href: '/docs/dashboard', labelKey: 'webDashboard' },
  { href: '/docs/troubleshooting', labelKey: 'troubleshooting' },

  // Three Inputs
  { href: '/docs/inputs/voice', labelKey: 'voiceInput' },
  { href: '/docs/inputs/text-selection', labelKey: 'textSelectionInput' },
  { href: '/docs/inputs/screen', labelKey: 'screenInput' },

  // Infinite Output
  { href: '/docs/features/dictation', labelKey: 'dictation' },
  { href: '/docs/features/refine', labelKey: 'refine' },
  { href: '/docs/features/ask', labelKey: 'ask' },
  { href: '/docs/features/translate', labelKey: 'translate' },
  { href: '/docs/features/note', labelKey: 'note' },
  { href: '/docs/features/vision', labelKey: 'visionActions' },
  { href: '/docs/features/quick-chat', labelKey: 'quickChat' },
  { href: '/docs/features/oops', labelKey: 'oops' },
  { href: '/docs/features/macros', labelKey: 'macros' },
  { href: '/docs/features/tts', labelKey: 'textToSpeech' },

  // Settings — mirrors real app nav order: General → Audio → AI Engine → Pipelines → Dictation Presets → Macros → Privacy → Account
  { href: '/docs/settings/general', labelKey: 'general' },
  { href: '/docs/settings/control-panel', labelKey: 'controlPanel' },
  { href: '/docs/settings/hotkeys', labelKey: 'hotkeys' },
  { href: '/docs/settings/audio', labelKey: 'audio' },
  { href: '/docs/settings/ai-engine', labelKey: 'aiEngine' },
  { href: '/docs/settings/pipelines', labelKey: 'pipelines' },
  { href: '/docs/settings/dictation-modes', labelKey: 'dictationModes' },
  { href: '/docs/settings/macros', labelKey: 'macros' },
  { href: '/docs/settings/privacy', labelKey: 'privacy' },
  { href: '/docs/settings/account', labelKey: 'account' },

  // Developer Guide
  { href: '/docs/dev/setup', labelKey: 'envSetup' },
  { href: '/docs/dev/migration/v1-to-v2-guide', labelKey: 'v1ToV2Migration' },
  { href: '/docs/dev/architecture/audio-pipeline', labelKey: 'audioPipeline' },
  { href: '/docs/dev/architecture/ui-mvvm', labelKey: 'uiMvvm' },
  { href: '/docs/dev/architecture/threat-model', labelKey: 'threatModel' },
  { href: '/docs/dev/api/stt-providers', labelKey: 'sttProviders' },
  { href: '/docs/dev/api/llm-providers', labelKey: 'llmProviders' },
  { href: '/docs/dev/api/tts-providers', labelKey: 'ttsProviders' },
];

/** Returns the previous and next pages relative to the given slug path (e.g. "inputs/voice"). */
export function getAdjacentDocs(slugPath: string): { prev: DocPage | null; next: DocPage | null } {
  const fullPath = `/docs/${slugPath}`;
  const idx = DOCS_ORDER.findIndex((p) => p.href === fullPath);
  if (idx === -1) return { prev: null, next: null };
  return {
    prev: idx > 0 ? DOCS_ORDER[idx - 1] : null,
    next: idx < DOCS_ORDER.length - 1 ? DOCS_ORDER[idx + 1] : null,
  };
}
