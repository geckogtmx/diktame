import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import { GlassCard } from '../components/GlassCard';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Features - dIKta.me',
  description: 'Explore all 45+ features that make dIKta.me the most powerful voice dictation tool for Windows.',
};

const featureCategories = [
  {
    category: 'Core Modes',
    features: [
      { name: 'Dictate', description: 'Transform voice to text with perfect accuracy' },
      { name: 'Ask', description: 'Query your AI assistant with voice commands' },
      { name: 'Refine', description: 'Edit and improve text with AI assistance' },
      { name: 'Structured Notes', description: 'Timestamped note-taking with AI organization' },
    ],
  },
  {
    category: 'Intelligence',
    features: [
      { name: 'Whisper V3', description: 'State-of-the-art speech recognition' },
      { name: 'Local LLMs', description: 'Run models entirely on your machine' },
      { name: 'API Flexibility', description: 'Use any provider (Anthropic, Google, OpenAI)' },
      { name: 'Smart Context', description: 'Remembers conversation history' },
    ],
  },
  {
    category: 'Privacy & Control',
    features: [
      { name: '100% Local', description: 'Process everything on your device' },
      { name: 'Encrypted Storage', description: 'All data encrypted at rest' },
      { name: 'No Telemetry', description: 'Zero tracking or analytics' },
      { name: 'API Keys Secured', description: 'Encrypted locally, never sent' },
    ],
  },
  {
    category: 'Customization',
    features: [
      { name: 'Global Hotkeys', description: 'Customizable keyboard shortcuts' },
      { name: 'Voice Macros', description: 'Program voice commands in Python' },
      { name: 'Model Switching', description: 'Switch LLMs without reconfiguring' },
      { name: 'Language Support', description: 'English ↔ Spanish (more coming)' },
    ],
  },
];

export default function FeaturesPage() {
  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Hero */}
      <section className="pt-32 pb-20 sm:pb-32 relative overflow-hidden">
        <div className="absolute inset-0 -z-10">
          <div className="absolute inset-0 bg-gradient-to-b from-blue-900/20 via-transparent to-transparent" />
          <div className="absolute top-1/4 right-0 w-96 h-96 bg-blue-600/10 rounded-full blur-3xl opacity-40" />
        </div>

        <Container>
          <div className="text-center max-w-3xl mx-auto">
            <h1 className="text-5xl sm:text-6xl md:text-7xl font-bold mb-6">
              45+ Powerful Features
            </h1>
            <p className="text-xl text-gray-400 mb-8">
              Everything you need to revolutionize your workflow. Fast, smart, and completely private.
            </p>
          </div>
        </Container>
      </section>

      {/* Feature Categories */}
      <section className="py-20 sm:py-32">
        <Container>
          <div className="space-y-20">
            {featureCategories.map((category) => (
              <div key={category.category}>
                <h2 className="text-3xl sm:text-4xl font-bold text-white mb-12">
                  {category.category}
                </h2>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {category.features.map((feature) => (
                    <GlassCard key={feature.name}>
                      <h3 className="text-xl font-semibold text-white mb-2">
                        {feature.name}
                      </h3>
                      <p className="text-gray-400">{feature.description}</p>
                    </GlassCard>
                  ))}
                </div>
              </div>
            ))}
          </div>
        </Container>
      </section>

      {/* CTA */}
      <section className="py-20 sm:py-32 border-t border-white/10">
        <Container>
          <div className="text-center max-w-2xl mx-auto">
            <h2 className="text-4xl sm:text-5xl font-bold text-white mb-6">
              Ready to transform your workflow?
            </h2>
            <p className="text-lg text-gray-400 mb-8">
              Download dIKta.me today and experience the difference that local AI makes.
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <a
                href="#"
                className="px-8 py-3 bg-orange-500 hover:bg-orange-600 rounded-lg font-semibold transition-colors"
              >
                Download Now
              </a>
              <a
                href="/login"
                className="px-8 py-3 border border-blue-500/20 text-blue-400 hover:bg-blue-500/10 rounded-lg font-semibold transition-colors"
              >
                View Dashboard
              </a>
            </div>
          </div>
        </Container>
      </section>

      <Footer />
    </main>
  );
}
