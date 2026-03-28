# dIKta.me V2.0 — Launch Content Package

> **Status:** Draft · March 2026
> All copy follows BRAND_BOOK.md voice: precise, direct, no marketing fluff.
> English + Spanish throughout. Eduardo to review and correct Spanish.

---

## Table of Contents

1. [The Input Layer Is Broken — Manifesto](#1-the-input-layer-is-broken--manifesto)
2. [Product Hunt Launch Page](#2-product-hunt-launch-page)
3. [Show HN Post](#3-show-hn-post)
4. [Reddit Launch Posts](#4-reddit-launch-posts)
5. [Launch Week Social Batch (7 Days)](#5-launch-week-social-batch-7-days)
6. [SEO Blog Outlines (4)](#6-seo-blog-outlines-4)

---

## 1. The Input Layer Is Broken — Manifesto

> **Format:** Blog post / LinkedIn article · Target: 900-1,000 words
> **Publish:** Day 0 (Launch Day). LinkedIn long-form + dikta.me/blog.
> Spanish version below — Eduardo to review.

---

### The input layer is broken

The AI models got better. Significantly better.

Three years ago, GPT-3 hallucinated constantly and could barely follow multi-step instructions. Today, Gemini 2.5 Pro reasons across 1 million tokens. Claude 3.7 writes production code. The best open-source models run on consumer hardware and approach proprietary performance on most real-world tasks.

The models aren't the bottleneck anymore.

**The input layer is.**

You are still typing your thoughts into a chat box. You are still copy-pasting text between your editor and the AI window. You are still screenshotting things manually, uploading them, waiting, copying the result, switching back. You are still paying for five separate subscriptions — dictation, grammar, meeting AI, chat, vision — each covering a slice of the same workflow.

The output quality of AI improved 3x. The context window grew 250x. But the way you talk to your AI is exactly what it was in 2022: a text box in a browser tab.

Nobody fixed the input.

---

**Here is what a broken input layer costs you every day.**

You type at roughly 60 words per minute. You speak at roughly 150. That gap — 2.5x — is time you spend transcribing thoughts instead of having them. And that's before you account for the backspacing, the corrections, the re-reads.

But it's not just speed. It's context switching. Every time you move from your work tool to a chat window to ask the AI something, and then back, you pay a context tax. Research puts this at 15-20 minutes to fully recover focus after an interruption. You're interrupting yourself dozens of times per day just to use the tools that are supposed to make you faster.

And the economics are absurd. A knowledge worker in 2026 might pay:

- $12/month for AI-powered dictation
- $12/month for grammar checking
- $14/month for meeting AI
- $20/month for a chat assistant
- $15/month for legacy dictation software

That's $73/month — $876/year — to rent software that still requires you to manually bridge the gaps between them.

---

**The fix isn't a better chat box.**

I spent 20 years in consulting, digital media, and project management. I'm not a software engineer by training. I'm a user who got frustrated enough to build something.

What I wanted was simple: press a key in any app, speak naturally, and have the result appear at my cursor. Not in a sidebar. Not in a separate window. At the cursor, in whatever I was already working in.

And not just transcription. I wanted the AI to do the work — clean up the grammar, translate on the fly, answer questions in context, take notes to a file, read text back to me. All of it. Without switching apps.

That's dIKta.me.

---

**What it actually does.**

Eight workflow modes, activated by hotkeys, that work inside any Windows application:

**Dictate** — speak, get clean text at your cursor. Not raw transcription: punctuated, formatted, ready.

**Refine** — select any text you've written, hold the hotkey, describe the change in your voice. "Make this more direct." "Cut it in half." "Translate to Spanish." The text updates in place.

**Ask** — speak a question, get the answer injected at your cursor. Works in your IDE, your doc editor, your email client. Whatever you have open.

**Translate** — speak in English, get Spanish. Speak in Spanish, get English. LLM-backed, context-aware, one hotkey.

**Note** — voice post-it notes that go to a markdown file. Works offline.

**Read Selection** — highlight text, hear it read aloud. Locally, using Kokoro TTS. No cloud required.

Plus vision: point at anything on your screen, ask a question, get an answer. Screenshot, crop, describe, extract text, build a table — all with voice commands.

---

**The part that matters most: it runs locally.**

Whisper V3 for speech-to-text. Ollama for the language model. Kokoro for text-to-speech. Three local AI models, all running on your GPU, none of them requiring internet access after the first download.

Your voice never leaves your machine unless you choose cloud. Your documents don't get uploaded. Your API keys are encrypted with Windows DPAPI and never transmitted. The privacy controls go to four levels of granularity.

This is not a privacy checkbox. It is the architecture.

If you want cloud speed, you can use Deepgram, Gemini, OpenAI, or Anthropic — with your own API keys, at cost, no markup. But you don't have to. Full local mode works. It's fast. It's free to run.

---

**Why it costs $20 once instead of $20/month.**

Because I don't believe you should pay rent on software that runs on your hardware.

The code is MIT-licensed and on GitHub. You can read it, fork it, build it yourself. The $20 Power License is for people who want the installer and don't want to set up a .NET 8 build environment. That's a fair exchange.

There's also a free tier — the Wallet — that lets you try the full app with cloud credits before you decide anything. No credit card to start.

If you want to support the project beyond that, there's a Ko-fi for $2/month. It's a donation, not a service contract. No SLA. No obligation.

The math against alternatives: five subscriptions at $73/month is $876/year. dIKta.me is $20 once, and the local AI stack runs at $0/month after that.

---

**What's coming.**

V2.0 is the core: voice, vision, text selection, local AI, all workflow modes.

V2.1 adds connectors — route dictation output to Obsidian, webhooks, Discord, Notion. Adds a grammar checking pipeline that works in every app (not just the ones with native integrations). Adds meeting intelligence that merges your voice notes with transcripts to synthesize action items.

After that: a memory layer. The product gets smarter the more you use it, because everything you say and select starts building context.

The thesis is simple. AI is not a destination you travel to. It should be the environment you work inside. The input layer should feel invisible. Natural. Fast.

dIKta.me is the first piece of that.

---

*dIKta.me V2.0 is available now at [dikta.me](https://dikta.me). Free to try. $20 to own.*

*Built by Eduardo Garcia-Torres. One developer. Three months. 1,014 tests. MIT license.*

---

### La capa de entrada está rota 🇪🇸

Los modelos de IA mejoraron. Significativamente.

Hace tres años, GPT-3 alucinaba constantemente y apenas podía seguir instrucciones de varios pasos. Hoy, Gemini 2.5 Pro razona sobre un millón de tokens. Claude 3.7 escribe código de producción. Los mejores modelos de código abierto corren en hardware de consumo y se acercan al rendimiento propietario en la mayoría de las tareas reales.

Los modelos ya no son el cuello de botella.

**La capa de entrada lo es.**

Sigues escribiendo tus pensamientos en un cuadro de chat. Sigues copiando y pegando texto entre tu editor y la ventana de IA. Sigues haciendo capturas de pantalla manualmente, subiéndolas, esperando, copiando el resultado, regresando. Sigues pagando cinco suscripciones separadas — dictado, gramática, IA para reuniones, chat, visión — cada una cubriendo una parte del mismo flujo de trabajo.

La calidad de salida de la IA mejoró 3x. La ventana de contexto creció 250x. Pero la forma en que le hablas a tu IA es exactamente la misma que en 2022: un cuadro de texto en una pestaña del navegador.

Nadie arregló la entrada.

---

**Esto es lo que una capa de entrada rota te cuesta cada día.**

Escribes a unas 60 palabras por minuto. Hablas a unas 150. Esa brecha — 2.5x — es tiempo que pasas transcribiendo pensamientos en lugar de teniéndolos. Y eso es antes de contar los retrocesos, las correcciones, las releeruras.

Pero no es solo velocidad. Es el cambio de contexto. Cada vez que pasas de tu herramienta de trabajo a una ventana de chat para preguntarle algo a la IA, y luego regresas, pagas un impuesto de contexto. La investigación estima que se tardan 15-20 minutos en recuperar el enfoque tras una interrupción. Te estás interrumpiendo docenas de veces al día solo para usar las herramientas que se supone que deben hacerte más rápido.

Y la economía es absurda. Un trabajador del conocimiento en 2026 podría pagar:

- $12/mes por dictado con IA
- $12/mes por corrección gramatical
- $14/mes por IA de reuniones
- $20/mes por un asistente de chat
- $15/mes por software de dictado heredado

Son $73/mes — $876/año — por rentar software que todavía requiere que puentes manualmente las brechas entre todos ellos.

---

**La solución no es un mejor cuadro de chat.**

Pasé 20 años en consultoría, medios digitales y gestión de proyectos. No soy ingeniero de software de formación. Soy un usuario que se frustró lo suficiente como para construir algo.

Lo que quería era simple: presionar una tecla en cualquier app, hablar naturalmente, y que el resultado apareciera en mi cursor. No en una barra lateral. No en una ventana separada. En el cursor, en lo que ya estaba trabajando.

Y no solo transcripción. Quería que la IA hiciera el trabajo — limpiar la gramática, traducir al vuelo, responder preguntas en contexto, tomar notas en un archivo, leerme texto en voz alta. Todo. Sin cambiar de app.

Eso es dIKta.me.

---

**Lo que realmente hace.**

Ocho modos de flujo de trabajo, activados por atajos de teclado, que funcionan dentro de cualquier aplicación de Windows:

**Dictar** — habla, obtén texto limpio en tu cursor. No transcripción cruda: puntuada, formateada, lista.

**Refinar** — selecciona cualquier texto que hayas escrito, mantén el atajo, describe el cambio con tu voz. "Hazlo más directo." "Córtalo a la mitad." "Traduce al español." El texto se actualiza en su lugar.

**Preguntar** — habla una pregunta, obtén la respuesta inyectada en tu cursor. Funciona en tu IDE, tu editor de documentos, tu cliente de correo. Lo que tengas abierto.

**Traducir** — habla en inglés, obtén español. Habla en español, obtén inglés. Respaldado por LLM, consciente del contexto, un solo atajo.

**Nota** — notas de voz que van a un archivo markdown. Funciona sin conexión.

**Leer Selección** — resalta texto, escúchalo leer en voz alta. Localmente, usando Kokoro TTS. Sin nube.

Más visión: apunta a cualquier cosa en tu pantalla, haz una pregunta, obtén una respuesta. Captura, recorta, describe, extrae texto, construye una tabla — todo con comandos de voz.

---

**La parte más importante: corre localmente.**

Whisper V3 para voz a texto. Ollama para el modelo de lenguaje. Kokoro para texto a voz. Tres modelos de IA locales, todos corriendo en tu GPU, ninguno requiriendo acceso a internet después de la primera descarga.

Tu voz nunca sale de tu máquina a menos que elijas la nube. Tus documentos no se suben. Tus claves API están cifradas con Windows DPAPI y nunca se transmiten. Los controles de privacidad llegan a cuatro niveles de granularidad.

Esto no es una casilla de privacidad. Es la arquitectura.

Si quieres velocidad en la nube, puedes usar Deepgram, Gemini, OpenAI o Anthropic — con tus propias claves API, a costo real, sin margen. Pero no tienes que hacerlo. El modo local funciona. Es rápido. Es gratuito para correr.

---

**Por qué cuesta $20 una vez en lugar de $20/mes.**

Porque no creo que debas pagar renta por software que corre en tu propio hardware.

El código tiene licencia MIT y está en GitHub. Puedes leerlo, hacer un fork, compilarlo tú mismo. La Licencia Power de $20 es para personas que quieren el instalador y no quieren configurar un entorno de compilación de .NET 8. Eso es un intercambio justo.

También hay un nivel gratuito — la Billetera — que te permite probar la app completa con créditos en la nube antes de decidir nada. Sin tarjeta de crédito para empezar.

Si quieres apoyar el proyecto más allá de eso, hay un Ko-fi por $2/mes. Es una donación, no un contrato de servicio. Sin SLA. Sin obligaciones.

La matemática frente a las alternativas: cinco suscripciones a $73/mes son $876/año. dIKta.me es $20 una vez, y el stack de IA local corre a $0/mes después de eso.

---

**Lo que viene.**

V2.0 es el núcleo: voz, visión, selección de texto, IA local, todos los modos de flujo de trabajo.

V2.1 añade conectores — enruta la salida del dictado a Obsidian, webhooks, Discord, Notion. Añade un pipeline de corrección gramatical que funciona en todas las apps. Añade inteligencia de reuniones que fusiona tus notas de voz con transcripciones para sintetizar elementos de acción.

Después de eso: una capa de memoria. El producto se vuelve más inteligente cuanto más lo usas, porque todo lo que dices y seleccionas empieza a construir contexto.

La tesis es simple. La IA no es un destino al que viajas. Debe ser el entorno dentro del que trabajas. La capa de entrada debería sentirse invisible. Natural. Rápida.

dIKta.me es la primera pieza de eso.

---

*dIKta.me V2.0 ya está disponible en [dikta.me](https://dikta.me). Gratis para probar. $20 para ser tuyo.*

*Construido por Eduardo Garcia-Torres. Un desarrollador. Tres meses. 1,014 pruebas. Licencia MIT.*

---

---

## 2. Product Hunt Launch Page

> **[ES VERSION NEEDED]** for tagline and first two description paragraphs on any bilingual-capable listing.

---

### Title

```
dIKta.me — Local-first AI dictation for Windows
```

### Tagline

```
Stop typing at your AI models. Just talk to them.
```

*(Alternate: "Voice + Vision + LLM. Any app. Any model. $20 once.")*

---

### Short Description (appears in listing card, ~160 chars)

```
8 voice AI modes that work in any Windows app. Local Whisper + Ollama + Kokoro or cloud. Dictate, refine, translate, ask, screenshot AI. One-time $20.
```

---

### Full Description (~500 words)

**The problem:** You type at 60 wpm. You think at 150. The gap is glue work — copy-pasting between apps, context-switching to chat windows, paying for five separate AI subscriptions that don't talk to each other.

**What dIKta.me does:** Press a hotkey in any Windows application — Word, VS Code, Slack, your browser, anything — and your voice becomes processed text at your cursor. Not raw transcription. AI-refined, context-aware output, exactly where you need it.

**Eight workflow modes:**

- **Dictate** (`Ctrl+Alt+D`) — speak naturally, get clean text injected at cursor
- **Refine Voice** — select any text, speak a description of the change, watch it update
- **Ask** — voice question, AI answer appears where your cursor is
- **Translate** — speak English, get Spanish (or reverse). One hotkey, any app
- **Note** — voice notes that route to a markdown file
- **Read Selection** — highlight text, hear it read aloud via local TTS
- **Vision** — point at anything on screen, ask what it is, extract text, build tables

**The local-first stack:**

- STT: Whisper V3 Turbo (local GPU, Vulkan) or Deepgram/Gemini (cloud)
- LLM: Ollama (any model) or OpenAI/Anthropic/Gemini (BYOK, no markup)
- TTS: Kokoro ONNX (~88MB, fully local) or Deepgram/OpenAI (cloud)

No data leaves your machine unless you choose cloud. API keys are DPAPI-encrypted. Four levels of privacy control.

**Who it's for:**

Knowledge workers who live in productivity tools. Privacy-conscious professionals who won't put voice recordings on third-party servers. Anyone paying $70+/month across dictation + grammar + meeting + chat AI subscriptions.

**Pricing:**

- Free Wallet tier — full app with $1 promo cloud credit, no credit card needed
- Power License — $20 once. Local mode, BYOK, offline. Yours forever.
- Ko-fi Supporter — $2/month, direct line to the builder, early access

**What's next:** App connectors (Obsidian, Discord, webhooks), grammar checking pipeline (Grammarly-style, works in every app), meeting intelligence, semantic memory layer.

**Built by:** Eduardo Garcia-Torres — a marketing and business executive from Mexico with 20+ years in IT consulting and digital media. Not a software engineer by training. This is his first desktop application, built with C#, WinUI 3, and AI coding tools. One developer. Three months. 1,014 tests.

**Open source:** MIT license. GitHub public at launch. Build from source if you prefer — .NET 8 SDK required.

---

### About / Maker Comment (posted Day 0 on PH)

Hi — I'm Eduardo. I built dIKta.me because I was spending too much time managing tools that were supposed to save me time.

I've been in consulting and project management for 20 years. I'm bilingual (English/Spanish), work across multiple apps simultaneously, and generate a lot of written output. I started dictating years ago, then started layering in AI. At some point I had five subscriptions and was still doing manual copy-paste between all of them.

So I rewrote my Python prototype from scratch in C#/WinUI 3. It took three months and 1,014 tests to get here.

dIKta.me is free to try — sign in, get $1 in cloud credits, run the full app, see if it fits your workflow. If you want local AI (4x faster, offline, private), the Power License is $20 once.

I read every piece of feedback. Ask me anything.

---

---

## 3. Show HN Post

> **English only.** HN doesn't do localization.

---

### Title

```
Show HN: dIKta.me – local-first AI dictation for Windows (voice + vision + LLM pipeline)
```

---

### Body

I've been building this for the past ~3 months. It's a native Windows app (C#, WinUI 3) that bridges your voice, screen, and selected text to whatever AI you're running — locally or cloud.

**What it does:**

Eight hotkey-activated workflow modes that work inside any Windows app at the OS level:

- Dictate: voice → clean text injected at cursor
- Refine: select text + speak instructions → text updated in-place
- Ask: voice question → AI answer at cursor
- Translate: speak EN, get ES (or reverse). LLM-backed.
- Vision: screenshot any region → describe, OCR, extract table, ask questions
- Quick Chat: floating overlay for multi-turn voice conversations
- Note: voice → markdown file
- Read Selection: highlight text → local TTS reads it back

**The local stack:**

- STT: Whisper.net (C# wrapper for whisper.cpp), Vulkan GPU acceleration, runs fully offline
- LLM: Ollama (any model via HTTP), cached provider instances to avoid reconnect overhead
- TTS: KokoroSharp (ONNX, ~88MB, CPU or GPU)

You can also use cloud providers (Deepgram, Gemini, OpenAI, Anthropic) with BYOK — no markup, no proxy for the LLM calls. The wallet system (for cloud STT/LLM without managing API keys) uses Supabase + Cloudflare Workers edge functions.

**Architecture notes:**

Provider-agnostic pipeline: `ISpeechTranscriber`, `ILLMProvider`, `ITextToSpeechService` interfaces. Swap any layer without touching the rest. PipelineFactory resolves providers per-dictation based on settings + auth mode.

LLMProviderFactory uses a `ConcurrentDictionary` cache keyed by `"{type}:{model}"` — learned the hard way that creating a new `HttpClient` per dictation costs ~2500ms in Ollama connection overhead.

Text injection is OS-level: `SendInput` for most apps, clipboard paste as fallback (with original clipboard restore via delay + Win32 clipboard chain).

1,014 unit tests (xUnit + Moq + FluentAssertions). CI on GitHub Actions.

**Honest about what it isn't:**

- Windows only (Mac would need a different input injection approach)
- No command mode voice editing yet (like Wispr Flow has)
- No auto-update yet (V2.1)
- Installer requires SmartScreen workaround until I can afford an EV cert (~$300/year)

**The non-engineer angle:**

I'm a marketing/business exec from Mexico who decided to rewrite a Python/Electron prototype in C# because it was embarrassingly slow. I'm not a software engineer by training. I've been writing code for about a year, with AI assistance (primarily Claude). The architecture decisions and product direction come from 20 years of building businesses and understanding what knowledge workers actually need.

I think that context is worth being transparent about.

**Links:**

- App: dikta.me
- Free trial: sign in, get $1 cloud credits, run the full app
- Power License: $20 one-time, local mode + BYOK + offline

Happy to answer questions about the architecture, the local AI integration, or anything else.

---

---

## 4. Reddit Launch Posts

---

### 4a. r/selfhosted

> **Title:** dIKta.me – local-first AI dictation for Windows: Whisper + Ollama + Kokoro, no cloud required

**Body:**

Built a native Windows app (C#/WinUI 3) that does AI dictation, text refinement, translation, vision/OCR, and TTS — fully locally, no cloud required.

The local stack:

- **STT:** Whisper.net (whisper.cpp C# bindings), Vulkan GPU acceleration, runs offline
- **LLM:** Ollama (any model you have pulled), HTTP keep-alive, provider caching
- **TTS:** KokoroSharp (ONNX runtime, ~88MB download, CPU or GPU)

Privacy controls: 4 levels. Zero telemetry. API keys encrypted with Windows DPAPI. Your voice never touches a server unless you explicitly choose cloud.

Eight hotkey-activated modes: Dictate, Refine (voice instructions on selected text), Ask (Q&A injected at cursor), Translate (EN↔ES), Note (→ markdown file), Read Selection (TTS), Vision (screenshot AI), Quick Chat overlay.

Pricing: Free Wallet tier with cloud credits for trying it, $20 one-time for Power License (local mode, BYOK, full offline). MIT license, source on GitHub at launch.

**Caveat:** Windows only right now. Mac would need different OS-level text injection.

The local setup takes about 10 minutes (download Whisper model + pull an Ollama model + optionally download Kokoro). The first-run wizard walks through it. After that, no internet needed for anything.

Happy to answer questions about the self-hosted stack.

---

### 4b. r/productivity

> **Title:** I replaced 5 AI subscriptions with one app — here's what it does

**Body:**

I was paying for Grammarly ($12), Otter ($10), a dictation app ($15), ChatGPT Plus ($20), and a screenshot AI tool ($10). That's $67/month, and I was still copy-pasting between all of them.

So I built dIKta.me — a Windows app that does all of it from one hotkey.

**The actual workflow:**

- In any app (Word, Slack, Notion, VS Code, email, anything): press `Ctrl+Alt+D`, speak, clean text appears at cursor.
- Select any text you've written + hold the hotkey + say "make this more direct" → text rewrites in-place.
- `Ctrl+Alt+A`: speak a question, AI answer appears where your cursor is. No app switching.
- `Ctrl+Alt+T`: speak English, get Spanish. Or reverse. Works in any app.
- `Ctrl+Alt+S`: point at anything on screen, ask a question, get an answer. OCR, table extraction, vision AI.

No copy-paste. No context switching. No sidebar. Text appears where you're working.

**The pricing:**

- Free to try (Wallet tier, $1 cloud credit, full app)
- $20 one-time for local mode (Whisper + Ollama — 4x faster, offline, private)

dikta.me if you want to check it out. Happy to answer questions about specific workflows.

---

### 4c. r/LocalLLaMA

> **Title:** Built a Windows dictation app that uses Ollama as its LLM brain — full local pipeline with Whisper + Kokoro

**Body:**

Built dIKta.me — a native Windows app (C#/WinUI 3) that uses Ollama as the LLM backend for AI-enhanced dictation.

**The local pipeline:**

1. Whisper.net (whisper.cpp C# bindings) for STT — Vulkan GPU acceleration, base/small/medium/large models
2. Ollama for LLM processing (any model you have: gemma3, llama3, qwen2.5, mistral, etc.)
3. KokoroSharp ONNX for TTS (local readback, ~88MB)

**What Ollama handles in the pipeline:**

- Refine mode: takes selected text + voice instructions, returns edited version
- Ask mode: voice question → structured answer
- Translate mode: EN↔ES context-aware translation
- Vision mode: image description, OCR, table extraction (using minicpm-v or any vision model)
- Quick Chat: multi-turn conversation overlay

**Some implementation notes for those curious:**

The biggest performance issue was LLM provider instantiation. Creating a new `HttpClient` per dictation call adds ~2500ms from TCP reconnect overhead. Fixed with a `ConcurrentDictionary<string, ILLMProvider>` cache keyed by `"{type}:{model}"` — drops Ollama latency from ~3000ms to ~550ms.

Vision model and text LLM use separate cache keys with different `keep_alive` values (5min for vision, 10min for text) since switching between them unloads/reloads weights.

Full local mode runs at $0/month after the model downloads. Tested on RTX 3060 12GB: gemma3:4b runs fine for refinement, minicpm-v handles vision adequately.

Architecture is provider-agnostic — same interface for Ollama as for OpenAI/Anthropic/Gemini if you want cloud fallback.

MIT license, source on GitHub at launch. Power License ($20 one-time) for the installer and pre-built binaries.

---

---

## 5. Launch Week Social Batch (7 Days)

> Template per RELEASE_ROADMAP.md Appendix A. Day 0 = announcement.
> Each day: LinkedIn (long-form) + X/Twitter (punchy, ≤280 chars).
> **[ES VERSION NEEDED]** for all LinkedIn posts and X/Twitter posts — follow the bilingual pattern from SOCIAL_W13_MAR24-30.md.

---

### Day 0 — Announcement (Launch Day)

**LinkedIn 🇺🇸**

> **dIKta.me V2.0 is live.**
>
> Three months. One developer. 1,014 tests. MIT license. Here's what I built.
>
> The problem: AI models improved 3x. Context windows grew 250x. But you're still typing into a chat box and copy-pasting between apps. Nobody fixed the input layer.
>
> dIKta.me fixes it.
>
> 8 hotkey-activated modes that work in any Windows app — Dictate, Refine, Ask, Translate, Vision, Note, Quick Chat, Read Selection. Results appear at your cursor. No app switching. No copy-paste.
>
> Runs fully locally: Whisper V3 for STT, Ollama for LLM, Kokoro for TTS. Or use cloud providers with your own API keys. Your choice.
>
> Free to try. $20 to own. MIT license if you want to build from source.
>
> dikta.me — I read every piece of feedback.
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> dIKta.me V2.0 is live.
>
> 8 voice AI modes. Any Windows app. Local Whisper + Ollama + Kokoro or cloud. Speak → result at cursor.
>
> Free to try. $20 to own.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 1 — "Day 1 numbers + what I learned"

**LinkedIn 🇺🇸**

> 24 hours in. Here's what happened.
>
> [Replace with actual numbers: downloads, installs, PH rank, comments, DMs]
>
> Three things I learned from Day 1 feedback:
>
> [Replace with 3 real observations from feedback]
>
> The question I got most: [most common question]. The answer: [answer].
>
> What's being fixed first: [top bug or UX issue from feedback].
>
> Thanks to everyone who tried it, upvoted, commented, or sent notes. Every one of those matters more than you'd think when you're building alone.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> Day 1 of @dIKtaMe:
> [X downloads] installs
> [X] Product Hunt rank
> Top question: [question]
> Top bug: [bug]
>
> Building in public. Here's what I'm fixing first: [fix]
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 2 — Feature walkthrough: Dictate + Refine

**LinkedIn 🇺🇸**

> **The two modes I use 20x per day.**
>
> **Dictate mode** (`Ctrl+Alt+D`): press, speak, release. Clean text appears at cursor. Not raw transcription — punctuated, formatted, ready to send or publish. Works in any app: email, Slack, VS Code, Google Docs, Word, your IDE, anything.
>
> The difference from basic dictation: it's not dumping words. It's running your speech through a language model that handles punctuation, paragraph breaks, and minor corrections while preserving your meaning.
>
> **Refine Voice** (`Ctrl+Alt+R` hold): select text, hold the hotkey, say "make this more formal" or "cut this in half" or "translate to Spanish" — release. The text rewrites in-place. No new window. No copy-paste. The cursor stays where it was.
>
> I use this to fix emails I drafted too fast, condense meeting notes, and adapt the same paragraph for different audiences without switching apps.
>
> Local mode: Whisper + Ollama. Sub-2 second combined latency on a mid-range GPU.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> Two modes I use constantly:
>
> Dictate: press hotkey, speak, text appears at cursor in any app.
>
> Refine Voice: select text, say "cut it in half" or "make this formal" → text rewrites in-place.
>
> Both run locally. Sub-2s latency. Any app.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 3 — Technical deep-dive: local AI stack

**LinkedIn 🇺🇸**

> **How the local AI stack works in dIKta.me.**
>
> Three separate AI models, all running on your GPU (or CPU), none requiring internet.
>
> **STT — Whisper V3 Turbo**
> Built on whisper.cpp via Whisper.net (C# bindings). GPU acceleration via Vulkan — works on NVIDIA and AMD without CUDA. First run downloads the model (~1.5GB for large-v3). After that: offline.
>
> **LLM — Ollama**
> Any model you have pulled: gemma3:4b (fast, good for refinement), llama3.1:8b (more accurate), qwen2.5 (strong multilingual), minicpm-v (vision). HTTP keep-alive with cached provider instances — took me a while to figure out that creating a new HttpClient per dictation was adding 2500ms of TCP overhead.
>
> **TTS — Kokoro ONNX**
> ~88MB download. Runs fully locally. 20+ voice styles. No cloud required for readback.
>
> Combined latency on a mid-range GPU: ~800ms for dictate+refine. That's fast enough to be invisible.
>
> The architecture is provider-agnostic throughout — same interface for Ollama as for OpenAI or Gemini if you want cloud. Swap one setting.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> How dIKta.me local AI works:
>
> STT: Whisper V3 via whisper.cpp (Vulkan GPU, NVIDIA + AMD)
> LLM: Ollama, HTTP keep-alive, provider caching (~550ms vs 3000ms without cache)
> TTS: Kokoro ONNX, ~88MB, 20+ voices
>
> No internet after first model download.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 4 — The $1,440/year problem (SaaS fatigue)

**LinkedIn 🇺🇸**

> **A typical knowledge worker's AI subscription stack in 2026:**
>
> - Grammarly Pro: $12/month
> - Otter.ai: $10/month
> - Dragon / other dictation: $15/month
> - ChatGPT Plus: $20/month
> - Granola / meeting AI: $14/month
>
> That's $71/month. $852/year. To rent software that still requires you to copy-paste between all of it.
>
> dIKta.me does everything those five products do, in any app, from one hotkey. One-time price: $20.
>
> Or use local AI (Whisper + Ollama) and the running cost is $0/month after that.
>
> I'm not against SaaS. I use SaaS tools. But the model doesn't make sense when the AI inference can run on your own hardware for free. You're paying monthly for a wrapper around models you could run yourself.
>
> The free trial is the free trial. No credit card to start. Try the full app with $1 in cloud credits. If local mode is 4x faster (it is), the Power License pays for itself in about 4 minutes of not waiting for cloud API calls.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> Grammarly + Otter + dictation app + ChatGPT + meeting AI = $71/month = $852/year.
>
> dIKta.me does all of it. One hotkey. Any app.
>
> One-time: $20. Local AI running cost: $0/month.
>
> Stop renting.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 5 — Vision module demo

**LinkedIn 🇺🇸**

> **The feature nobody expects in a dictation app.**
>
> `Ctrl+Alt+S`. Draw a region on your screen. A menu appears:
>
> - **Save** — screenshot to file
> - **Clipboard** — copy to clipboard
> - **Chat** — open the image in the quick chat overlay, ask questions
> - **Note** — voice note with the image as context
> - **OCR** — extract all text from the image
> - **Table** — extract structured data as a spreadsheet-ready table
>
> All of these route to AI. The OCR and table extraction run locally (via minicpm-v on Ollama) or to Gemini in cloud mode.
>
> I use it most for: extracting text from screenshots of PDFs, pulling tables from web pages I can't copy-paste from, asking questions about charts and graphs, and quickly annotating visuals in my notes.
>
> It runs the same pipeline as the dictation modes — same providers, same settings, just image input instead of voice.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> dIKta.me has a vision mode.
>
> Ctrl+Alt+S → draw a region → OCR, table extract, ask questions, save to notes.
>
> Runs locally via minicpm-v (Ollama) or cloud Gemini.
>
> Not what you expect from a dictation app.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### Day 6 — Week 1 retro (build in public)

**LinkedIn 🇺🇸**

> **Week 1 numbers. What I built, what I shipped, what I learned.**
>
> [Replace with actual metrics]
>
> Downloads: [X]
> Power Licenses sold: [X]
> Product Hunt rank: [X]
> GitHub stars: [X]
> Most common feedback theme: [theme]
>
> What I got wrong on Day 0: [honest mistake]
>
> What I got right: [thing that worked]
>
> What's shipping next: [V2.0.1 bug fixes or first V2.1 module]
>
> I started this project because I was frustrated. I finished it because I thought it might be useful to other people who are frustrated by the same things. Week 1 tells me whether I was right about that.
>
> [Results summary and what it means]
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter 🇺🇸**

> Week 1 of dIKta.me:
> [X] downloads · [X] licenses · [X] GitHub stars
>
> Most common feedback: [theme]
> Biggest surprise: [surprise]
> What's shipping next: [feature]
>
> Building in public. Week 2 starts now.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

---

## 6. SEO Blog Outlines (4)

> Outlines only. Full posts to be written separately.
> All posts link to dikta.me and cross-link each other.
> **[ES VERSION NEEDED]** for each — separate URL slugs (`/es/blog/...`).

---

### 6a. "Best dictation software for Windows in 2026"

**Target keyword:** `best dictation software windows`
**Secondary:** `best voice to text windows`, `windows dictation app 2026`
**Intent:** Purchase / comparison
**Estimated word count:** 2,000–2,500

**Outline:**

1. **Intro** — Why dictation software is worth using in 2026 (speed, RSI, AI workflow)
2. **What to look for** — accuracy, AI processing, local/offline, app compatibility, pricing model
3. **The contenders** (brief, fair summaries)
   - Dragon Professional (enterprise, expensive, no AI pipeline)
   - Wispr Flow (accurate, cloud-only, $12/mo, no local)
   - Windows built-in (free, basic, no AI)
   - Aqua Voice (developer-focused, cloud-only)
   - dIKta.me (local-first, AI pipeline, one-time)
4. **Comparison table** — accuracy, local mode, AI processing, price, platform
5. **Use case breakdowns**
   - Best for privacy-conscious users → dIKta.me (local stack)
   - Best for enterprise / compliance → Dragon
   - Best for Mac users → Wispr Flow
   - Best for developers → Aqua Voice or dIKta.me
6. **Deep-dive: dIKta.me** — modes, providers, workflow, pricing
7. **FAQ** — "Is Dragon still worth it?", "Can Windows dictation do AI?", "What's the fastest local STT?"
8. **Conclusion + CTA** → dikta.me free trial

---

### 6b. "Dragon NaturallySpeaking alternatives in 2026"

**Target keyword:** `dragon naturallyspeaking alternative`
**Secondary:** `dragon dictation alternative`, `replace dragon windows`
**Intent:** Switch / comparison
**Estimated word count:** 1,800–2,200

**Outline:**

1. **Intro** — Why people are leaving Dragon (price, no AI pipeline, slow updates)
2. **What Dragon does well** (fairness builds trust)
3. **What Dragon doesn't do** — AI post-processing, local LLM, vision, model-agnostic
4. **The alternatives**
   - Windows built-in dictation (free baseline)
   - Wispr Flow (closest to Dragon UX, cloud-only)
   - dIKta.me (local-first, AI pipeline, cheaper)
   - Aqua Voice (developer vocabulary)
   - Voice control via accessibility tools (Mac-style accessibility)
5. **Migration guide: Dragon → dIKta.me**
   - What transfers: muscle memory for push-to-talk
   - What's different: AI processing, local models, no custom vocabulary training needed
   - What you gain: vision mode, LLM pipeline, $20 vs $15/month
6. **Pricing comparison** — Dragon monthly vs dIKta.me one-time
7. **FAQ** — "Is Dragon still being updated?", "Can I import Dragon custom words?", "How long to switch?"
8. **Conclusion + CTA** → dikta.me free trial

---

### 6c. "Local AI dictation: how to run voice-to-text without the cloud"

**Target keyword:** `local ai dictation`
**Secondary:** `offline voice to text ai`, `whisper dictation windows`, `ollama dictation`
**Intent:** Technical / how-to
**Estimated word count:** 2,000–2,500

**Outline:**

1. **Intro** — Why local AI dictation matters in 2026 (privacy, latency, cost)
2. **The local AI stack explained**
   - STT: Whisper (models, VRAM requirements, accuracy tradeoffs)
   - LLM: Ollama (models for refinement — gemma3, llama3, qwen2.5)
   - TTS: Kokoro (ONNX, voice styles, hardware requirements)
3. **DIY approach** — running Whisper.net + Ollama directly, what you need to build
4. **Using dIKta.me** (the integrated approach)
   - First-run wizard walks through model download
   - Hardware requirements: 8GB+ RAM, 4GB+ VRAM recommended
   - Latency benchmarks: GPU vs CPU, model size tradeoffs
5. **Privacy architecture** — where data goes (nowhere), what's encrypted, what's logged
6. **Use cases for local-first**
   - Medical / legal professionals (sensitive voice)
   - Developers in regulated environments
   - Offline workers / remote locations
   - Anyone with slow or expensive internet
7. **Comparison: local vs cloud** — latency, accuracy, cost, privacy
8. **Setup guide** (step-by-step for dIKta.me local mode)
9. **FAQ** — "Can I run this without a GPU?", "Which Whisper model?", "Does Ollama need internet?"
10. **Conclusion + CTA** → dikta.me Power License (local mode)

---

### 6d. "Voice to text without internet: offline dictation for Windows"

**Target keyword:** `voice to text without internet`
**Secondary:** `offline dictation windows`, `voice to text no internet`, `dictation app offline`
**Intent:** Privacy / offline use case
**Estimated word count:** 1,500–2,000

**Outline:**

1. **Intro** — Who needs offline dictation (privacy, connectivity, compliance)
2. **What "offline" actually means** — no audio upload, no cloud API, no telemetry
3. **Windows built-in options** — what works offline vs what requires internet
4. **The problem with most "offline" claims** — apps that still ping home, sync metadata, upload for "accuracy improvement"
5. **True offline: the technical requirements**
   - Local STT model (Whisper, downloaded once)
   - Local LLM (Ollama, downloaded once)
   - Local TTS (Kokoro)
   - No required internet after setup
6. **dIKta.me offline mode** — setup, what works, what's cloud-only (Wallet)
7. **Performance offline** — GPU recommended, CPU minimum specs
8. **Security audit checklist** — how to verify an app is truly offline (network monitor, firewall rules)
9. **Use case: legal / medical / consulting** — what compliance requires and how local AI satisfies it
10. **FAQ** — "Does dIKta.me work on a plane?", "What happens if I lose internet mid-dictation?", "Can I block it from the internet in my firewall?"
11. **Conclusion + CTA** → dikta.me Power License

---

---

*LAUNCH_CONTENT.md — dIKta.me V2.0*
*Generated: March 2026*
*Brand voice: BRAND_BOOK.md · Facts: RELEASE_ROADMAP.md*
