/**
 * Generates a branded HTML page that redirects to the diktame:// deeplink
 * for the desktop app, with a nice "You're signed in" message + dashboard link.
 */
export function buildDeeplinkPage(deeplinkUrl: string): string {
  // Inline the logo SVG so it works without any asset references
  const logoSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 250 250" fill="none">
<path d="M 124.67,244 C 58.94,244 5.67,190.48 5.67,125.46 C 5.67,60.91 58.94,6 124.67,6 C 190.71,6 244.33,59.52 244.33,125.46 C 244.33,190.48 190.71,244 124.67,244 Z M 124.67,9.13 C 60.78,9.13 9.02,60.45 9.02,125.46 C 9.02,189.77 60.78,240.87 124.67,240.87 C 189.22,240.87 241.19,189.77 241.19,125.46 C 241.19,60.45 189.22,9.13 124.67,9.13 Z" fill="#C55A28"/>
<path d="M 124.41,18.86 C 66.91,18.86 18.32,66.23 18.32,125.46 C 18.32,183.93 66.08,229.76 123.89,229.76 C 183.91,229.76 230.78,182.84 230.78,125.46 C 230.78,68.08 183.61,18.86 124.41,18.86 Z" fill="#C55A28"/>
<path d="M 65.34,67 H 84.18 V 182.79 H 65.34 V 67 Z" fill="white"/>
<path d="M 96.12,67 H 114.96 V 117.25 C 105.95,121.67 100.91,128.09 96.12,135.08 V 67 Z" fill="white"/>
<path d="M 118.21,115.31 L 157.98,67 H 180.61 L 142.97,113.19 C 136.47,111.62 127.22,111.17 118.21,115.31 Z" fill="white"/>
<path d="M 95.75,163.79 V 182.79 H 114.59 V 161.63 C 114.59,145.98 121.78,132.29 137.16,132.29 C 147.81,132.29 156.18,141.91 161.92,145.87 C 167.93,149.96 174.19,151.11 181.11,150.9 C 197.99,150.38 210.49,136.98 210.49,122.62 C 210.49,108.84 200.71,96.88 186.78,96.88 C 174.84,96.88 166.47,106.41 166.47,116.4 C 166.47,126.02 173.91,132.29 181.85,132.29 C 189.78,132.29 192.12,125.26 192.12,122.42 C 192.12,119.38 189.78,116.4 187.91,116.4 C 188.84,119.58 186.38,122.22 183.71,122.22 C 179.88,122.22 177.72,119.58 177.72,116.8 C 177.72,111.72 181.01,108.94 185.78,108.94 C 194.93,108.94 199.13,116.08 199.13,122.12 C 199.13,131.22 190.71,139.42 181.75,139.42 C 173.41,139.42 167.01,133.86 164.75,131.73 C 154.88,122.84 145.62,118.91 134.57,118.91 C 112.04,118.91 95.75,137.31 95.75,163.79 Z" fill="white"/>
<path d="M 124.71,144.92 C 128.06,140.98 130.92,138.34 136.65,138.34 C 141.13,138.34 145.11,140.47 147.67,143.81 L 182.37,182.79 H 159.02 L 124.71,144.92 Z" fill="white"/>
</svg>`;

  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>dIKta.me — Signed In</title>
  <meta http-equiv="refresh" content="0;url=${deeplinkUrl}"/>
  <link rel="preconnect" href="https://fonts.googleapis.com"/>
  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700&display=swap" rel="stylesheet"/>
  <style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    body {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(to bottom, #111827, #000000);
      font-family: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
      color: #f8fafc;
    }
    .card {
      text-align: center;
      max-width: 420px;
      width: 90%;
      background: rgba(15, 23, 42, 0.6);
      backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 1.25rem;
      padding: 3rem 2.5rem;
    }
    .logo { margin-bottom: 1.5rem; }
    .check {
      width: 48px; height: 48px;
      margin: 0 auto 1rem;
      border-radius: 50%;
      background: rgba(34, 197, 94, 0.15);
      display: flex; align-items: center; justify-content: center;
    }
    .check svg { width: 24px; height: 24px; }
    h1 { font-size: 1.5rem; font-weight: 700; margin-bottom: 0.5rem; }
    p { color: #94a3b8; font-size: 0.95rem; line-height: 1.5; margin-bottom: 1.75rem; }
    .actions { display: flex; flex-direction: column; gap: 0.75rem; }
    .btn {
      display: inline-block;
      padding: 0.75rem 1.5rem;
      border-radius: 0.625rem;
      font-family: inherit;
      font-size: 0.9rem;
      font-weight: 600;
      text-decoration: none;
      transition: opacity 0.2s;
      cursor: pointer;
      border: none;
    }
    .btn:hover { opacity: 0.85; }
    .btn-primary { background: #f97316; color: white; }
    .btn-secondary {
      background: transparent;
      color: #94a3b8;
      border: 1px solid rgba(255, 255, 255, 0.12);
    }
    .btn-secondary:hover { color: #f8fafc; border-color: rgba(255, 255, 255, 0.25); }
    .footer { margin-top: 2rem; font-size: 0.8rem; color: #475569; }
  </style>
</head>
<body>
  <div class="card">
    <div class="logo">${logoSvg}</div>
    <div class="check">
      <svg viewBox="0 0 24 24" fill="none" stroke="#22c55e" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
        <polyline points="20 6 9 17 4 12"/>
      </svg>
    </div>
    <h1>You&rsquo;re signed in!</h1>
    <p>The dIKta.me desktop app should open automatically. You can close this tab, or head to your dashboard.</p>
    <div class="actions">
      <a href="/dashboard" class="btn btn-primary">Go to Dashboard</a>
      <button class="btn btn-secondary" onclick="window.close()">Close this tab</button>
    </div>
    <div class="footer">dikta.me</div>
  </div>
  <script>window.location.href=${JSON.stringify(deeplinkUrl)};</script>
</body>
</html>`;
}
