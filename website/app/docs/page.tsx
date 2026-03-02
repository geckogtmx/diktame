import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import { GlassCard } from '../components/GlassCard';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Documentation - dIKta.me',
  description: 'Getting started with dIKta.me. Installation, configuration, and troubleshooting guides.',
};

const docs = [
  {
    title: 'Getting Started',
    description: 'Learn how to download, install, and activate dIKta.me',
    sections: [
      'System Requirements',
      'Download & Installation',
      'First Run Setup',
      'Activation & Trial Credits',
    ],
  },
  {
    title: 'Core Features',
    description: 'Master the four core modes of dIKta.me',
    sections: [
      'Dictate Mode',
      'Ask Mode (LLM)',
      'Refine Mode (Editing)',
      'Structured Notes',
    ],
  },
  {
    title: 'Configuration',
    description: 'Customize dIKta.me to match your workflow',
    sections: [
      'Hotkey Configuration',
      'Model Selection',
      'API Keys Setup',
      'Language Settings',
    ],
  },
  {
    title: 'Advanced',
    description: 'Power features for advanced users',
    sections: [
      'Voice Macros (Python)',
      'Custom Models',
      'Offline Setup',
      'Data Encryption',
    ],
  },
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
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8 mb-16">
            {docs.map((doc) => (
              <GlassCard key={doc.title}>
                <h2 className="text-2xl font-bold text-white mb-3">{doc.title}</h2>
                <p className="text-gray-400 mb-6">{doc.description}</p>
                <ul className="space-y-2">
                  {doc.sections.map((section) => (
                    <li key={section} className="flex items-center gap-2 text-gray-300">
                      <span className="text-blue-400">→</span>
                      <a href="#" className="hover:text-blue-400 transition">
                        {section}
                      </a>
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
                href="https://github.com/geckogtmx/diktate"
                className="px-8 py-3 bg-gray-800 hover:bg-gray-700 rounded-lg font-semibold transition-colors"
              >
                View on GitHub
              </a>
              <a
                href="#contact"
                className="px-8 py-3 border border-blue-500/20 text-blue-400 hover:bg-blue-500/10 rounded-lg font-semibold transition-colors"
              >
                Contact Support
              </a>
            </div>
          </div>
        </Container>
      </section>

      <Footer />
    </main>
  );
}
