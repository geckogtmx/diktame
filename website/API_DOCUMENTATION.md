# dikta.me Website API Documentation

> **Status:** Phase 5 Complete | Ready for Desktop Integration
> **Last Updated:** 2026-02-09

## Overview

The dikta.me website provides a complete backend API for managing user authentication, trial credits, and AI model proxying. This document covers all available API routes and the Gemini proxy Edge Function.

---

## Authentication

All API routes require authentication via Supabase JWT tokens. Include the token in the Authorization header:

```typescript
Authorization: Bearer YOUR_JWT_TOKEN
```

Get the token from Supabase auth:

```typescript
const { data: { session } } = await supabase.auth.getSession();
const token = session?.access_token;
```

---

## API Routes

### 1. Trial Status

**Endpoint:** `GET /api/trial/status`

**Description:** Fetch the current user's trial credit status

**Authentication:** Required

**Response:**

```json
{
  "wordsUsed": 1250,
  "wordsQuota": 15000,
  "daysRemaining": 12,
  "expiresAt": "2026-02-21T00:00:00.000Z",
  "trialActive": true,
  "hasCustomKey": false
}
```

**Fields:**
- `wordsUsed` - Total words consumed across all API usage
- `wordsQuota` - Trial quota (default: 15,000 words)
- `daysRemaining` - Days until trial expiration (0 if expired)
- `expiresAt` - ISO 8601 timestamp of trial expiration
- `trialActive` - Boolean indicating if trial is active (quota remaining AND not expired)
- `hasCustomKey` - Boolean indicating if user has configured a custom Gemini API key

**Status Codes:**
- `200` - Success
- `401` - Unauthorized (no valid JWT token)
- `404` - Profile not found
- `500` - Internal server error

**Example Usage:**

```typescript
const response = await fetch('/api/trial/status', {
  headers: {
    'Authorization': `Bearer ${token}`,
  },
});

const status = await response.json();
console.log(`${status.wordsUsed} / ${status.wordsQuota} words used`);
```

---

### 2. Record API Usage

**Endpoint:** `POST /api/trial/usage`

**Description:** Record API usage and enforce trial quota

**Authentication:** Required

**Request Body:**

```json
{
  "provider": "gemini",
  "model": "gemini-2.0-flash-001",
  "wordsUsed": 125,
  "tokensUsed": 150,
  "cost": 0
}
```

**Fields:**
- `provider` (required) - One of: `gemini`, `anthropic`, `openai`, `deepseek`
- `model` (required) - Model identifier (e.g., `gemini-2.0-flash-001`)
- `wordsUsed` (required) - Number of words in the response (positive integer)
- `tokensUsed` (optional) - Token count from API response
- `cost` (optional) - Cost in USD (default: 0 for trial)

**Response (Success):**

```json
{
  "success": true,
  "wordsUsed": 1375,
  "wordsQuota": 15000,
  "quotaRemaining": 13625
}
```

**Response (Quota Exceeded):**

```json
{
  "error": "Trial quota exceeded",
  "wordsUsed": 15100,
  "wordsQuota": 15000,
  "quotaRemaining": 0
}
```

**Response (Custom Key):**

```json
{
  "success": true,
  "quotaRemaining": null,
  "message": "Using custom API key"
}
```

**Status Codes:**
- `200` - Success (usage recorded)
- `400` - Invalid request body
- `401` - Unauthorized
- `403` - Trial quota exceeded
- `404` - Profile not found
- `500` - Internal server error

**Example Usage:**

```typescript
const response = await fetch('/api/trial/usage', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    provider: 'gemini',
    model: 'gemini-2.0-flash-001',
    wordsUsed: 125,
    tokensUsed: 150,
  }),
});

if (response.status === 403) {
  console.error('Trial quota exceeded!');
}
```

---

### 3. Profile Management

**Endpoint:** `GET /api/profile`

**Description:** Fetch the authenticated user's profile

**Authentication:** Required

**Response:**

