// SPEC_042: Dashboard page
export const dynamic = 'force-dynamic';
import { createClient } from '@/lib/supabase/server'
import { redirect } from 'next/navigation'

export default async function DashboardPage() {
    const supabase = await createClient()

    const { data: { user }, error } = await supabase.auth.getUser()

    if (error || !user) {
        redirect('/login')
    }

    // Get user profile
    const { data: profile } = await supabase
        .from('profiles')
        .select('*')
        .eq('id', user.id)
        .single()

    const wordsRemaining = profile ? profile.trial_words_quota - profile.trial_words_used : 0
    const wordsUsed = profile?.trial_words_used || 0
    const wordsQuota = profile?.trial_words_quota || 15000
    const percentUsed = (wordsUsed / wordsQuota) * 100

    // Calculate days remaining
    const trialExpires = profile?.trial_expires_at ? new Date(profile.trial_expires_at) : null
    const now = new Date()
    const daysRemaining = trialExpires
        ? Math.max(0, Math.ceil((trialExpires.getTime() - now.getTime()) / (1000 * 60 * 60 * 24)))
        : 0

    return (
        <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black text-white">
            <div className="container mx-auto px-4 py-8 max-w-4xl">
                {/* Header */}
                <div className="flex justify-between items-center mb-8">
                    <div>
                        <h1 className="text-3xl font-bold">Welcome back, {user.user_metadata?.full_name || 'User'}! 🎙️</h1>
                        <p className="text-gray-400 mt-1">{user.email}</p>
                    </div>
                    <form action="/auth/signout" method="post">
                        <button className="px-4 py-2 bg-gray-800 hover:bg-gray-700 rounded-lg transition-colors">
                            Sign Out
                        </button>
                    </form>
                </div>

                {/* Trial Status Card */}
                <div className="bg-gradient-to-br from-blue-900/30 to-purple-900/30 backdrop-blur-xl rounded-2xl p-8 border border-blue-500/20 mb-6">
                    <h2 className="text-xl font-semibold mb-4">Trial Credits</h2>

                    {/* Progress Bar */}
                    <div className="mb-4">
                        <div className="flex justify-between text-sm mb-2">
                            <span>{wordsUsed.toLocaleString()} / {wordsQuota.toLocaleString()} words used</span>
                            <span>{wordsRemaining.toLocaleString()} remaining</span>
                        </div>
                        <div className="w-full bg-gray-700 rounded-full h-3 overflow-hidden">
                            <div
                                className="bg-gradient-to-r from-blue-500 to-purple-500 h-full transition-all duration-500"
                                style={{ width: `${Math.min(percentUsed, 100)}%` }}
                            />
                        </div>
                    </div>

                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-2xl font-bold">{daysRemaining} days left</p>
                            <p className="text-sm text-gray-400">
                                {trialExpires ? `Expires ${trialExpires.toLocaleDateString()}` : 'No expiration set'}
                            </p>
                        </div>
                        <a
                            href="/dashboard/profile"
                            className="px-6 py-3 bg-blue-600 hover:bg-blue-700 rounded-lg font-semibold transition-colors"
                        >
                            Add Your Gemini API Key
                        </a>
                    </div>
                </div>

                {/* Quick Actions */}
                <div className="grid md:grid-cols-3 gap-4 mb-6">
                    <a href="#" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">📥 Download App</h3>
                        <p className="text-sm text-gray-400">Get the Windows desktop app</p>
                    </a>
                    <a href="/docs" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">📚 Documentation</h3>
                        <p className="text-sm text-gray-400">Learn how to use dIKta.me</p>
                    </a>
                    <a href="/dashboard/profile" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">⚙️ Settings</h3>
                        <p className="text-sm text-gray-400">Manage your profile</p>
                    </a>
                </div>

                {/* Stats */}
                <div className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700">
                    <h3 className="font-semibold mb-4">Account Info</h3>
                    <div className="grid md:grid-cols-2 gap-4 text-sm">
                        <div>
                            <p className="text-gray-400">Account created</p>
                            <p className="font-medium">{new Date(user.created_at).toLocaleDateString()}</p>
                        </div>
                        <div>
                            <p className="text-gray-400">License tier</p>
                            <p className="font-medium capitalize">{profile?.license_tier || 'Free'}</p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    )
}
