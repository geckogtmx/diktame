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
    const { senderName, recipientEmail } = await req.json();

    if (!recipientEmail || !senderName) {
      return new Response(JSON.stringify({ error: "Missing required fields" }), {
        status: 400,
        headers: { ...CORS_HEADERS, "Content-Type": "application/json" },
      });
    }

    if (!RESEND_API_KEY) {
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
        from: "dIKta.me <invites@dikta.me>",
        replyTo: "geckogt@gmail.com",
        to: [recipientEmail],
        subject: `${senderName} gifted you a Priority Pass for dIKta.me`,
        html: `
          <!DOCTYPE html>
          <html>
          <head>
            <style>
              body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #020617; color: #ffffff; margin: 0; padding: 40px; }
              .container { max-width: 600px; margin: 0 auto; background-color: #0f172a; border: 1px solid #1e293b; border-radius: 16px; padding: 40px; }
              .logo { font-size: 24px; font-weight: bold; color: #ffffff; margin-bottom: 32px; display: flex; align-items: center; gap: 8px; }
              h1 { font-size: 24px; font-weight: 800; margin-bottom: 16px; letter-spacing: -0.02em; }
              p { font-size: 16px; line-height: 1.6; color: #94a3b8; margin-bottom: 24px; }
              .highlight { color: #2563eb; font-weight: 600; }
              .footer { margin-top: 40px; padding-top: 24px; border-top: 1px solid #1e293b; font-size: 12px; color: #64748b; }
              .button { display: inline-block; background-color: #2563eb; color: #ffffff; padding: 12px 24px; border-radius: 8px; text-decoration: none; font-weight: bold; margin-top: 16px; }
            </style>
          </head>
          <body>
            <div class="container">
              <div class="logo">dIKta<span style="color: #2563eb;">.</span>me</div>
              <h1>A gift for you.</h1>
              <p>
                Your friend <span class="highlight">${senderName}</span> just gifted you a <span class="highlight">Priority Pass</span> for the dIKta.me V2 waiting list.
              </p>
              <p>
                dIKta.me is the future of local-first voice dictation. By using this pass, you'll get higher priority in our early access program.
              </p>
              <p>
                Ready to claim your spot?
              </p>
              <a href="https://dikta.me/waitlist" class="button">Claim Priority Pass</a>
              <div class="footer">
                You received this because ${senderName} invited you to the dIKta.me waitlist.<br>
                @ 2026 dIKta.me. All rights reserved.
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