```json
{
  "id": "user-uuid",
  "email": "user@example.com",
  "name": "John Doe",
  "trialWordsQuota": 15000,
  "trialExpiresAt": "2026-02-21T00:00:00.000Z",
  "hasCustomGeminiKey": false,
  "createdAt": "2026-02-06T10:30:00.000Z",
  "updatedAt": "2026-02-09T15:45:00.000Z"
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized
- `404` - Profile not found
- `500` - Internal server error

---

**Endpoint:** `PATCH /api/profile`

**Description:** Update the user's profile

**Authentication:** Required

**Request Body:**

```json
{
  "name": "Jane Smith",
  "customGeminiKey": "AIza...your_key_here"
}
```

**Fields:**
- `name` (optional) - User's display name (set to `null` to clear)
- `customGeminiKey` (optional) - Gemini API key (set to empty string `""` to clear)

**Response:**

```json
{
  "id": "user-uuid",
  "email": "user@example.com",
  "name": "Jane Smith",
  "trialWordsQuota": 15000,
  "trialExpiresAt": "2026-02-21T00:00:00.000Z",
  "hasCustomGeminiKey": true,
  "createdAt": "2026-02-06T10:30:00.000Z",
  "updatedAt": "2026-02-09T16:00:00.000Z"
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized
- `500` - Internal server error

**Example Usage:**

```typescript
// Update name
await fetch('/api/profile', {
  method: 'PATCH',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({ name: 'Jane Smith' }),
});

// Add custom API key
await fetch('/api/profile', {
  method: 'PATCH',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({ customGeminiKey: 'AIza...your_key_here' }),
});

// Remove custom API key (revert to trial credits)
await fetch('/api/profile', {
  method: 'PATCH',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({ customGeminiKey: '' }),
});
```

---

**Endpoint:** `DELETE /api/profile`

**Description:** Delete the user's account and all associated data

**Authentication:** Required

**Response:**

```json
{
  "success": true,
  "message": "Account deleted successfully"
}
```

**Status Codes:**
- `200` - Success (account deleted, user signed out)
- `401` - Unauthorized
- `500` - Internal server error

**Note:** This action is irreversible. All data in `profiles`, `api_usage`, and `budget_tracker` tables will be permanently deleted.

---

## Edge Functions

### Gemini Proxy

**Endpoint:** `POST https://YOUR_PROJECT_REF.supabase.co/functions/v1/gemini-proxy`

**Description:** Proxy requests to Google Gemini API with automatic quota tracking

**Authentication:** Required (JWT in Authorization header)

**Request Body:**

```json
{
  "model": "gemini-2.0-flash-001",
  "contents": [
    {
      "role": "user",
      "parts": [
        {
          "text": "Your prompt here"
        }
      ]
    }
  ],
  "generationConfig": {
    "temperature": 0.7,
    "topK": 40,
    "topP": 0.95,
    "maxOutputTokens": 2048
  }
}
```

**Response (Success):**

```json
{
  "candidates": [
    {
      "content": {
        "parts": [
          {
            "text": "AI-generated response text"
          }
        ],
        "role": "model"
      },
      "finishReason": "STOP",
      "index": 0
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 50,
    "candidatesTokenCount": 125,
    "totalTokenCount": 175
  }
}
```

**Response (Quota Exceeded):**

```json
{
  "error": "Trial quota exceeded",
  "message": "Word quota exceeded",
  "wordsUsed": 15100,
  "wordsQuota": 15000
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized (missing or invalid JWT token)
- `403` - Trial quota exceeded
- `404` - Profile not found
- `500` - Internal server error or Gemini API error

**Features:**
- Automatic JWT verification
- Trial quota enforcement (unless custom key is configured)
- Automatic usage recording in `api_usage` table
- Support for custom API keys (bypass trial limits)
- Word count tracking from response text
- Token count tracking from Gemini metadata
- CORS headers for browser requests

**Example Usage:**

```typescript
const response = await fetch('https://YOUR_PROJECT_REF.supabase.co/functions/v1/gemini-proxy', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    model: 'gemini-2.0-flash-001',
    contents: [
      {
        role: 'user',
        parts: [{ text: 'Explain quantum computing in simple terms' }],
      },
    ],
    generationConfig: {
      temperature: 0.7,
      maxOutputTokens: 2048,
    },
  }),
});

