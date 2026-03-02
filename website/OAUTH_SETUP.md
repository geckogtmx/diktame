# OAuth Setup Guide (SPEC_042)

## Quick Setup: Configure OAuth Providers in Supabase

### Step 1: Google OAuth

1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create a new project (or select existing): "diktate-oauth"
3. Enable **Google+ API**
4. Navigate to **Credentials** → **Create Credentials** → **OAuth 2.0 Client ID**
5. Configure OAuth consent screen:
   - App name: `dIKtate`
   - User support email: your email
   - Developer contact: your email
6. Create OAuth Client ID:
   - Application type: **Web application**
   - Name: `dIKtate Web`
   - Authorized redirect URIs:
     ```
     https://YOUR-PROJECT-REF.supabase.co/auth/v1/callback
     ```
     (Replace `YOUR-PROJECT-REF` with your actual Supabase project reference)
7. Copy **Client ID** and **Client Secret**

### Step 2: GitHub OAuth

1. Go to [GitHub Settings → Developer Settings](https://github.com/settings/developers)
2. Click **OAuth Apps** → **New OAuth App**
3. Fill in:
   - Application name: `dIKtate`
   - Homepage URL: `https://dikta.me` (or `http://localhost:3000` for testing)
   - Authorization callback URL:
     ```
     https://YOUR-PROJECT-REF.supabase.co/auth/v1/callback
     ```
4. Click **Register application**
5. Copy **Client ID**
6. Generate **Client Secret** and copy it

### Step 3: Add to Supabase

1. Go to your Supabase Dashboard → **Authentication** → **Providers**
2. **Enable Google:**
   - Paste Client ID
   - Paste Client Secret
   - Click **Save**
3. **Enable GitHub:**
   - Paste Client ID
   - Paste Client Secret
   - Click **Save**
4. **Email (Magic Link)** should already be enabled by default

### Step 4: Find Your Supabase Callback URL

Your callback URL is in the format:
```
https://YOUR-PROJECT-REF.supabase.co/auth/v1/callback
```

To find YOUR-PROJECT-REF:
1. Go to Supabase Dashboard → **Settings** → **API**
2. Look at your **Project URL**: `https://abcdefghijk.supabase.co`
3. The part before `.supabase.co` is your project reference
4. Your callback URL is: `https://abcdefghijk.supabase.co/auth/v1/callback`

### Step 5: Test the Login Flow

1. Make sure your dev server is running: `npm run dev`
2. Visit: http://localhost:3000/login
3. Try signing in with:
   - Google
   - GitHub
   - Email (magic link)
4. You should be redirected to `/dashboard` after successful login

## Troubleshooting

**"Invalid redirect URI" error:**
- Make sure you added the exact callback URL to Google/GitHub
- Check for typos in the Supabase project reference

**"OAuth provider not configured" error:**
- Verify you enabled the provider in Supabase
- Check that Client ID and Secret are correct

**Can't see login buttons:**
- Check browser console for errors
- Verify `.env.local` has correct Supabase keys
- Restart dev server after adding env variables

---

**Related:** SPEC_042_DIKTA_ME_WEBSITE.md
