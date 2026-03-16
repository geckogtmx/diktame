import { serve } from "https://deno.land/std@0.168.0/http/server.ts";

const RESEND_API_KEY = Deno.env.get("RESEND_API_KEY");

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
};

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: CORS_HEADERS });
  }

  try {
    const payload = await req.json();
    const { record } = payload; // Supabase webhook payload structure

    if (!record || !record.email) {
      return new Response(JSON.stringify({ error: "No record found" }), {
        status: 400,
        headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
      });
    }

    const { name, email } = record;

    if (!RESEND_API_KEY) {
      console.error("Missing RESEND_API_KEY");
      return new Response(JSON.stringify({ error: "Internal server error" }), {
        status: 500,
        headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
      });
    }

    const res = await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${RESEND_API_KEY}`,
      },
      body: JSON.stringify({
        from: "dIKta.me <welcome@dikta.me>",
        replyTo: "geckogt@gmail.com",
        to: [email],
        subject: "Welcome to the dIKta.me Waitlist!",
        html: `
          <!DOCTYPE html>
          <html>
          <head>
            <style>
              body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #020617; color: #ffffff; margin: 0; padding: 40px; }
              .container { max-width: 600px; margin: 0 auto; background-color: #0f172a; border: 1px solid #1e293b; border-radius: 16px; padding: 40px; }
              .logo { font-size: 24px; font-weight: bold; color: #ffffff; margin-bottom: 32px; display: flex; align-items: center; gap: 8px; }
              .dot { color: #2563eb; }
              h1 { font-size: 28px; font-weight: 800; margin-bottom: 16px; letter-spacing: -0.02em; }
              p { font-size: 16px; line-height: 1.6; color: #94a3b8; margin-bottom: 24px; }
              .highlight { color: #2563eb; font-weight: 600; }
              .footer { margin-top: 40px; padding-top: 24px; border-top: 1px solid #1e293b; font-size: 12px; color: #64748b; }
              .button { display: inline-block; background-color: #2563eb; color: #ffffff; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: bold; margin-top: 16px; }
            </style>
          </head>
          <body>
            <div class="container">
              <div class="logo">dIKta<span style="color: #2563eb;">.</span>me</div>
              <h1>You're on the list, ${name}!</h1>
              <p>
                Thank you for joining the <span class="highlight">dIKta.me V2</span> waiting list. 
                We're incredibly excited to show you what we've been building.
              </p>
              <p>
                You’re now first in line for the most powerful, privacy-first voice engine for Windows. 
                Look out for an invite to our early access program soon.
              </p>
              <p>
                In the meantime, feel free to share the news on Twitter and help us spread the word!
              </p>
              <a href="https://twitter.com/intent/tweet?text=Just%20joined%20the%20waitlist%20for%20dikta.me%20-%20the%20future%20of%20local-first%20voice%20dictation!%20%F0%9F%9A%80" class="button">Share on Twitter</a>
              <div class="footer">
                @ 2026 dIKta.me. All rights reserved.<br>
                Privacy-first, local-first, human-first.
              </div>
            </div>
          </body>
          </html>
        `,
      }),
    });

    const data = await res.json();

    return new Response(JSON.stringify(data), {
      status: res.status,
      headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
    });
  } catch (error) {
    return new Response(JSON.stringify({ error: error.message }), {
      status: 500,
      headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
    });
  }
});
