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

    return (
        <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black text-white">
            <div className="container mx-auto px-4 py-8 max-w-4xl">
                {/* Header */}
                <div className="flex justify-between items-center mb-8">
                    <div>
                        <h1 className="text-3xl font-bold">Welcome, {user.user_metadata?.full_name || profile?.name || 'User'}</h1>
                        <p className="text-gray-400 mt-1">{user.email}</p>
                    </div>
                    <form action="/auth/signout" method="post">
                        <button className="px-4 py-2 bg-gray-800 hover:bg-gray-700 rounded-lg transition-colors">
                            Sign Out
                        </button>
                    </form>
                </div>

                {/* Account Info */}
                <div className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 mb-6">
                    <h3 className="font-semibold mb-4">Account</h3>
                    <div className="grid md:grid-cols-2 gap-4 text-sm">
                        <div>
                            <p className="text-gray-400">Email</p>
                            <p className="font-medium">{user.email}</p>
                        </div>
                        <div>
                            <p className="text-gray-400">Member since</p>
                            <p className="font-medium">{new Date(user.created_at).toLocaleDateString()}</p>
                        </div>
                        <div>
                            <p className="text-gray-400">License tier</p>
                            <p className="font-medium capitalize">{profile?.license_tier || 'Free'}</p>
                        </div>
                    </div>
                </div>

                {/* Quick Actions */}
                <div className="grid md:grid-cols-3 gap-4 mb-6">
                    <a href="/waitlist" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">Get the App</h3>
                        <p className="text-sm text-gray-400">Join the waitlist for early access</p>
                    </a>
                    <a href="/docs" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">Documentation</h3>
                        <p className="text-sm text-gray-400">Learn how to use dIKta.me</p>
                    </a>
                    <a href="/dashboard/profile" className="bg-gray-800/50 backdrop-blur-xl rounded-xl p-6 border border-gray-700 hover:border-blue-500 transition-colors">
                        <h3 className="font-semibold mb-2">Settings</h3>
                        <p className="text-sm text-gray-400">Manage your profile</p>
                    </a>
                </div>
            </div>
        </div>
    )
}
