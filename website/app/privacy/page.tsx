import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Privacy Policy - dIKta.me',
  description: 'How dIKta.me handles your data. Short version: the desktop app is air-gapped and never phones home. The website collects only what it needs.',
};

export default function PrivacyPage() {
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-3xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-4 tracking-tight">Privacy Policy</h1>
            <p className="text-muted mb-12">Last updated: March 2026</p>

            <div className="prose prose-invert max-w-none space-y-10 text-gray-300 leading-relaxed">
              {/* Intro */}
              <section>
                <p className="text-lg">
                  We believe your data is yours. That&apos;s the whole point of dIKta.me &mdash; a voice engine
                  that runs locally on your machine. This policy explains what happens on our <em>website</em>,
                  because the desktop app itself doesn&apos;t collect anything.
                </p>
              </section>

              {/* The Desktop App */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">The Desktop App</h2>
                <p>
                  dIKta.me processes everything on your computer. Your voice, your text, your dictation &mdash;
                  none of it leaves your machine when running in local mode. There is:
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li><strong className="text-white">No telemetry.</strong> We don&apos;t phone home.</li>
                  <li><strong className="text-white">No voice data collection.</strong> Audio stays on your device.</li>
                  <li><strong className="text-white">No keystroke logging.</strong> We inject text, we don&apos;t record it.</li>
                  <li><strong className="text-white">No clipboard snooping.</strong> Clipboard is used briefly for injection, then restored.</li>
                </ul>
                <p className="mt-4">
                  If you use cloud features (like Deepgram streaming or wallet credits), your audio is sent to
                  third-party APIs for processing. Those providers have their own privacy policies. We don&apos;t
                  store your audio or transcripts on our servers.
                </p>
              </section>

              {/* What the Website Collects */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">What the Website Collects</h2>

                <h3 className="text-lg font-bold text-white mt-6 mb-2">Waitlist Signup</h3>
                <p>
                  When you join the waitlist, we collect your <strong className="text-white">name</strong> and{' '}
                  <strong className="text-white">email address</strong>. We use this to notify you when dIKta.me
                  is ready and to send product updates. That&apos;s it.
                </p>

                <h3 className="text-lg font-bold text-white mt-6 mb-2">Account Registration</h3>
                <p>
                  If you create an account (for cloud features or wallet credits), we store your email,
                  display name, and encrypted API keys via Supabase. You can delete your account and all
                  associated data at any time from the dashboard.
                </p>

                <h3 className="text-lg font-bold text-white mt-6 mb-2">Analytics</h3>
                <p>
                  We use <strong className="text-white">Vercel Web Analytics</strong> to understand how people
                  use our website. This tracks anonymous page views &mdash; no cookies, no personal data,
                  no cross-site tracking. It&apos;s{' '}
                  <a href="https://vercel.com/docs/analytics/privacy-policy" target="_blank" rel="noopener noreferrer" className="text-primary hover:underline">
                    privacy-focused by design
                  </a>.
                </p>
              </section>

              {/* Third Parties */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Third-Party Services</h2>
                <ul className="list-disc pl-6 space-y-2">
                  <li><strong className="text-white">Vercel</strong> &mdash; Website hosting and analytics</li>
                  <li><strong className="text-white">Supabase</strong> &mdash; Authentication and database</li>
                  <li><strong className="text-white">Resend</strong> &mdash; Email delivery for waitlist and notifications</li>
                  <li><strong className="text-white">Deepgram</strong> &mdash; Cloud speech-to-text (only when using cloud mode)</li>
                  <li><strong className="text-white">Google Gemini</strong> &mdash; Cloud LLM processing (only when using cloud mode)</li>
                </ul>
              </section>

              {/* Cookies */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Cookies</h2>
                <p>
                  We only use essential cookies for authentication (Supabase session tokens).
                  No tracking cookies, no advertising cookies, no third-party cookie nonsense.
                </p>
              </section>

              {/* Your Rights */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Your Rights</h2>
                <ul className="list-disc pl-6 space-y-2">
                  <li>Delete your account and all data from the dashboard at any time</li>
                  <li>Request removal of your waitlist entry by emailing us</li>
                  <li>Ask us what data we have about you</li>
                </ul>
              </section>

              {/* Contact */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Contact</h2>
                <p>
                  Questions about privacy? Reach out at{' '}
                  <a href="mailto:privacy@dikta.me" className="text-primary hover:underline">
                    privacy@dikta.me
                  </a>
                </p>
              </section>
            </div>
          </div>
        </Container>
      </div>

      <Footer />
    </main>
  );
}
