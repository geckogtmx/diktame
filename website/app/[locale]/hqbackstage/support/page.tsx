export default function AdminSupportPage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Support</h1>

      <div className="rounded-xl border border-white/10 bg-white/5 p-8 text-center space-y-4">
        <div className="text-4xl">📬</div>
        <h2 className="text-lg font-semibold">Support Ticket System</h2>
        <p className="text-gray-400 max-w-md mx-auto">
          Coming soon. For now, check support emails via ImprovMX forwarding to your inbox.
        </p>
        <div className="pt-2">
          <a
            href="mailto:support@dikta.me"
            className="inline-block px-4 py-2 rounded-lg bg-white/10 text-sm hover:bg-white/20 transition-colors"
          >
            support@dikta.me
          </a>
        </div>
      </div>

      <div className="rounded-lg border border-white/10 bg-white/5 p-5 text-sm text-gray-400 space-y-2">
        <p className="font-medium text-white">Future Plans</p>
        <ul className="list-disc list-inside space-y-1">
          <li>GitHub Issues integration for bug reports</li>
          <li>In-app feedback collection</li>
          <li>User-facing ticket submission form</li>
          <li>Email-to-ticket via ImprovMX + webhook</li>
        </ul>
      </div>
    </div>
  );
}
