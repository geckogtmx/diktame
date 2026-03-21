import { getTranslations } from 'next-intl/server';
import { Navbar } from '@/app/components/Navbar';
import { DashboardSidebar } from './DashboardSidebar';

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  const t = await getTranslations({ locale, namespace: 'metadata' });

  return {
    title: t('dashboardTitle'),
    robots: 'noindex, nofollow',
  };
}

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <>
      <Navbar />
      <div className="pt-16 min-h-screen bg-gradient-to-b from-gray-900 to-black text-white">
        <div className="flex">
          <DashboardSidebar />
          <main className="flex-1 min-w-0">{children}</main>
        </div>
      </div>
    </>
  );
}
