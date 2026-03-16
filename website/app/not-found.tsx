import Link from 'next/link';

export default function NotFound() {
  return (
    <main className="min-h-screen bg-black text-white flex items-center justify-center px-4">
      <div className="text-center max-w-md">
        <h1 className="text-6xl font-bold text-white mb-4">404</h1>
        <h2 className="text-2xl font-semibold text-gray-300 mb-6">Page Not Found</h2>
        <p className="text-gray-400 mb-8">
          The page you&apos;re looking for doesn&apos;t exist or has been moved.
        </p>
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <Link
            href="/"
            className="px-6 py-3 bg-blue-600 hover:bg-blue-700 rounded-lg font-semibold transition-colors"
          >
            Go Home
          </Link>
          <Link
            href="/docs"
            className="px-6 py-3 border border-white/20 hover:bg-white/5 rounded-lg font-semibold transition-colors"
          >
            Documentation
          </Link>
          <Link
            href="/features"
            className="px-6 py-3 border border-white/20 hover:bg-white/5 rounded-lg font-semibold transition-colors"
          >
            Features
          </Link>
        </div>
      </div>
    </main>
  );
}
