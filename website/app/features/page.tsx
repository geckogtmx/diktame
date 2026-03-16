import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import { GlassCard } from '../components/GlassCard';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Features - dIKta.me',
  description: 'Explore 45+ features including local Whisper STT, AI-powered editing, voice snippets, real-time translation, and text-to-speech — all running privately on your Windows PC.',
};

const featureCategories = [
  {
    category: 'Core Modes',
    features: [
      { name: 'Dictate', description: 'Transform voice to text with perfect accuracy' },
      { name: 'Ask', description: 'Query your AI assistant with voice commands' },
      { name: 'Refine', description: 'Edit and improve text with AI assistance' },
      { name: 'Quick Chat', description: 'Hotkey-activated floating LLM chat overlay' },
    ],
  },
  {
    category: 'Intelligence',
    features: [
      { name: 'Whisper V3 Turbo', description: 'Native state-of-the-art speech recognition' },
      { name: 'Local LLMs', description: 'Run Gemma 3 or Llama 3 entirely on your machine' },
      { name: 'Model Agnostic', description: 'Switch between any cloud or local provider' },
      { name: 'Audio Ducking', description: 'Automatically lowers background music while recording' },
    ],
  },
  {
    category: 'Privacy & Tech',
    features: [
      { name: '100% Local', description: 'Process everything on your device, air-gapped' },
      { name: 'Native C# Build', description: 'High-performance self-contained executable — no runtime install needed' },
      { name: 'No Telemetry', description: 'Zero tracking, zero analytics, zero cloud dependencies' },
      { name: 'API Keys Secured', description: 'OS-level encryption for all your credentials' },
    ],
  },
  {
    category: 'Customization',
    features: [
      { name: 'Global Hotkeys', description: 'Fully customizable keyboard shortcuts' },
      { name: 'Voice Snippets', description: 'Phrase-triggered automatic text expansion' },
      { name: 'Sound Feedback', description: 'Reactive audio cues for all pipeline events' },
      { name: '+Key Automation', description: 'Auto-inject Enter, Tab, or spaces after dictation' },
    ],
  },
  {
    category: 'On the Roadmap',
    features: [
      { name: 'Connectors', description: 'Native integrations with Slack, Notion, and more' },
      { name: 'Meetings & Scribe', description: 'Intelligent, local-first meeting transcription' },
      { name: 'Vision & See', description: 'Screen-aware AI for contextual understanding' },
      { name: 'Memory Layer', description: 'Persistent contextual memory for your workflow' },
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
              Join the waitlist and be the first to experience the difference that local AI makes.
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <a
                href="/waitlist"
                className="px-8 py-3 bg-orange-500 hover:bg-orange-600 rounded-lg font-semibold transition-colors"
              >
                Join Waitlist
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
