import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import Link from 'next/link';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About - dIKta.me',
  description: 'dIKta.me is a local-first, privacy-first voice dictation engine for Windows. Built by a solo indie developer, powered by on-device AI.',
};

export default function AboutPage() {
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-3xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-12 tracking-tight">About dIKta.me</h1>

            <div className="prose prose-invert max-w-none space-y-10 text-gray-300 leading-relaxed">
              {/* What */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">What is dIKta.me?</h2>
                <p className="text-lg">
                  dIKta.me is a voice dictation engine for Windows that runs entirely on your machine.
                  Press a hotkey, speak, and your words appear in whatever app you&apos;re using &mdash;
                  Word, Slack, your browser, a terminal, anything.
                </p>
                <p>
                  It&apos;s not just transcription. A local AI model cleans up your speech, removes filler
                  words, formats your text, and can even follow instructions like &ldquo;write this as a
                  professional email&rdquo; or &ldquo;translate this to Spanish&rdquo;.
                </p>
              </section>

              {/* Why */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Why does this exist?</h2>
                <p>
                  Every voice tool out there wants to send your audio to the cloud. They want a monthly
                  subscription. They cap your words. They own your data.
                </p>
                <p>
                  dIKta.me was born from a simple frustration: why should I need an internet connection
                  and a recurring payment just to talk to my computer? Your voice is personal. Your
                  ideas are yours. The processing should happen on <em>your</em> hardware.
                </p>
              </section>

              {/* How */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">How it&apos;s built</h2>
                <p>
                  dIKta.me V2 is a ground-up rewrite in C# with WinUI 3, designed for performance and
                  a native Windows experience.
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li>
                    <strong className="text-white">Speech-to-Text:</strong> Whisper V3 Turbo running locally
                    via faster-whisper, or cloud streaming via Deepgram
                  </li>
                  <li>
                    <strong className="text-white">AI Brain:</strong> Any Ollama-compatible model (Gemma, Llama,
                    Mistral) for formatting, cleanup, and intelligence
                  </li>
                  <li>
                    <strong className="text-white">Text-to-Speech:</strong> Kokoro ONNX for fully local voice
                    synthesis
                  </li>
                  <li>
                    <strong className="text-white">Cloud option:</strong> Optional wallet credits for Deepgram +
                    Gemini Flash when you want cloud speed without managing API keys
                  </li>
                </ul>
                <p className="mt-4">
                  The Power Version is source-available &mdash; you can read every line of code and know
                  exactly what&apos;s running on your machine.
                </p>
              </section>

              {/* Who */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">The developer</h2>
                <p>
                  dIKta.me is a solo indie project, co-authored with AI tools. No VC funding, no
                  corporate agenda, no growth-at-all-costs mindset. Just one developer who wanted
                  better voice tools and decided to build them.
                </p>
              </section>

              {/* Support */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Support the project</h2>
                <p>
                  dIKta.me is independent and self-funded. If you want to help keep it that way:
                </p>
                <div className="flex flex-wrap gap-4 mt-6">
                  <Link
                    href="/waitlist"
                    className="px-6 py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/80 transition-colors"
                  >
                    Join the Waitlist
                  </Link>
                  <a
                    href="https://ko-fi.com/geckogtmx"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="px-6 py-3 bg-white/5 border border-white/20 text-white font-bold rounded-lg hover:bg-white/10 transition-colors"
                  >
                    Support on Ko-Fi
                  </a>
                </div>
              </section>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
