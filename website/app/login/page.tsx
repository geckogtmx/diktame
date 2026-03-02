// SPEC_042: Login page with Supabase Auth UI
'use client'

import { createClient } from '@/lib/supabase/client'
import { Auth } from '@supabase/auth-ui-react'
import { ThemeSupa } from '@supabase/auth-ui-shared'
import { useRouter } from 'next/navigation'
import { useEffect, useState } from 'react'

export default function LoginPage() {
    const router = useRouter()
    const [isMounted, setIsMounted] = useState(false)

    useEffect(() => {
        const supabase = createClient()

        // Check if user is already logged in
        const checkUser = async () => {
            const { data: { user } } = await supabase.auth.getUser()
            if (user) {
                router.push('/dashboard')
            }
        }
        checkUser()

        // Listen for auth changes
        const { data: { subscription } } = supabase.auth.onAuthStateChange((event, session) => {
            if (event === 'SIGNED_IN' && session) {
                router.push('/dashboard')
            }
        })

        setIsMounted(true)

        return () => subscription.unsubscribe()
    }, [router])

    if (!isMounted) {
        return null
    }

    const supabase = createClient()

    const redirectUrl = typeof window !== 'undefined' ? `${window.location.origin}/auth/callback` : 'https://dikta.me/auth/callback'

    return (
        <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center p-4">
            <div className="w-full max-w-md">
                <div className="text-center mb-8">
                    <h1 className="text-4xl font-bold text-white mb-2">dIKta.me</h1>
                    <p className="text-gray-400">Sign in to access your trial credits</p>
                </div>

                <div className="bg-gray-800/50 backdrop-blur-xl rounded-2xl p-8 border border-gray-700">
                    <Auth
                        supabaseClient={supabase}
                        appearance={{
                            theme: ThemeSupa,
                            variables: {
                                default: {
                                    colors: {
                                        brand: '#3b82f6',
                                        brandAccent: '#2563eb',
                                        brandButtonText: 'white',
                                        defaultButtonBackground: '#1f2937',
                                        defaultButtonBackgroundHover: '#374151',
                                        inputBackground: '#111827',
                                        inputBorder: '#374151',
                                        inputBorderHover: '#4b5563',
                                        inputBorderFocus: '#3b82f6',
                                    },
                                },
                            },
                            className: {
                                container: 'auth-container',
                                button: 'auth-button',
                                input: 'auth-input',
                            },
                        }}
                        providers={['google', 'github']}
                        redirectTo={redirectUrl}
                        magicLink={true}
                        view="sign_in"
                        showLinks={true}
                        theme="dark"
                    />
                </div>

                <div className="mt-6 text-center text-sm text-gray-500">
                    <p>By signing up, you get:</p>
                    <ul className="mt-2 space-y-1">
                        <li>✓ 15,000 words OR 15 days trial</li>
                        <li>✓ Access to managed Gemini API</li>
                        <li>✓ Dashboard to track usage</li>
                    </ul>
                </div>
            </div>
        </div>
    )
}
