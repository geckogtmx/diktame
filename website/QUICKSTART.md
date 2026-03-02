# How to Get Supabase Service Role Key

## Step-by-Step Guide

### 1. Go to Supabase Dashboard
Navigate to: https://supabase.com/dashboard/project/YOUR-PROJECT-ID

### 2. Navigate to API Settings
- Click on **Settings** (gear icon in left sidebar)
- Click on **API** in the settings menu

### 3. Find the Keys Section
You'll see a section called **Project API keys** with several keys:

#### Keys You'll See (New Naming - 2026):

1. **Project URL** 
   - Example: `https://abcdefghijk.supabase.co`
   - Copy this to: `NEXT_PUBLIC_SUPABASE_URL`

2. **Publishable Key** (replaces "anon key")
   - Labeled as: `sb_publishable_xxx` or just "Publishable"
   - Starts with: `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
   - Copy this to: `NEXT_PUBLIC_SUPABASE_ANON_KEY`
   - ✅ Safe to expose in client-side code (with RLS enabled)

3. **Secret Key** (replaces "service_role key") (⚠️ SECRET - Never expose!)
   - Labeled as: `sb_secret_xxx` or "Secret"
   - Also starts with: `eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
   - **This is the one you need!**
   - Copy this to: `SUPABASE_SERVICE_ROLE_KEY`
   - ⚠️ **NEVER commit this to git or expose in client code**

> **Note:** You may also see legacy "anon" and "service_role" keys. Both work, but Supabase recommends using the new "publishable" and "secret" keys for new projects.

### 4. Revealing the Secret Key

The `secret` key is **hidden by default** for security:
- Look for a key labeled **"Secret"** or **"Secret Key"**
- Click the **eye icon** 👁️ or **"Reveal"** button next to it
- Copy the entire key (it's very long, ~200+ characters)

### 5. Paste into `.env.local`

Your `.env.local` should look like:

```bash
NEXT_PUBLIC_SUPABASE_URL=https://abcdefghijk.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFiY2RlZmdoaWprIiwicm9sZSI6ImFub24iLCJpYXQiOjE2NDMyMDQ4MDAsImV4cCI6MTk1ODc4MDgwMH0.XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
SUPABASE_SERVICE_ROLE_KEY=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImFiY2RlZmdoaWprIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTY0MzIwNDgwMCwiZXhwIjoxOTU4NzgwODAwfQ.YYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYYY
```

## ⚠️ Security Warning

**The service_role key has FULL DATABASE ACCESS and bypasses Row Level Security!**

- ✅ DO: Keep it in `.env.local` (already in `.gitignore`)
- ✅ DO: Only use it in server-side code (API routes, Server Components)
- ❌ DON'T: Commit it to git
- ❌ DON'T: Expose it in client-side code
- ❌ DON'T: Share it publicly

## Troubleshooting

**Can't find the service_role key?**
- Make sure you're the **owner** of the Supabase project
- Check you're in the right project
- Look for a "Reveal" or eye icon 👁️ button

**Still stuck?**
- Screenshot the API settings page (hide the keys!)
- The service_role key should be listed below the anon key

---

**Related:** SPEC_042_DIKTA_ME_WEBSITE.md
