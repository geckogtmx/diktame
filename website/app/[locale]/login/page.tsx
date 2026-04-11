// SPEC_042: Login page with Supabase Auth UI
'use client'

import { createClient } from '@/lib/supabase/client'
import { Auth } from '@supabase/auth-ui-react'
import { ThemeSupa } from '@supabase/auth-ui-shared'
import { useRouter, useSearchParams } from 'next/navigation'
import { Suspense, useEffect, useState } from 'react'
import { useTranslations, useLocale } from 'next-intl'

function LoginContent() {
    const router = useRouter()
    const searchParams = useSearchParams()
    const mode = searchParams.get('mode')
    const [isMounted, setIsMounted] = useState(false)
    const t = useTranslations('LoginPage')
    const locale = useLocale()

    useEffect(() => {
        const supabase = createClient()

        // Check if user is already logged in
        const checkUser = async () => {
            const { data: { user } } = await supabase.auth.getUser()
            if (user) {
                if (mode === 'app') {
                    // Already signed in — server route will issue diktame:// deeplink
                    window.location.href = '/api/auth/app-token'
                } else {
                    router.push(`/${locale}/dashboard`)
                }
            }
        }
        checkUser()

        // Listen for auth changes
        const { data: { subscription } } = supabase.auth.onAuthStateChange((event, session) => {
            if (event === 'SIGNED_IN' && session && mode !== 'app') {
                router.push(`/${locale}/dashboard`)
            }
        })

        setIsMounted(true)

        return () => subscription.unsubscribe()
    }, [router, mode])

    if (!isMounted) {
        return null
    }

    const supabase = createClient()

    const baseCallback = typeof window !== 'undefined' ? `${window.location.origin}/auth/callback` : 'https://www.dikta.me/auth/callback'
    const redirectUrl = mode ? `${baseCallback}?mode=${mode}` : baseCallback

    return (
        <div className="min-h-screen bg-gradient-to-b from-gray-900 to-black flex items-center justify-center p-4">
            <div className="w-full max-w-md">
                <div className="text-center mb-8">
                    <h1 className="text-4xl font-bold text-white mb-2">{t('title')}</h1>
                    <p className="text-gray-400">{t('subtitle')}</p>
                </div>

                <div className="mb-6 text-center text-sm text-gray-400">
                    <p className="font-medium text-gray-300 mb-2">{t('benefitsIntro')}</p>
                    <ul className="space-y-1">
                        <li>&#10003; {t('benefit1')}</li>
                        <li>&#10003; {t('benefit2')}</li>
                        <li>&#10003; {t('benefit3')}</li>
                    </ul>
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

            </div>
        </div>
    )
}

export default function LoginPage() {
    return (
        <Suspense>
            <LoginContent />
        </Suspense>
    )
}
