import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import { GlassCard } from '../components/GlassCard';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Documentation - dIKta.me',
  description: 'Complete documentation for dIKta.me — setup guides, core feature walkthroughs, AI engine configuration, hotkey customization, and developer architecture references.',
};

import Link from 'next/link';

const userDocs = [
  {
    title: 'Overview',
    description: 'Learn how to download, install, and troubleshoot dIKta.me',
    links: [
      { href: '/docs/getting-started', label: 'Getting Started Guide' },
      { href: '/docs/troubleshooting', label: 'Troubleshooting' },
    ]
  },
  {
    title: 'Core Features',
    description: 'Master the core modes of the application',
    links: [
      { href: '/docs/features/dictation', label: 'Dictation' },
      { href: '/docs/features/refine', label: 'Refine (Editing)' },
      { href: '/docs/features/quick-chat', label: 'Quick Chat Overlay' },
    ]
  },
  {
    title: 'Utility Pipelines',
    description: 'Quick voice-driven tools for everyday tasks',
    links: [
      { href: '/docs/features/ask', label: 'Ask (Q&A)' },
      { href: '/docs/features/translate', label: 'Translate' },
      { href: '/docs/features/note', label: 'Note Taking' },
      { href: '/docs/features/oops', label: 'Oops (Undo/Fix)' },
    ]
  },
  {
    title: 'Configuration',
    description: 'Customize every deeply integrated setting',
    links: [
      { href: '/docs/settings/general', label: 'General & Audio' },
      { href: '/docs/settings/ai-engine', label: 'AI Engine & Models' },
      { href: '/docs/settings/hotkeys', label: 'Hotkeys' },
      { href: '/docs/settings/modes', label: 'Modes & Profiles' },
    ]
  }
];

const devDocs = [
  {
    title: 'Developer Guide',
    description: 'Architecture, APIs, and contribution guidelines',
    links: [
      { href: '/docs/dev/setup', label: 'Environment Setup' },
      { href: '/docs/dev/architecture/ui-mvvm', label: 'Architecture Patterns' },
      { href: '/docs/dev/api/stt-providers', label: 'API & Extensibility' },
      { href: '/docs/dev/migration/v1-to-v2-guide', label: 'V1 to V2 Migration' },
    ]
  }
];

export default function DocsPage() {
  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Hero */}
      <section className="pt-32 pb-20 sm:pb-32 relative overflow-hidden">
        <div className="absolute inset-0 -z-10">
          <div className="absolute inset-0 bg-gradient-to-b from-blue-900/20 via-transparent to-transparent" />
        </div>

        <Container>
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-5xl sm:text-6xl md:text-7xl font-bold mb-6">
              Documentation
            </h1>
            <p className="text-xl text-gray-400">
              Complete guides to get the most out of dIKta.me.
            </p>
          </div>
        </Container>
      </section>

      {/* Documentation Grid */}
      <section className="py-20 sm:py-32">
        <Container>
          {/* User Documentation Section */}
          <div className="mb-12">
            <h2 className="text-3xl font-bold text-white mb-4">User Documentation</h2>
            <p className="text-gray-400">Guides for setting up, configuring, and using the dIKta.me application.</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-16">
            {userDocs.map((doc) => (
              <GlassCard key={doc.title}>
                <h2 className="text-2xl font-bold text-white mb-3">{doc.title}</h2>
                <p className="text-gray-400 mb-6">{doc.description}</p>
                <ul className="space-y-2">
                  {doc.links.map((link) => (
                    <li key={link.href} className="flex items-center gap-2 text-gray-300">
                      <span className="text-blue-400">→</span>
                      <Link href={link.href} className="hover:text-blue-400 transition">
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </GlassCard>
            ))}
          </div>

          {/* Developer Documentation Section */}
          <div className="mb-12 mt-24 border-t border-white/10 pt-16">
            <h2 className="text-3xl font-bold text-white mb-4">Developer Documentation</h2>
            <p className="text-gray-400">Architecture deep dives, plugin APIs, and setup guides for contributors.</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-16">
            {devDocs.map((doc) => (
              <GlassCard key={doc.title}>
                <h2 className="text-2xl font-bold text-white mb-3">{doc.title}</h2>
                <p className="text-gray-400 mb-6">{doc.description}</p>
                <ul className="space-y-2">
                  {doc.links.map((link) => (
                    <li key={link.href} className="flex items-center gap-2 text-gray-300">
                      <span className="text-blue-400">→</span>
                      <Link href={link.href} className="hover:text-blue-400 transition">
                        {link.label}
                      </Link>
                    </li>
                  ))}
                </ul>
              </GlassCard>
            ))}
          </div>

          {/* Quick Start */}
          <div className="max-w-2xl mx-auto">
            <GlassCard>
              <h3 className="text-2xl font-bold text-white mb-6">Quick Start</h3>
              <ol className="space-y-6">
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center font-bold">
                    1
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">Download dIKta.me</h4>
                    <p className="text-gray-400">
                      Get the latest version from the download page. Supports Windows 10+.
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center font-bold">
                    2
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">Run the Installer</h4>
                    <p className="text-gray-400">
                      Extract and run the installer. Choose your preferred installation path.
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center font-bold">
                    3
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">Configure Your Model</h4>
                    <p className="text-gray-400">
                      Select a speech recognition model. Local (Whisper) or API key.
                    </p>
                  </div>
                </li>
                <li className="flex gap-4">
                  <span className="flex-shrink-0 w-8 h-8 rounded-full bg-blue-500 text-white flex items-center justify-center font-bold">
                    4
                  </span>
                  <div>
                    <h4 className="font-semibold text-white mb-1">Press Your Hotkey</h4>
                    <p className="text-gray-400">
                      Default hotkey is Ctrl+Alt+D. Try dictating your first text!
                    </p>
                  </div>
                </li>
              </ol>
            </GlassCard>
          </div>
        </Container>
      </section>

      {/* External Resources */}
      <section className="py-20 sm:py-32 border-t border-white/10">
        <Container>
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-4xl font-bold text-white mb-6">Need More Help?</h2>
            <p className="text-lg text-gray-400 mb-8">
              Check out our community resources, GitHub repository, or contact us directly.
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <a
                href="https://github.com/geckogtmx/diktame"
                className="px-8 py-3 bg-gray-800 hover:bg-gray-700 rounded-lg font-semibold transition-colors"
              >
                View on GitHub
              </a>
              <a
                href="https://github.com/geckogtmx/diktame/issues"
                className="px-8 py-3 border border-blue-500/20 text-blue-400 hover:bg-blue-500/10 rounded-lg font-semibold transition-colors"
              >
                Report an Issue
              </a>
            </div>
          </div>
        </Container>
      </section>

      <Footer />
    </main>
  );
}
