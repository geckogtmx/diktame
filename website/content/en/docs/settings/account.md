# Account Settings

The **Account** tab is your central hub for managing your primary dIKta.me configuration, credit system, and subscription.

If you don't want to use your own Developer accounts to connect to Anthropic, OpenAI, or Google in the `API Keys` tab (Bring Your Own Key), you can utilize the native **dIKta.me Cloud**.

## Authentication Options

Depending on how you intend to use the app, dIKta.me allows you to declare how you authenticate:

1.  **Wallet Mode**: Authenticates via the dIKta.me cloud infrastructure. Every dictation, translation, and chat query effortlessly deducts micro-cents from your pre-loaded Wallet balance based on exactly how many tokens you consumed. You do not need to manage API keys, monitor traffic, or worry about rate limits.
2.  **API Key Mode**: Completely bypasses the dIKta.me cloud servers. You must provide all of your own developer keys in the `API Keys` tab. Your requests are processed entirely locally using your personal provider accounts.
3.  **Local Mode**: Bypasses everything. dIKta.me exclusively talks to Ollama and Whisper.net running natively on your computer, meaning no internet or authentication is required at all to dictate, ask, or refine text.

## Profile Management

*   **Log In**: Authenticate your dIKta.me account seamlessly via your web browser to access your Wallet balance across multiple PCs securely.
*   **Avatar Customization**: Personalize your dIKta.me HUD by uploading a custom profile picture. The settings interface includes a built-in circular cropping tool to ensure your avatar fits perfectly on your dashboard and user pane.
*   **Balance Top-Up**: Purchase additional compute credits that are credited to your active session instantly without interrupting your workflow.
*   **Sign Out**: Safely purge your active authorization tokens and revert to an Unauthenticated state. 

*Note: All local Dictation settings, Presets, and customizations are preserved even if you completely log out or switch Authentication Modes.*
