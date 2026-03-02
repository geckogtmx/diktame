/**
 * Coming Soon page (SPEC_042)
 * Shown at /login and /auth/* while NEXT_PUBLIC_COMING_SOON=true.
 * Remove the env var to disable — no code changes needed.
 */
export default function ComingSoonPage() {
  return (
    <div
      style={{
        minHeight: '100vh',
        background: '#020617',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        fontFamily: 'system-ui, sans-serif',
        color: '#f8fafc',
        textAlign: 'center',
        padding: '2rem',
      }}
    >
      <div style={{ marginBottom: '1.5rem', fontSize: '3rem' }}>🎙️</div>
      <h1
        style={{
          fontSize: 'clamp(2rem, 6vw, 4rem)',
          fontWeight: 800,
          letterSpacing: '-0.02em',
          marginBottom: '1rem',
          color: '#f8fafc',
        }}
      >
        dIKta.me
      </h1>
      <p
        style={{
          fontSize: '1.25rem',
          color: '#94a3b8',
          marginBottom: '0.5rem',
          fontWeight: 500,
        }}
      >
        Coming Soon
      </p>
      <p
        style={{
          fontSize: '0.95rem',
          color: '#475569',
          maxWidth: '380px',
        }}
      >
        We&apos;re putting the finishing touches on something fast, private, and local-first.
        Check back shortly.
      </p>
      <a
        href="/"
        style={{
          marginTop: '2.5rem',
          color: '#3b82f6',
          fontSize: '0.875rem',
          textDecoration: 'none',
        }}
      >
        ← Back to home
      </a>
    </div>
  );
}
