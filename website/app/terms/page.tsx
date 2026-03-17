import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { Container } from '../components/Container';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Terms of Service - dIKta.me',
  description: 'Terms of service for dIKta.me. One-time purchase, no subscriptions, source-available license. Plain English, no surprises.',
};

export default function TermsPage() {
  return (
    <main className="min-h-screen bg-black text-white selection:bg-primary/30">
      <Navbar />

      <div className="relative pt-32 pb-20">
        <Container>
          <div className="max-w-3xl mx-auto">
            <h1 className="text-4xl md:text-5xl font-bold mb-4 tracking-tight">Terms of Service</h1>
            <p className="text-muted mb-12">Last updated: March 2026</p>

            <div className="prose prose-invert max-w-none space-y-10 text-gray-300 leading-relaxed">
              {/* Intro */}
              <section>
                <p className="text-lg">
                  These are the terms for using dIKta.me. We&apos;ve kept them short and human-readable
                  because nobody enjoys 40 pages of legal text.
                </p>
              </section>

              {/* The Purchase */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">The Deal</h2>
                <p>
                  dIKta.me is a one-time purchase. You pay once, you own a license to use the software
                  forever. No subscriptions, no recurring charges, no &ldquo;your trial has expired&rdquo;
                  pop-ups.
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li>
                    <strong className="text-white">Power Version</strong> &mdash; One-time payment grants you
                    a perpetual license to use dIKta.me with all features unlocked, plus access to the
                    source code for personal use.
                  </li>
                  <li>
                    <strong className="text-white">Free Trial</strong> &mdash; You get complimentary cloud
                    credits to test the app. No payment required. Credits are non-transferable and may
                    expire.
                  </li>
                </ul>
              </section>

              {/* License */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Source-Available License</h2>
                <p>
                  The Power Version includes access to the dIKta.me source code. This is a
                  source-available license, not an open-source license. Here&apos;s what that means:
                </p>
                <ul className="list-disc pl-6 space-y-2 mt-4">
                  <li><strong className="text-white">You can</strong> read, study, and learn from the code</li>
                  <li><strong className="text-white">You can</strong> modify it for your own personal use</li>
                  <li><strong className="text-white">You cannot</strong> redistribute, resell, or create competing products from it</li>
                  <li><strong className="text-white">You cannot</strong> remove or alter license notices</li>
                </ul>
              </section>

              {/* Cloud Services */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Cloud Services</h2>
                <p>
                  If you use cloud features (wallet credits, Deepgram streaming), those services are
                  provided on a best-effort basis. We route your requests through third-party APIs
                  (Deepgram, Google Gemini) and charge credits accordingly.
                </p>
                <p className="mt-4">
                  Cloud service availability depends on third-party uptime. We don&apos;t guarantee
                  100% availability, but we do our best. If a cloud provider goes down, local mode
                  keeps working &mdash; that&apos;s the whole point.
                </p>
              </section>

              {/* No Warranty */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">No Warranty</h2>
                <p>
                  dIKta.me is provided &ldquo;as is&rdquo; without warranty of any kind. We work hard
                  to make it great, but we can&apos;t guarantee it will work perfectly in every
                  situation on every machine. We provide best-effort support and regular updates.
                </p>
              </section>

              {/* Your Account */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Your Account</h2>
                <p>
                  You can delete your account and all associated data at any time from the dashboard.
                  When you delete your account, we remove your profile, API keys, and usage history.
                  Waitlist entries can be removed by emailing us.
                </p>
              </section>

              {/* Acceptable Use */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Play Nice</h2>
                <p>
                  Don&apos;t use dIKta.me for anything illegal. Don&apos;t abuse the cloud
                  credits system. Don&apos;t try to reverse-engineer the licensing mechanism.
                  Basically: be a reasonable human being.
                </p>
              </section>

              {/* Changes */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Changes to These Terms</h2>
                <p>
                  We may update these terms from time to time. If we make significant changes,
                  we&apos;ll notify you via email. Continued use of the service after changes
                  constitutes acceptance of the updated terms.
                </p>
              </section>

              {/* Contact */}
              <section>
                <h2 className="text-2xl font-bold text-white mb-4">Contact</h2>
                <p>
                  Questions? Reach out at{' '}
                  <a href="mailto:legal@dikta.me" className="text-primary hover:underline">
                    legal@dikta.me
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