if (response.ok) {
  const data = await response.json();
  const text = data.candidates[0].content.parts[0].text;
  console.log('AI Response:', text);
} else {
  const error = await response.json();
  console.error('Error:', error.error);
}
```

---

## Error Handling

All API routes return consistent error responses:

```json
{
  "error": "Error message description"
}
```

Common status codes:
- `400` - Bad Request (invalid parameters)
- `401` - Unauthorized (missing or invalid auth token)
- `403` - Forbidden (quota exceeded, insufficient permissions)
- `404` - Not Found (profile or resource doesn't exist)
- `500` - Internal Server Error (database or service error)

**Best Practices:**
1. Always check response status before parsing JSON
2. Handle quota exceeded errors gracefully (prompt user to add API key)
3. Implement retry logic with exponential backoff for 500 errors
4. Cache trial status to minimize API calls
5. Use optimistic UI updates with error rollback

---

## Rate Limiting

- API routes: Rate-limited by Supabase (default: 100 requests per 10 seconds)
- Edge Functions: Rate-limited by Supabase (default: 100 requests per 10 seconds)
- Gemini API: Rate-limited by Google (varies by key tier)

**Recommendation:** Implement client-side rate limiting and request queuing for production use.

---

## Security

### Authentication
- All routes protected by Supabase JWT verification
- Tokens expire after 1 hour (automatic refresh via Supabase client)
- Row Level Security (RLS) policies enforce data isolation

### API Keys
- Custom Gemini API keys encrypted at rest in database
- Keys never exposed in API responses (use `hasCustomKey` boolean)
- Service role key only accessible to Edge Functions

### Data Access
- Users can only access their own profile and usage data
- RLS policies prevent cross-user data access
- Profile deletion cascades to related tables

---

## Desktop App Integration

### 1. Authentication Flow

```typescript
// Open browser for OAuth login
const authUrl = 'https://dikta.me/login';
shell.openExternal(authUrl);

// Listen for deeplink callback
app.on('open-url', (event, url) => {
  // Parse: diktame://auth?token=xxx
  const token = new URL(url).searchParams.get('token');
  // Store token securely
});
```

### 2. Check Trial Status

```typescript
async function checkTrialStatus() {
  const response = await fetch('https://dikta.me/api/trial/status', {
    headers: { 'Authorization': `Bearer ${token}` },
  });
  const status = await response.json();

  if (!status.trialActive) {
    // Prompt user to add custom API key
    showUpgradePrompt();
  }

  return status;
}
```

### 3. Use Gemini Proxy

```typescript
async function callGeminiAPI(prompt: string) {
  const response = await fetch(
    'https://YOUR_PROJECT_REF.supabase.co/functions/v1/gemini-proxy',
    {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        model: 'gemini-2.0-flash-001',
        contents: [{ role: 'user', parts: [{ text: prompt }] }],
      }),
    }
  );

  if (response.status === 403) {
    // Quota exceeded - prompt to add API key
    showUpgradePrompt();
    return null;
  }

  const data = await response.json();
  return data.candidates[0].content.parts[0].text;
}
```

### 4. Add Custom API Key

```typescript
async function addCustomAPIKey(apiKey: string) {
  const response = await fetch('https://dikta.me/api/profile', {
    method: 'PATCH',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ customGeminiKey: apiKey }),
  });

  if (response.ok) {
    console.log('API key added successfully');
    // No longer subject to trial limits
  }
}
```

---

## Development Setup

### Environment Variables

Create `.env.local` in the `website/` directory:

```env
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your_anon_key
SUPABASE_SERVICE_ROLE_KEY=your_service_role_key
```

### Edge Function Secrets

Set via Supabase CLI:

```bash
supabase secrets set GEMINI_API_KEY=your_gemini_api_key
```

### Local Testing

Run the development server:

```bash
cd website
npm run dev
```

Test Edge Functions locally:

```bash
supabase start
supabase functions serve gemini-proxy
```

---

## Support

For API issues:
1. Check function logs: `supabase functions logs gemini-proxy`
2. Verify authentication token is valid
3. Check Supabase Dashboard for database errors
4. Review RLS policies in Supabase SQL Editor

For documentation updates or bug reports:
- GitHub: `geckogtmx/diktate`
- Email: support@dikta.me (coming soon)

---

**Version:** 1.0.0
**Last Updated:** 2026-02-09
**Status:** Production Ready (pending Phase 6 desktop integration)
