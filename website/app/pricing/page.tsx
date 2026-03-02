import { Navbar } from '../components/Navbar';
import { Footer } from '../components/Footer';
import { PricingSection } from '../components/PricingSection';
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Pricing - dIKta.me',
  description: 'Simple, transparent pricing. One-time purchase, no subscriptions, no hidden fees.',
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
