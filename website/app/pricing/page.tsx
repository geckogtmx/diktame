import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { PricingSection } from '../components/PricingSection';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Pricing - dIKta.me',
  description: 'One-time $25 purchase for unlimited local voice dictation. No subscriptions, no word limits, no cloud dependency. Free trial with complimentary credits included.',
};

export default function PricingPage() {
  return (
    <main className="min-h-screen bg-black text-white">
      <Navbar />

      {/* Pricing Section starts at top */}
      <div className="pt-20">
        <PricingSection />
      </div>

      <Footer />
    </main>
  );
}
