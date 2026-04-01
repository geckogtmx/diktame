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
6. [Video Content (2 pieces)](#6-video-content-2-pieces)
7. [Feature-Driven Marketing Ideas](#7-feature-driven-marketing-ideas)
8. [Feature Post Library](#8-feature-post-library)
9. [SEO Blog Outlines (4)](#9-seo-blog-outlines-4)

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

But it's not just speed. It's context switching. Every time you move from your work tool to a chat window to ask the AI something, and then back, you pay a context tax. It takes 15-20 minutes to fully recover focus after an interruption. You're interrupting yourself dozens of times per day just to use the tools that are supposed to make you faster.

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

Whisper V3 for speech-to-text. Ollama for the language model. Kokoro for text-to-speech. All on your GPU. Sub-second pipeline. No internet after the first download.

Your voice never leaves your machine unless you choose cloud. Your documents don't get uploaded. Your API keys are encrypted with Windows DPAPI and never transmitted.

This is not a privacy checkbox. It is the architecture.

If you want cloud speed, you can use Deepgram, Gemini, OpenAI, or Anthropic — with your own API keys, at cost, no markup. But you don't have to. Full local mode works. It's fast. It's free to run.

---

**Why it costs $20 once instead of $20/month.**

Because I don't believe you should pay rent on software that runs on your hardware.

The code is MIT-licensed and on GitHub. You can read it, fork it, build it yourself. The Full Version is $20 for people who want the installer and don't want to set up a .NET 8 build environment. That's a fair exchange.

There's a free trial that lets you try all 8 modes with cloud credits before you decide anything. No credit card to start.

If you want to support the project beyond that, there's a Ko-fi for $2/month. It's a donation, not a service contract. No SLA. No obligation.

---

**What's coming.**

V2.0 is the core: voice, vision, text selection, local AI, all workflow modes.

V2.1 adds connectors — route dictation output to Obsidian, webhooks, Discord, Notion. Adds a grammar checking pipeline that works in every app (not just the ones with native integrations). Adds meeting intelligence that merges your voice notes with transcripts to synthesize action items.

After that: a memory layer. The product gets smarter the more you use it, because everything you say and select starts building context.

AI is not a destination you travel to. It should be the environment you work inside.

dIKta.me makes the input layer disappear.

---

*dIKta.me V2.0 is live at [dikta.me](https://dikta.me). Free to try. $20 to own.*

*Built by Eduardo Garcia-Torres. One developer. Three months. 1,134 tests. MIT license.*

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

Pero no es solo velocidad. Es el cambio de contexto. Cada vez que pasas de tu herramienta de trabajo a una ventana de chat para preguntarle algo a la IA, y luego regresas, pagas un impuesto de contexto. Toma entre 15 y 20 minutos recuperar el enfoque tras una interrupción. Te estás interrumpiendo docenas de veces al día solo para usar las herramientas que se supone deben hacerte más rápido.

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

Whisper V3 para voz a texto. Ollama para el modelo de lenguaje. Kokoro para texto a voz. Todo en tu GPU. Pipeline sub-segundo. Sin internet después de la primera descarga.

Tu voz nunca sale de tu máquina a menos que elijas la nube. Tus documentos no se suben. Tus claves API están cifradas con Windows DPAPI y nunca se transmiten.

Esto no es una casilla de privacidad. Es la arquitectura.

Si quieres velocidad en la nube, puedes usar Deepgram, Gemini, OpenAI o Anthropic — con tus propias claves API, a costo real, sin margen. Pero no tienes que hacerlo. El modo local funciona. Es rápido. Es gratuito para correr.

---

**Por qué cuesta $20 una vez en lugar de $20/mes.**

Porque no creo que debas pagar renta por software que corre en tu propio hardware.

El código tiene licencia MIT y está en GitHub. Puedes leerlo, hacer un fork, compilarlo tú mismo. La Versión Completa cuesta $20 para quienes quieren el instalador y no quieren configurar un entorno de compilación de .NET 8. Eso es un intercambio justo.

Hay una prueba gratis que te permite probar los 8 modos con créditos en la nube antes de decidir nada. Sin tarjeta de crédito para empezar.

Si quieres apoyar el proyecto más allá de eso, hay un Ko-fi por $2/mes. Es una donación, no un contrato de servicio. Sin SLA. Sin obligaciones.

---

**Lo que viene.**

V2.0 es el núcleo: voz, visión, selección de texto, IA local, todos los modos de flujo de trabajo.

V2.1 añade conectores — enruta la salida del dictado a Obsidian, webhooks, Discord, Notion. Añade un pipeline de corrección gramatical que funciona en todas las apps. Añade inteligencia de reuniones que fusiona tus notas de voz con transcripciones para sintetizar elementos de acción.

Después de eso: una capa de memoria. El producto se vuelve más inteligente cuanto más lo usas, porque todo lo que dices y seleccionas empieza a construir contexto.

La IA no es un destino al que viajas. Debe ser el entorno dentro del que trabajas.

dIKta.me hace que la capa de entrada desaparezca.

---

*dIKta.me V2.0 ya está disponible en [dikta.me](https://dikta.me). Gratis para probar. $20 para ser tuyo.*

*Construido por Eduardo Garcia-Torres. Un desarrollador. Tres meses. 1,134 pruebas. Licencia MIT.*

---

---

## 2. Product Hunt Launch Page

> Spanish tagline and description paragraphs below — Eduardo to review.

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

**Tagline 🇪🇸**

```
Deja de escribirle a tus modelos de IA. Háblales.
```

*(Alternativa: "Voz + Visión + LLM. Cualquier app. Cualquier modelo. $20 una vez.")*

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

- Free Trial — all 8 modes with cloud credits, no credit card needed
- Full Version — $20 once. Local mode, BYOK, offline. Yours forever.
- Ko-fi Supporter — $2/month, direct line to the builder, early access

**What's next:** App connectors (Obsidian, Discord, webhooks), grammar checking pipeline (Grammarly-style, works in every app), meeting intelligence, semantic memory layer.

**Built by:** Eduardo Garcia-Torres — a marketing and business executive from Mexico with 20+ years in IT consulting and digital media. Not a software engineer by training. This is his first desktop application, built with C#, WinUI 3, and AI coding tools. One developer. Three months. 1,134 tests.

**Open source:** MIT license. GitHub public at launch. Build from source if you prefer — .NET 8 SDK required.

---

**Full Description — 🇪🇸 (first two paragraphs)**

**El problema:** Escribes a 60 palabras por minuto. Piensas a 150. La brecha es trabajo de pegamento — copiar y pegar entre apps, cambiar de contexto a ventanas de chat, pagar cinco suscripciones de IA separadas que no se comunican entre sí.

**Lo que hace dIKta.me:** Presiona un atajo en cualquier aplicación de Windows — Word, VS Code, Slack, tu navegador, lo que sea — y tu voz se convierte en texto procesado en tu cursor. No transcripción cruda. Salida refinada por IA, consciente del contexto, exactamente donde la necesitas.

---

### First Post / Maker Comment (posted IMMEDIATELY at 12:01 AM PST on Day 0)

> **Structure**: Origin story first (builds trust) → HAI manifesto (reframes the problem) → concrete product → price anchor → invitation for questions (drives threaded engagement, worth 40-50x more than votes in PH algorithm).

**How was it born?**

I was paying for 4-5 monthly subscriptions for basic productivity tools. Dictation, grammar, meeting AI, chat, vision — all separate apps, separate bills, separate windows. And I was STILL copy-pasting between all of them.

So I gathered $100 for Claude Code compute and built the tool I wanted. A native Windows app. C#, WinUI 3, 1,134 automated tests. Three months from prototype to launch.

I'm a marketing and business executive with 20+ years in IT consulting, digital media, and project management. Not a software engineer by training. dIKta.me is my first desktop application, built from scratch with AI coding tools. The product decisions come from two decades of building businesses and shipping products — not a CS degree.

**The problem nobody's fixing**

Everyone's racing to make AI smarter. Nobody's fixing how you talk to it.

We've gone from GPT-3 to high reasoning models in 3 years. Context windows went from 2K to 2M tokens. Multimodal, real-time, on-device — the intelligence side is moving fast.

But you still type at it like it's a search engine from 2003. You open a browser tab. You paste text. You upload a screenshot manually. You switch apps to copy context.

The bottleneck was never the AI. The bottleneck is the input layer.

**What dIKta.me does about it**

Press a hotkey in any Windows app — Word, VS Code, Slack, your browser — and your voice becomes AI-processed text at your cursor.

🎙️ **Dictate** — voice to clean text, injected where you work. 3-4x faster than typing.

👁️ **Vision** — point at anything on your screen, ask a question, get an answer. No copy-paste.

✂️ **Refine** — highlight text, speak "make this shorter" or "translate to Spanish." One hotkey.

🏠 **Local-first** — Whisper + Ollama + Kokoro TTS on your GPU. Sub-1.2s latency. Nothing leaves your machine unless you choose cloud.

**$20 once. No subscription. Your models, your hardware, your data.**

MIT open source — read every line on GitHub.

I read every comment here. Ask me anything about the architecture, the local AI setup, or the decisions behind it.

> **First Post 🇪🇸** (post as reply if PH audience warrants it)

**Cómo nació**

Pagaba 4-5 suscripciones mensuales por herramientas básicas de productividad. Dictado, gramática, reuniones con IA, chat, visión — apps separadas, facturas separadas, ventanas separadas. Y SEGUÍA copiando y pegando entre todas.

Así que junté $100 para cómputo de Claude Code y construí la herramienta que quería. Una app nativa de Windows. C#, WinUI 3, 1,134 tests automatizados. Tres meses de prototipo a lanzamiento.

Soy ejecutivo de marketing y negocios con más de 20 años en consultoría IT, medios digitales y gestión de proyectos. No soy ingeniero de software. dIKta.me es mi primera aplicación de escritorio, construida desde cero con herramientas de IA.

**El problema que nadie está resolviendo**

Todos compiten por hacer la IA más inteligente. Nadie está arreglando cómo le hablas.

Pasamos de GPT-3 a modelos de razonamiento avanzado en 3 años. Pero sigues escribiéndole como si fuera un buscador de 2003. Abres una pestaña. Pegas texto. Subes capturas manualmente. Cambias de app para copiar contexto.

El cuello de botella nunca fue la IA. El cuello de botella es la capa de entrada.

**Qué hace dIKta.me**

Presiona un atajo en cualquier app de Windows y tu voz se convierte en texto procesado por IA en tu cursor.

🎙️ **Dictar** — voz a texto limpio, inyectado donde trabajas. 3-4x más rápido que escribir.

👁️ **Visión** — apunta a cualquier cosa en tu pantalla, haz una pregunta, obtén respuesta.

✂️ **Refinar** — selecciona texto, di "hazlo más corto" o "traduce al inglés." Un atajo.

🏠 **Local-first** — Whisper + Ollama + Kokoro TTS en tu GPU. Latencia sub-1.2s. Nada sale de tu máquina a menos que elijas la nube.

**$20 una vez. Sin suscripción. Tus modelos, tu hardware, tus datos.**

MIT open source — lee cada línea en GitHub.

Leo cada comentario aquí. Pregúntame lo que quieras.

---

### PH Comment Reply Templates (Pre-Written for Speed)

> **Context**: 1 quality comment ≈ 40-50 upvotes in PH algorithm. Respond to every comment within 10 minutes. Always ask a follow-up question to drive threaded engagement.

**Q: "How does local mode actually work?"**
> Great question. The local stack is: Whisper.net for STT (Vulkan GPU acceleration), Ollama for the LLM (any model — gemma3, llama3, etc.), and KokoroSharp for TTS. Everything runs on your GPU, nothing leaves your machine. Sub-1.2s latency end-to-end once models are warm. What kind of hardware are you running? Happy to suggest models that would work well.

**Q: "Why Windows only?"**
> Deliberate choice. Every competitor in this space is Mac-first — Wispr Flow, superwhisper, MacWhisper. Windows users have nothing native. WinUI 3 gives us OS-level text injection (SendInput) that works in any app, which isn't possible with Electron or web wrappers. Mac is on the roadmap for V3. What's your primary OS?

**Q: "Is $20 sustainable? What's the catch?"**
> No catch — the economics just work differently. dIKta.me runs on YOUR hardware for local mode. I don't pay for GPU inference. Cloud mode uses your own API keys (Deepgram, Gemini, OpenAI) — no proxy, no markup. The only cloud cost I carry is the wallet system for users who don't want to manage API keys, and that's pay-as-you-go. Plus it's MIT open source — even if I disappeared tomorrow, the code is yours.

**Q: "How does this compare to Wispr Flow?"**
> Wispr Flow is excellent — polished, cross-platform, great Command Mode. Where dIKta.me differs: (1) runs fully local on your GPU for zero cloud dependency, (2) $20 once vs $180/yr, (3) model-agnostic — swap any STT/LLM/TTS provider, (4) Vision mode for screen analysis, (5) MIT open source. Where Wispr wins: Mac support, enterprise compliance, more polish. If you need cross-platform right now, Wispr is great. If you want local-first on Windows with model freedom, that's us.

**Q: "I already use ChatGPT/Copilot for everything"**
> Those live in a browser tab. dIKta.me works at the OS level — press a hotkey in Word, VS Code, Slack, email, literally any app, and the output appears at your cursor. No tab-switching, no copy-paste. The Refine mode is the killer: select text anywhere, speak "make this more concise" or "translate to Spanish," and it updates in-place. It's the difference between a tool you visit vs a tool that's always there. What apps do you use most?

**Q: "What about privacy?"**
> Four privacy levels: (1) Full local — everything on your GPU, nothing leaves your machine, (2) BYOK cloud — your API keys, direct connection to providers, I never see your data, (3) Wallet mode — I route through edge functions but don't store content, (4) You choose per-session with a toggle. Credentials stored with Windows DPAPI encryption. And it's MIT open source — audit every line.

**Q: "What's the free trial like?"**
> Sign in at dikta.me, get cloud credits to try all 8 modes immediately. No credit card, no time limit on the trial. The credits cover enough dictation to know if it fits your workflow. If you want local AI (faster, offline, private), Full Version is $20 once. What kind of work do you do? I can suggest which modes to try first.

**Q: "Built by one person? Can I trust it?"**
> Fair concern. Three things: (1) 1,134 automated tests with CI on GitHub Actions — every commit is verified, (2) MIT license — you can read, fork, and modify every line, (3) it's a native Windows app, not a cloud service — if I get hit by a bus, the app still runs on your machine. Also I've been shipping products for 20 years in consulting — this is my first desktop app, but not my first product.

**Q: "What's on the roadmap?"**
> V2.1 is where it gets interesting: (1) Connectors — plug into Obsidian, Notion, calendar, email, (2) Grammar pipeline — real-time correction without a Grammarly subscription, (3) Memory layer — the app remembers context across sessions, (4) "Chaviz" conversational orchestrator — speak a command, get things done across your system. The architecture is modular specifically so these extensions don't require rewriting the core. What feature would be most useful for your workflow?

**Q: "Does it work with [specific app]?"**
> It works in any Windows app that accepts text input — Word, VS Code, Slack, Discord, Chrome, Firefox, Outlook, Notion (desktop), Obsidian, terminal emulators, literally any text field. It injects at the OS level using SendInput with a clipboard fallback for apps that don't accept keystroke injection. What app are you thinking of? I can test it right now.

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

1,134 unit tests (xUnit + Moq + FluentAssertions). CI on GitHub Actions.

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
- Free trial: sign in, try all 8 modes with cloud credits
- Full Version: $20 one-time, local mode + BYOK + offline

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

Pricing: Free Trial with cloud credits, $20 one-time for Full Version (local mode, BYOK, offline). MIT license, source on GitHub at launch.

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

- Free to try (all modes, cloud credits included)
- $20 one-time for local mode (Whisper + Ollama — 4x faster, offline, private)

dikta.me if you want to check it out. Happy to answer questions about specific workflows.

---

### 4c. r/LocalLLaMA

> **Title:** Built a Windows dictation app that uses Ollama as its LLM brain — full local pipeline with Whisper + Kokoro

**Body:**

Built dIKta.me — a native Windows app (C#/WinUI 3) that uses Ollama as the LLM backend for voice dictation.

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

MIT license, source on GitHub at launch. Full Version ($20 one-time) for the installer and pre-built binaries.

---

---

## 5. Launch Week Social Batch (7 Days)

> Template per RELEASE_ROADMAP.md Appendix A. Day 0 = announcement.
> Each day: LinkedIn (long-form) + X/Twitter (punchy, ≤280 chars). English + Spanish.
> Eduardo to review and correct Spanish.

---

### Day 0 — Announcement (Launch Day)

**LinkedIn 🇺🇸**

> **dIKta.me V2.0 is live.**
>
> Three months. One developer. 1,134 tests. MIT license. Here's what I built.
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

**LinkedIn 🇪🇸**

> **dIKta.me V2.0 ya está disponible.**
>
> Tres meses. Un desarrollador. 1,134 pruebas. Licencia MIT. Esto es lo que construí.
>
> El problema: los modelos de IA mejoraron 3x. Las ventanas de contexto crecieron 250x. Pero sigues escribiendo en un cuadro de chat y copiando y pegando entre apps. Nadie arregló la capa de entrada.
>
> dIKta.me lo hace.
>
> 8 modos activados por atajos que funcionan en cualquier app de Windows — Dictar, Refinar, Preguntar, Traducir, Visión, Nota, Chat Rápido, Leer Selección. Los resultados aparecen en tu cursor. Sin cambiar de app. Sin copiar y pegar.
>
> Corre completamente local: Whisper V3 para STT, Ollama para LLM, Kokoro para TTS. O usa proveedores en la nube con tus propias claves. Tu elección.
>
> Gratis para probar. $20 para ser tuyo. Licencia MIT si quieres compilarlo desde el código fuente.
>
> dikta.me — Leo cada comentario.

**X / Twitter 🇺🇸**

> dIKta.me V2.0 is live.
>
> 8 voice AI modes. Any Windows app. Local Whisper + Ollama + Kokoro or cloud. Speak → result at cursor.
>
> Free to try. $20 to own.
>
> dikta.me

**X / Twitter 🇪🇸**

> dIKta.me V2.0 ya está disponible.
>
> 8 modos de voz con IA. Cualquier app de Windows. Whisper + Ollama + Kokoro local o nube. Habla → resultado en tu cursor.
>
> Gratis para probar. $20 para ser tuyo.
>
> dikta.me

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

**LinkedIn 🇪🇸**

> 24 horas después. Esto es lo que pasó.
>
> [Reemplazar con números reales: descargas, instalaciones, posición en PH, comentarios, DMs]
>
> Tres cosas que aprendí del feedback del Día 1:
>
> [Reemplazar con 3 observaciones reales del feedback]
>
> La pregunta más frecuente: [pregunta más común]. La respuesta: [respuesta].
>
> Lo que se está corrigiendo primero: [bug principal o problema de UX del feedback].
>
> Gracias a todos los que lo probaron, votaron, comentaron o enviaron notas. Cada uno de esos importa más de lo que imaginas cuando construyes solo.
>
> dikta.me

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

**X / Twitter 🇪🇸**

> Día 1 de @dIKtaMe:
> [X] instalaciones
> Posición en Product Hunt: [X]
> Pregunta más frecuente: [pregunta]
> Bug principal: [bug]
>
> Construyendo en público. Esto es lo que corrijo primero: [fix]
>
> dikta.me

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
> Local mode: Whisper + Ollama. Sub-second combined latency on a mid-range GPU.
>
> dikta.me

**LinkedIn 🇪🇸**

> **Los dos modos que uso 20 veces al día.**
>
> **Modo Dictar** (`Ctrl+Alt+D`): presiona, habla, suelta. El texto limpio aparece en el cursor. No transcripción cruda — puntuado, formateado, listo para enviar o publicar. Funciona en cualquier app: correo, Slack, VS Code, Google Docs, Word, tu IDE, lo que sea.
>
> La diferencia con el dictado básico: no es solo volcar palabras. Está pasando tu habla por un modelo de lenguaje que maneja puntuación, saltos de párrafo y correcciones menores, mientras preserva tu significado.
>
> **Refinar Voz** (`Ctrl+Alt+R` mantener): selecciona texto, mantén el atajo, di "hazlo más formal" o "córtalo a la mitad" o "traduce al español" — suelta. El texto se reescribe en su lugar. Sin nueva ventana. Sin copiar y pegar. El cursor se queda donde estaba.
>
> Lo uso para corregir correos que redacté demasiado rápido, condensar notas de reunión y adaptar el mismo párrafo para diferentes audiencias sin cambiar de app.
>
> Modo local: Whisper + Ollama. Latencia combinada sub-segundo en una GPU de gama media.
>
> dikta.me

**X / Twitter 🇺🇸**

> Two modes I use constantly:
>
> Dictate: press hotkey, speak, text appears at cursor in any app.
>
> Refine Voice: select text, say "cut it in half" or "make this formal" → text rewrites in-place.
>
> Both run locally. Sub-second. Any app.
>
> dikta.me

**X / Twitter 🇪🇸**

> Dos modos que uso constantemente:
>
> Dictar: presiona el atajo, habla, el texto aparece en el cursor en cualquier app.
>
> Refinar Voz: selecciona texto, di "córtalo a la mitad" o "hazlo más formal" → el texto se reescribe en su lugar.
>
> Ambos corren localmente. Sub-segundo. Cualquier app.
>
> dikta.me

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
> Combined latency on a mid-range GPU: sub-second for dictate+refine. Fast enough to be invisible.
>
> The architecture is provider-agnostic throughout — same interface for Ollama as for OpenAI or Gemini if you want cloud. Swap one setting.
>
> dikta.me

**LinkedIn 🇪🇸**

> **Cómo funciona el stack de IA local en dIKta.me.**
>
> Tres modelos de IA separados, todos corriendo en tu GPU (o CPU), ninguno requiriendo internet.
>
> **STT — Whisper V3 Turbo**
> Construido sobre whisper.cpp vía Whisper.net (bindings de C#). Aceleración GPU vía Vulkan — funciona en NVIDIA y AMD sin CUDA. La primera ejecución descarga el modelo (~1.5GB para large-v3). Después de eso: sin conexión.
>
> **LLM — Ollama**
> Cualquier modelo que hayas descargado: gemma3:4b (rápido, bueno para refinado), llama3.1:8b (más preciso), qwen2.5 (multilingüe sólido), minicpm-v (visión). HTTP keep-alive con instancias de proveedores en caché — me tomó un tiempo darme cuenta de que crear un nuevo HttpClient por dictado estaba añadiendo 2500ms de overhead de TCP.
>
> **TTS — Kokoro ONNX**
> ~88MB de descarga. Corre completamente local. Más de 20 estilos de voz. Sin nube para la lectura de retorno.
>
> Latencia combinada en una GPU de gama media: sub-segundo para dictar+refinar. Suficientemente rápido para ser invisible.
>
> La arquitectura es agnóstica al proveedor — misma interfaz para Ollama que para OpenAI o Gemini si quieres nube. Cambia una configuración.
>
> dikta.me

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

**X / Twitter 🇪🇸**

> Cómo funciona la IA local de dIKta.me:
>
> STT: Whisper V3 vía whisper.cpp (Vulkan GPU, NVIDIA + AMD)
> LLM: Ollama, HTTP keep-alive, caché de proveedores (~550ms vs 3000ms sin caché)
> TTS: Kokoro ONNX, ~88MB, 20+ voces
>
> Sin internet después de la primera descarga.
>
> dikta.me

---

### Day 4 — One app, one price

**LinkedIn 🇺🇸**

> **How many AI subscriptions are you paying for right now?**
>
> Dictation. Grammar checking. Meeting notes. Chat assistant. Screenshot tools. Each one does one thing. Each one charges monthly. And you're still copy-pasting between all of them.
>
> I built dIKta.me because I wanted one app that handled the whole input layer — voice, text selection, and vision — in whatever app I was already working in.
>
> Speak and get clean text at your cursor. Select text and have AI rewrite it in place. Ask a question without leaving your editor. Translate on the fly. Take voice notes to a file. Hear anything read aloud. Point at your screen and get answers.
>
> All of that runs on your hardware. No cloud required. Free to try. $20 once for the Full Version.
>
> Add up what you're paying monthly. Then try dIKta.me and see how many tools you still need.
>
> dikta.me

**LinkedIn 🇪🇸**

> **¿Cuántas suscripciones de IA estás pagando ahora mismo?**
>
> Dictado. Corrección gramatical. Notas de reunión. Asistente de chat. Herramientas de capturas. Cada una hace una cosa. Cada una cobra mensualmente. Y sigues copiando y pegando entre todas.
>
> Construí dIKta.me porque quería una sola app que manejara toda la capa de entrada — voz, selección de texto y visión — en cualquier app donde ya estuviera trabajando.
>
> Habla y obtén texto limpio en tu cursor. Selecciona texto y la IA lo reescribe en su lugar. Haz una pregunta sin salir de tu editor. Traduce al vuelo. Toma notas de voz a un archivo. Escucha cualquier cosa leída en voz alta. Apunta a tu pantalla y obtén respuestas.
>
> Todo eso corre en tu hardware. Sin nube necesaria. Gratis para probar. $20 una vez por la Versión Completa.
>
> Suma lo que pagas mensualmente. Luego prueba dIKta.me y ve cuántas herramientas sigues necesitando.
>
> dikta.me

**X / Twitter 🇺🇸**

> How many AI subscriptions are you paying for right now?
>
> dIKta.me does dictation, grammar, translation, Q&A, vision, and TTS. One app. One hotkey. Any Windows app.
>
> Free to try. $20 once.
>
> dikta.me

**X / Twitter 🇪🇸**

> ¿Cuántas suscripciones de IA estás pagando ahora mismo?
>
> dIKta.me hace dictado, gramática, traducción, Q&A, visión y TTS. Una app. Un atajo. Cualquier app de Windows.
>
> Gratis para probar. $20 una vez.
>
> dikta.me

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

**LinkedIn 🇪🇸**

> **La función que nadie espera en una app de dictado.**
>
> `Ctrl+Alt+S`. Dibuja una región en tu pantalla. Aparece un menú:
>
> - **Guardar** — captura a archivo
> - **Portapapeles** — copiar al portapapeles
> - **Chat** — abrir la imagen en el overlay de chat rápido, hacer preguntas
> - **Nota** — nota de voz con la imagen como contexto
> - **OCR** — extraer todo el texto de la imagen
> - **Tabla** — extraer datos estructurados como tabla lista para hoja de cálculo
>
> Todas estas opciones enrutan a IA. El OCR y la extracción de tablas corren localmente (vía minicpm-v en Ollama) o en Gemini en modo nube.
>
> Lo uso más para: extraer texto de capturas de pantalla de PDFs, obtener tablas de páginas web de las que no puedo copiar y pegar, hacer preguntas sobre gráficas y diagramas, y anotar rápidamente visuales en mis notas.
>
> Corre el mismo pipeline que los modos de dictado — mismos proveedores, misma configuración, solo entrada de imagen en lugar de voz.
>
> dikta.me

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

**X / Twitter 🇪🇸**

> dIKta.me tiene un modo de visión.
>
> Ctrl+Alt+S → dibuja una región → OCR, extrae tabla, haz preguntas, guarda en notas.
>
> Corre localmente vía minicpm-v (Ollama) o Gemini en la nube.
>
> No es lo que esperas de una app de dictado.
>
> dikta.me

---

### Day 6 — Week 1 retro (build in public)

**LinkedIn 🇺🇸**

> **Week 1 numbers. What I built, what I shipped, what I learned.**
>
> [Replace with actual metrics]
>
> Downloads: [X]
> Licenses sold: [X]
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

**LinkedIn 🇪🇸**

> **Números de la Semana 1. Lo que construí, lo que lancé, lo que aprendí.**
>
> [Reemplazar con métricas reales]
>
> Descargas: [X]
> Licencias Power vendidas: [X]
> Posición en Product Hunt: [X]
> Estrellas en GitHub: [X]
> Tema de feedback más común: [tema]
>
> Lo que hice mal el Día 0: [error honesto]
>
> Lo que hice bien: [cosa que funcionó]
>
> Lo que viene próximamente: [correcciones de V2.0.1 o primer módulo de V2.1]
>
> Empecé este proyecto porque estaba frustrado. Lo terminé porque pensé que podría ser útil para otras personas frustradas por las mismas cosas. La Semana 1 me dice si tenía razón.
>
> [Resumen de resultados y lo que significan]
>
> dikta.me

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

**X / Twitter 🇪🇸**

> Semana 1 de dIKta.me:
> [X] descargas · [X] licencias · [X] estrellas en GitHub
>
> Feedback más común: [tema]
> Mayor sorpresa: [sorpresa]
> Lo que viene después: [feature]
>
> Construyendo en público. Empieza la Semana 2.
>
> dikta.me

---

### Day 7 — What's next (forward-looking)

**LinkedIn 🇺🇸**

> **One week in. Here's what comes next.**
>
> dIKta.me V2.0 shipped with 8 voice modes, local AI, and vision. That's the foundation. Here's what I'm building on top of it.
>
> **Connectors** — route your dictation output to Obsidian, Notion, Discord, webhooks. Your voice goes where your tools are.
>
> **Grammar pipeline** — AI text refinement that works in every app, not just the ones with browser extensions.
>
> **Meeting intelligence** — merge voice notes with meeting transcripts. Get action items without a separate tool.
>
> **Memory layer** — the app learns your patterns. Context from previous sessions makes every dictation smarter.
>
> All of this builds on the same local-first architecture. Your data. Your hardware. No new subscriptions.
>
> If you haven't tried it yet: dikta.me — free trial, no card needed.
>
> If you have ideas for what should come first: I'm listening. Comments, DMs, GitHub issues — all of it.

**LinkedIn 🇪🇸**

> **Una semana después. Esto es lo que viene.**
>
> dIKta.me V2.0 salió con 8 modos de voz, IA local y visión. Esa es la base. Esto es lo que estoy construyendo encima.
>
> **Conectores** — enruta la salida del dictado a Obsidian, Notion, Discord, webhooks. Tu voz va donde están tus herramientas.
>
> **Pipeline de gramática** — refinamiento de texto con IA que funciona en cada app, no solo las que tienen extensiones de navegador.
>
> **Inteligencia de reuniones** — fusiona notas de voz con transcripciones de reuniones. Obtén elementos de acción sin una herramienta separada.
>
> **Capa de memoria** — la app aprende tus patrones. El contexto de sesiones anteriores hace cada dictado más inteligente.
>
> Todo esto se construye sobre la misma arquitectura local-first. Tus datos. Tu hardware. Sin nuevas suscripciones.
>
> Si aún no lo has probado: dikta.me — prueba gratis, sin tarjeta.
>
> Si tienes ideas sobre qué debería venir primero: estoy escuchando. Comentarios, DMs, issues en GitHub — todo cuenta.

**X / Twitter 🇺🇸**

> Week 1 done. What's next for dIKta.me:
>
> - Connectors (Obsidian, Notion, Discord)
> - Grammar pipeline (works in every app)
> - Meeting intelligence
> - Memory layer
>
> Same local-first architecture. No new subscriptions.
>
> What should ship first? dikta.me

**X / Twitter 🇪🇸**

> Semana 1 completa. Lo que viene para dIKta.me:
>
> - Conectores (Obsidian, Notion, Discord)
> - Pipeline de gramática (funciona en toda app)
> - Inteligencia de reuniones
> - Capa de memoria
>
> Misma arquitectura local-first. Sin nuevas suscripciones.
>
> ¿Qué debería salir primero? dikta.me

---

---

## 6. Video Content (2 pieces)

> Eduardo has screen recording, video editing (DaVinci Resolve), audio editing (Audacity), and a full streaming setup.
> Both videos are designed to be recorded in one or two takes with minimal editing — no face required unless you want it.
> Format: MP4, landscape 1080p. Publish to YouTube (primary) + repurpose clips for X, LinkedIn, Reddit.

---

### Video 1 — "The 90-second demo" (Launch trailer)

**Purpose:** The asset that goes everywhere on Day 0. Product Hunt gallery, GitHub README, dikta.me homepage, every social post. The first thing a skeptic watches.

**Target length:** 60–90 seconds

**Tone:** No narration required. Let the product speak. If you narrate, speak like you would to a colleague — one sentence at a time, no script reading.

**What to show (in order):**

1. **(0:00–0:10)** Open a blank document (Word, Notion, or VS Code). No dIKta.me visible yet. Just a cursor blinking.

2. **(0:10–0:25)** Press `Ctrl+Alt+D`. The waveform appears on the control panel. Speak one or two natural sentences — something real, not "testing testing." Release. Clean, punctuated text appears at the cursor. **No cut here** — show the real latency.

3. **(0:25–0:40)** Select a sentence of that text. Hold `Ctrl+Alt+R`. Say "make this more direct." Release. Watch it rewrite in-place. No cut — show the real speed.

4. **(0:40–0:55)** Press `Ctrl+Alt+S`. Draw a region over something on screen (a table, a chart, a PDF snippet). Choose OCR or Table. Text extracts. One beat of pause to let it land.

5. **(0:55–1:10)** End on the dIKta.me logo + dikta.me URL.

**Overlay text (minimal, add in DaVinci Resolve):**
- At step 2: `Ctrl+Alt+D — Dictate`
- At step 3: `Ctrl+Alt+R — Refine`
- At step 4: `Ctrl+Alt+S — Vision`
- End card: `dIKta.me · Free to try · $20 to own`

**Audio:** No music needed — the sound of real keystrokes and the waveform animation carry it. If you add music, keep it instrumental and quiet so the UI sounds come through.

**DaVinci Resolve notes:**
- Cut only to remove dead air between steps — keep real-time latency visible
- Add subtle zoom-in (1.05–1.1x) on the cursor area during text injection so viewers can see the output clearly
- Color grade: keep it close to the Midnight theme palette — cool dark tones, no warm grade
- Export: H.264, 1080p, ~30fps. Under 90 seconds = fine for X native upload

**Recording tips:**
- Do one real take before the "clean" take — genuine latency is the pitch, not a liability
- Dictate something real: a sentence you'd actually write in your work
- Recommended app: VS Code or Notion (recognizable to the target audience)

**Repurpose as:**
- Product Hunt gallery (first asset)
- GitHub README embed
- Day 0 X/Twitter native upload (no YouTube link — autoplay matters)
- Day 0 LinkedIn post
- Reddit posts (r/productivity, r/LocalLLaMA)

---

### Video 2 — "Local AI dictation setup in 5 minutes" (Technical walkthrough)

**Purpose:** Trust-building for the privacy / self-hosted audience. Shows exactly what "local mode" means in practice. Also an SEO asset for "how to set up local AI dictation windows."

**Target length:** 4–6 minutes

**Tone:** Calm, technical, honest. You're walking someone through your own setup. Not a tutorial read from a script — more like pairing with a colleague who hasn't done this before.

**Structure:**

**Part 1 — Hook (30 sec)**
Spoken or title card: *"Most AI dictation tools send your voice to a server. Here's how to run the whole stack locally — STT, LLM, TTS — on your own machine."*

**Part 2 — The first-run wizard (60 sec)**
- Open dIKta.me, show the wizard
- Highlight the three provider choices: STT, LLM, TTS
- "I'm going to pick local for all three"

**Part 3 — Whisper download (90 sec)**
- Select Whisper in the wizard, pick a model size
- Show the download progress
- Callout: *"This is whisper.cpp. Runs on your GPU via Vulkan — NVIDIA and AMD, no CUDA required."*

**Part 4 — Ollama setup (90 sec)**
- Show the Ollama wizard step. If already installed, say so and skip ahead.
- `ollama pull gemma3:4b` — show the pull progress
- *"This is the model that rewrites and refines your text. You can swap it for anything you already have — llama3, qwen2.5, whatever."*

**Part 5 — Live demo with proof (60–90 sec)**
- Same dictate + refine sequence as Video 1
- This time, call it out explicitly: *"Watch the network — nothing is going out. This is running on my machine."*
- Show Task Manager network graph briefly, or use a network monitor overlay — flat line during dictation is the proof
- State your GPU: *"I'm on an RTX 3060 — sub-second for Whisper plus Ollama."*

**Part 6 — Wrap (30 sec)**
- *"Free to try with cloud credits. $20 one-time for local mode. MIT license if you want to build from source."*
- dikta.me URL on screen

**On-screen text to add in DaVinci Resolve:**
- Part 3: `Whisper V3 Turbo · ~800MB · runs offline after download`
- Part 4: `Ollama · gemma3:4b · ~2.5GB · $0/month`
- Part 5: `Sub-second · Whisper + Ollama · no internet · RTX 3060`

**Audacity notes:**
- Record narration separately from screen capture if your streaming setup allows it — easier to re-record a line without re-recording the screen
- Light noise reduction pass. No compression needed — keep it natural, not podcast-polished

**DaVinci Resolve notes:**
- Hard cuts only — no transitions
- The network monitor moment in Part 5 can be a picture-in-picture inset (small, bottom corner) while the main screen shows dictation
- Export: H.264, 1080p. YouTube SEO title: *"Local AI dictation on Windows — Whisper + Ollama + Kokoro (dIKta.me setup guide)"*

**Repurpose as:**
- YouTube (primary SEO asset for "local ai dictation windows")
- r/selfhosted and r/LocalLLaMA launch posts — embed directly
- Day 3 LinkedIn post (technical deep-dive day)
- dikta.me/getting-started page
- Clip Part 5 (~90 sec) as a standalone short for X/Twitter

---

## 7. Feature-Driven Marketing Ideas

> These are specific copy angles and video moments that come directly from reading the feature docs.
> Use these as raw material — drop them into social posts, Reddit comments, Product Hunt Q&A, or the demo video script.

---

### The moments worth showing (video / GIF candidates)

**1. Oops recovery**
The scenario: you dictate a paragraph, your cursor was in the wrong window, it lands in Slack. You hit `Ctrl+Z` in Slack to undo it, click where you actually wanted it, press `Ctrl+Alt+V`. It re-injects instantly. Nobody expects this. Show it unedited — wrong window, undo, re-inject, done. 10 seconds. This is the moment that makes people say "wait, it does *that*?"

Copy angle: *"Dictated into the wrong window. One hotkey to fix it. No re-recording."*

**2. Refine autopilot on a bad email draft**
Type a rough, typo-ridden email draft. Select all. `Ctrl+Alt+R`. Watch it clean up in place. Don't narrate. Let the before/after speak. This is the Grammarly comparison in 15 seconds.

Copy angle: *"Grammarly corrects your typing. Refine corrects your thinking."*

**3. Vision → Table → Excel**
Screenshot a table from a web page or a PDF you can't copy-paste. `Ctrl+Alt+S`, draw region, Table. TSV result. Paste into Excel/Sheets. Structured data from an image in under 10 seconds. This one plays extremely well on LinkedIn to finance, ops, and analyst audiences.

Copy angle: *"Screenshot → spreadsheet. No manual re-typing."*

**4. Voice Macro triggered mid-dictation**
Set up a macro for a signature or a boilerplate phrase. Dictate naturally, say the trigger phrase mid-sentence, watch it expand. Nobody thinks of dictation software having text expansion built in.

Copy angle: *"It's a dictation app. It's also a text expander. Same hotkey."*

**5. Translate in a live Slack conversation**
Open Slack. Spanish message on screen. `Ctrl+Alt+T`, speak in English, Spanish text appears in the message box. Or reverse — bilingual reply without a separate tab. This is the one that lands with the 40M+ bilingual professionals angle.

Copy angle: *"Reply in Spanish without switching tabs. Speak English, send Spanish."*

**6. Ask → Clipboard → Paste**
Hold `Ctrl+Alt+A`, ask a factual question or request a regex. Release. A toast notification shows the answer and it's already in your clipboard. Paste it anywhere. This is the "AI that doesn't interrupt you" moment.

Copy angle: *"Asked the AI a question. Didn't leave my IDE."*

**7. Quick Chat + Clipboard Attach**
Copy a confusing error message. `Ctrl+Alt+C`, attach clipboard, say "what does this mean?" Answer in the overlay. No browser tab, no copy-paste loop. Strong developer audience play.

Copy angle: *"Error in terminal. Answer in overlay. Cursor never left VS Code."*

**8. Video recording → Document**
Record 60 seconds of yourself doing a workflow. Stop. Click Document. Gemini returns step-by-step instructions for what it just watched. This is the "documentation writes itself" moment — strong LinkedIn angle for team leads, ops, and trainers.

Copy angle: *"Recorded myself doing it once. Got the documentation automatically."*

**9. Note → Obsidian vault**
Set the Notes file path to point directly into your Obsidian vault. `Ctrl+Alt+N` mid-meeting, voice note appends with timestamp. Done. No tab switch, no copy-paste, no friction. This is the teaser for the Connectors module — but it works *right now* via file path.

Copy angle: *"I pointed the Notes file at my Obsidian vault. Voice notes have been going there ever since."*

**10. Audio ducking mid-Spotify**
Start dictating while music is playing. Spotify fades down automatically, you speak, it fades back up. Show this in a screen recording with the audio — it's a production quality detail that signals care. Very shareable by the "I use my computer for everything" audience.

Copy angle: *"It knows when you're talking. Spotify disagrees."*

**11. Mute detection catch**
Attempt to dictate while hardware-muted. dIKta.me alerts you instantly instead of silently eating the dictation. Simple QoL moment — everyone has been burned by this.

Copy angle: *"Your mic was muted. dIKta.me told you. Dragon did not."*

**12. Custom Dictation Mode: "Medical Transcriber" or "C# XML comments"**
Show creating a preset named "C# XML Doc Comments" with a specific cloud prompt and a shorter local prompt. Switch to it from the Control Panel dropdown. Dictate a function description — it comes out as perfectly formatted XML doc comment. This is the power user moment that makes developers stop scrolling.

Copy angle: *"I made a preset called 'C# XML Comments.' Now I just say what the function does."*

**13. Ghost Mode**
Show Ghost Mode being toggled on. Nothing written to disk. Nothing logged. Then dictate something. Then show the (empty) logs folder. For legal, medical, and security professionals — this is the feature that matters more than anything else.

Copy angle: *"Ghost Mode. Nothing written to disk. Not even token counts. The application operates completely namelessly."*

**14. Macro expansion post-LLM**
Key detail from the docs: macros expand *after* the LLM finishes, so the LLM can't accidentally reword or hallucinate your template content. This is a technical trust argument — your signature, your legal disclaimer, your boilerplate are never touched by AI. Show a macro with a precise legal disclaimer expanding verbatim.

Copy angle: *"The AI formats your words. The macro expands after. Your legal disclaimer arrives exactly as written — AI-proof."*

**15. Translate with a custom target language prompt**
The Translate mode isn't just EN↔ES — it's any language pair, driven by a system prompt. Show changing the prompt to "Translate to formal Japanese" and dictating in English. Japanese text appears at the cursor. This opens the product to a global audience beyond the bilingual-professionals angle.

Copy angle: *"The translate mode speaks every language. You just change the prompt."*

**16. Quick Chat web search grounding (Gemini)**
Show Quick Chat with Gemini + web search enabled. Ask a time-sensitive question ("what's the latest version of React?"). The answer cites sources and is current. This is the ChatGPT comparison — but it stays in an overlay, doesn't leave your workflow.

Copy angle: *"AI overlay with live web search. Never left my IDE."*

**17. Video recording + "Document" — the accidental documentation machine**
Record a 60-second workflow in an app. Click Stop. Click Document. Get back step-by-step instructions for what the recording showed. Frame this for team leads and ops people who spend hours writing SOPs and training guides.

Copy angle: *"Recorded my workflow once. Got the SOP automatically. That's the whole use case."*

---

### Additional video ideas

> Beyond the 2 launch videos — these are YouTube / content library pieces for weeks 2–8 post-launch.

---

**Video 3 — "5 things dIKta.me does that ChatGPT can't"**
*Format: 3–4 minutes, screen recording + narration*
*Audience: general AI users, LinkedIn, YouTube*

1. Injects text directly at your cursor — ChatGPT gives you text to copy
2. Works in any app without switching tabs
3. Runs fully offline — ChatGPT requires internet and an account
4. Vision mode analyzes what's on *your* screen, not an uploaded file
5. Audio ducking, macro expansion, and Oops recovery — ChatGPT has none of these

Each point: 30 seconds, quick demo, no lingering. Fast-cut. End with: *"$20 once. Free to try. dikta.me."*

DaVinci notes: split-screen comparison works well here — ChatGPT on left, dIKta.me on right doing the same task. Keep it fair and factual, not mocking.

---

**Video 4 — "I replaced Grammarly with a hotkey"**
*Format: 2–3 minutes, screen recording*
*Audience: writers, productivity crowd, LinkedIn*

The entire video is a single workflow:
1. Write a rough paragraph (visibly rough — typos, passive voice, unclear)
2. Select all
3. `Ctrl+Alt+R` — Autopilot Refine with a "fix grammar and clarity" prompt
4. Watch it rewrite in-place
5. Try Voice Instruction Refine: "make this sound more like a Forbes op-ed"
6. Watch it rewrite again

No narration needed. Text on screen: *"Grammarly: $12/month. This: included."*

---

**Video 5 — "The $0/month AI dictation setup" (local mode tutorial, extended)**
*Format: 8–10 minutes, tutorial*
*Audience: r/selfhosted, r/LocalLLaMA, privacy-focused, cost-conscious*

The deeper version of Video 2 — goes further into model selection, performance tradeoffs, and privacy verification.

Structure:
1. Why local-first (2 min) — privacy argument, cost argument, latency argument
2. Hardware requirements and expectations (1 min) — GPU vs CPU, what to expect without a GPU
3. Whisper model comparison (2 min) — base vs small vs large-v3, accuracy vs speed tradeoff, live demo of both
4. Ollama model comparison for LLM (2 min) — gemma3:4b vs llama3.1:8b, what makes a good dictation LLM
5. Kokoro TTS voices (1 min) — play a few voice styles
6. Privacy verification: network monitor showing zero outbound during dictation (1 min)

YouTube SEO title: *"Zero-cost AI dictation on Windows — Whisper + Ollama + Kokoro full setup 2026"*

---

**Video 6 — "Voice notes to Obsidian in 30 seconds" (niche, high-share)**
*Format: 60–90 seconds, no narration*
*Audience: Obsidian community, r/ObsidianMD, PKM crowd*

1. Show Obsidian vault open
2. Open dIKta.me settings, show Note file path pointed at a vault .md file
3. Close settings
4. `Ctrl+Alt+N`, speak a note, release
5. Switch to Obsidian — note is there, timestamped, formatted

End card: *"Voice → Obsidian. One hotkey. No plugin required."*

This one is specifically designed for organic sharing in the Obsidian community. Short, zero friction, does exactly what that audience wants. Post to r/ObsidianMD and the Obsidian Discord.

---

**Video 7 — "The Ghost Mode demo" (privacy audience)**
*Format: 2–3 minutes, calm, methodical*
*Audience: r/privacy, legal/medical professionals, security-conscious*

1. Open Settings → Privacy, explain the four logging levels (30 sec)
2. Enable Ghost Mode (10 sec)
3. Open the logs folder — show it's empty
4. Dictate a full paragraph using cloud mode
5. Return to logs folder — still empty
6. Open Task Manager → network tab — show the single outbound call to the API, then nothing
7. Enable local mode — repeat steps 4–6 — truly zero network activity

No hype. Just evidence. Methodical narration: *"Here's what gets logged. Here's what doesn't. Here's the proof."*

---

**Video 8 — "The developer workflow" (developer-specific)**
*Format: 4–5 minutes, VS Code visible throughout*
*Audience: developers, r/programming, r/LocalLLaMA*

A realistic developer session:
1. Set up a "C# XML Doc Comments" dictation preset (30 sec)
2. Dictate a function summary — XML comment appears
3. Highlight a confusing piece of code — `Ctrl+Alt+R` Voice Instruction: "explain what this does in plain English" — explanation appears as a comment
4. `Ctrl+Alt+A`: "give me a regex that matches ISO 8601 dates" — answer in clipboard, paste it
5. Quick Chat with clipboard attach: copy a stack trace, ask "what's causing this" — answer in overlay
6. Voice macro: say the trigger, boilerplate expands
7. End: *"All of that without leaving VS Code."*

---

**Video 9 — "Bilingual workflow demo" (EN/ES)**
*Format: 2–3 minutes, bilingual narration (Eduardo speaks)*
*Audience: LinkedIn ES, bilingual professionals, MX/LATAM market*

This is the only video where Eduardo's voice and bilingual fluency is the product feature. He switches between English and Spanish naturally, showing:
1. Reply to an English email in Spanish: `Ctrl+Alt+T`, speak English, Spanish appears
2. Switch back: speak Spanish, English appears
3. Quick Chat in Spanish: model responds in Spanish
4. Note in Spanish: appended to markdown file

The authenticity of a Mexican builder, demonstrating the bilingual workflow he actually uses, is more powerful than any scripted demo. This one is the LATAM launch asset.

**[ES NARRATION — Eduardo records this one himself, in his natural voice, no script]**

---

**Video 10 — "Shorts / Reels batch" (social clips)**
*Format: 15–30 seconds each, vertical crop for Reels/Shorts*
*Source: clip from longer videos*

Eight clips to extract and crop vertical from existing recordings:

| Clip | Source | Hook line |
|------|--------|-----------|
| Oops recovery | Video 1 or standalone | *"Dictated into the wrong window. Here's the fix."* |
| Spotify ducking | Video 8 or standalone | *"It knows when you're talking."* |
| Vision → Table | Video 1 | *"Screenshot → spreadsheet. 10 seconds."* |
| Ghost Mode toggle | Video 7 | *"Nothing written to disk. Not even token counts."* |
| Macro expansion | Video 8 | *"Said three words. Got the whole template."* |
| Translate EN→ES | Video 9 | *"Spoke English. Sent Spanish."* |
| Refine rewrite | Video 4 | *"Rough draft. One hotkey. Done."* |
| Ask → clipboard | Video 8 | *"Asked the AI a question. Didn't leave my IDE."* |

These feed Instagram Reels, YouTube Shorts, TikTok (if relevant), and X. Batch-produce from existing footage — no extra recording needed.

---

### Additional marketing angles not yet in the plan

**The "16 custom prompts" angle**
You have 8 modes × 2 profiles (cloud/local) = 16 distinct AI behaviors configured per-user. That's not a setting — that's a personalized AI workflow stack. Nobody else offers this level of per-mode prompt control in a desktop dictation app. Angle: *"16 AI behaviors. Configured by you. One hotkey each."*

**The "trailing space per preset" detail**
The fact that you can configure whether a trailing space is added *per preset* is a signal of genuine attention to craft. Terminal commands need no trailing space. Prose does. This detail is invisible until you need it — then it's the reason you trust the product. Use it in a "details that matter" LinkedIn post.

**The "auto-titles your conversations" detail**
Quick Chat auto-titles after your second message. Small thing. Nobody else mentions it. It's a thoughtfulness signal — the app is taking care of your history without asking you to. Worth a tweet.

**The convergence pricing post (specific math)**
This exists in the manifesto but deserves its own standalone LinkedIn post with a table:

| Tool | What it does | Monthly cost |
|------|-------------|-------------|
| Wispr Flow | Dictation | $12 |
| Grammarly Pro | Grammar | $12 |
| Otter.ai | Meeting notes | $10 |
| ChatGPT Plus | AI chat | $20 |
| Granola | Meeting AI | $14 |
| Screenshot AI tool | Vision | $10 |
| **Total** | | **$78/month** |
| **dIKta.me** | **All of the above** | **$20 once** |

**The "build log" series**
Eduardo's non-engineer background is a story, not a liability. 20 years of business experience + 10 weeks of C# + AI coding tools = a product built by someone who deeply understands the workflow problem. The build log series (weekly, LinkedIn + blog) is both marketing and community-building. Topics:
- "I'm not a software engineer. I built this anyway."
- "What 20 years of consulting taught me about software product design"
- "The C# mistake that cost me 3 days (and what I learned)"
- "Why I chose local-first when cloud would have been easier"
- "What shipping alone feels like"

These posts build the audience that buys V2.1. Each one earns trust independently of the product.

---

### Angles by audience (from the features)

**For developers** (r/LocalLLaMA, r/programming, HN):
- Ask pipeline output modes — "always outputs JSON" custom prompt
- Voice Macro for git commands, code snippets, boilerplate
- Quick Chat with clipboard attach for debugging
- Local-only mode: no voice data to any server
- Refine Voice: "explain this code" on selected text

**For writers / knowledge workers** (r/productivity, LinkedIn):
- Refine autopilot as Grammarly replacement
- Dictate → Refine Voice: draft fast, polish fast, never leave the document
- Ask clipboard toast: research without context-switching
- Translate for international communication
- Note → markdown file: voice journal that never requires a separate app

**For privacy-conscious users** (r/selfhosted, r/privacy):
- Full local stack: Whisper + Ollama + Kokoro
- Zero telemetry, DPAPI key encryption
- 4-level PII scrubber (Level 3 strips all identifiers before LLM)
- Network monitor demo: flat line during local dictation
- "Your voice never leaves your machine unless you choose"

**For bilingual professionals** (LinkedIn ES, specific targeting):
- Translate mode: auto-detects source, one hotkey
- Customizable target language via system prompt (not a dropdown)
- "Translate to formal Japanese" — any language pair, not just EN↔ES
- Full UI in English + Spanish

**For accessibility / RSI users**:
- Dictation as primary input — keyboard optional
- Read Selection for proofreading without re-reading
- Voice Macros eliminate repetitive typing entirely
- Push-to-talk vs always-on: user controls the activation model

---

### One-liners mined from the features

> *"It copies your selection, sends it to AI, and puts the rewritten version back. Works in every app. Doesn't care about the app."* — Refine

> *"The AI doesn't answer in a chat window. It answers in your clipboard."* — Ask

> *"You dictated into Slack by accident. Undo it there, click where you meant, press one key. Done."* — Oops

> *"You said 'insert signature.' 3 lines of formatted text appeared. That's a macro."* — Voice Macros

> *"Screenshot. Draw a box. 'Table.' Paste into Excel."* — Vision Table

> *"It recorded your screen for 90 seconds and wrote the documentation for what it watched."* — Video → Document

> *"The Chat window remembers the conversation. Auto-titles it after your second message. Saves it locally."* — Quick Chat

> *"Level 3 privacy scrubs your name, company, location, and email before anything reaches the LLM."* — PII scrubber

---

## 8. Feature Post Library

> Ready-to-use or lightly-edited posts. Not tied to launch week — these are the ongoing content engine for weeks 2–12.
> Each post stands alone. No setup required from prior posts.
> Eduardo reviews and personalizes where marked **[PERSONALIZE]**.
> **[ES VERSION NEEDED]** marks posts that should be translated for LinkedIn ES and X/Twitter ES audiences.

---

### OOPS — "Wrong window" posts

**LinkedIn**

> **I solved the most annoying thing about AI dictation.**
>
> You know the moment. You dictate a paragraph. The AI processes it. The text appears — in the wrong window. In Slack. In a chat you didn't mean to type in.
>
> Now you have to undo it there, re-dictate everything, hope the AI formats it the same way again.
>
> dIKta.me has a hotkey for this: `Ctrl+Alt+V`.
>
> The text injector remembers exactly what it last injected. Undo it where it landed. Click where it should have gone. Press the hotkey. It re-injects — same text, same formatting, zero re-recording.
>
> It's called Oops. It does exactly what the name says.
>
> **[PERSONALIZE]** — add a specific moment where this happened to you during development or testing.
>
> dikta.me

**X / Twitter**

> The most relatable dictation problem:
>
> You speak. AI processes. Text appears — in Slack. Not your doc.
>
> dIKta.me fix: Ctrl+Z in Slack, click where you meant, press Ctrl+Alt+V.
>
> It remembers the last injection. Re-injects it. Exactly.
>
> Called "Oops." Does what it says.
>
> dikta.me

**Reddit (r/productivity)**

> **Title:** My dictation app has a re-injection hotkey and I use it constantly
>
> I've been using AI dictation for about a year. The thing that drove me crazy wasn't accuracy — it was cursor position. You dictate, the AI takes 800ms to process, and in that time you click somewhere else. The text lands in the wrong place.
>
> dIKta.me has a feature called Oops (`Ctrl+Alt+V`). The injector stores the last piece of text it produced. If it lands wrong, you undo it there, click the right spot, press the hotkey. Re-injects identically.
>
> Sounds small. It's not. I use it multiple times a day.
>
> The app is dikta.me if anyone wants to check it out. Free to try with cloud credits.

---

### REFINE — "Grammarly killer" posts

**LinkedIn**

> **I stopped paying for Grammarly. Here's what replaced it.**
>
> Grammarly works where it has a native integration — about 60% of apps if you're lucky. It doesn't work in your terminal. It doesn't work in most internal tools. It doesn't work in anything custom or niche.
>
> dIKta.me's Refine mode works everywhere. It uses `Ctrl+C` to grab whatever you've highlighted, sends it through the LLM with your system prompt, and puts the rewritten version back. Works in 100% of Windows apps. If `Ctrl+C` works, Refine works.
>
> My Refine prompt: *"Fix all grammar and clarity errors. Keep the original tone. Don't change the meaning."*
>
> I highlight. I press `Ctrl+Alt+R`. It's done.
>
> Voice Instruction mode goes further: hold the hotkey, say "make this sound more direct" or "cut this in half" or "translate to Spanish" — and it does that instead.
>
> $12/month for Grammarly. $20 once for everything.
>
> **[PERSONALIZE]** — mention a specific writing context (emails, docs, Slack, code comments).
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> Grammarly works in ~60% of apps.
>
> dIKta.me Refine works in 100%.
>
> If Ctrl+C works in your app, Refine works. Highlight → hotkey → rewritten in-place.
>
> Or hold the hotkey, say "make this more direct." Does that instead.
>
> $12/mo vs $20 once.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

**Reddit (r/productivity)**

> **Title:** I replaced Grammarly with a hotkey that works in every app
>
> Grammarly's biggest problem is coverage — it works natively in Chrome, Word, and a few others. Anything custom, internal, or niche and you're on your own.
>
> dIKta.me Refine mode doesn't care what app you're in. It grabs your selection with Ctrl+C, runs it through an LLM with a system prompt you configure, and puts the result back with Ctrl+V. Works anywhere Ctrl+C works — which is basically everywhere on Windows.
>
> There's also Voice Instruction mode: hold the hotkey and say what you want it to do. "Translate to Spanish." "Make this three bullet points." "Rewrite this for a non-technical audience." The instruction changes what the LLM does with the text.
>
> Replaced Grammarly for me. Not for everyone — if you need the browser extension suggestions inline as you type, this isn't that. But for post-writing cleanup, it covers everything.

---

### VISION TABLE — "Screenshot to spreadsheet" posts

**LinkedIn**

> **The workflow that saves me 20 minutes a week.**
>
> You've seen data trapped in a web page. A table you can't select. A PDF where the copy comes out garbled. A screenshot from a client with numbers you need in a spreadsheet.
>
> The old workflow: screenshot → retype manually → pray you didn't miss a cell.
>
> dIKta.me Vision: `Ctrl+Alt+S` → draw a box around the table → click Table → paste into Excel.
>
> It extracts the structure as tab-separated values. Rows, columns, headers. Ready to paste.
>
> Takes about 10 seconds.
>
> **[PERSONALIZE]** — name a specific type of table you deal with: pricing tables, project timelines, financial data, etc.
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> Data trapped in a screenshot.
>
> Ctrl+Alt+S → draw a box → Table → paste into Excel.
>
> Tab-separated values. Rows, columns, headers. ~10 seconds.
>
> dIKta.me Vision mode.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

**Reddit (r/productivity)**

> **Title:** dIKta.me's Vision mode extracts tables from screenshots directly into spreadsheet format
>
> I deal with a lot of data that lives in PDFs and web pages where copy-paste produces garbage. Spent a while manually retyping tables.
>
> dIKta.me has a Vision mode (`Ctrl+Alt+S`) that lets you draw a region on your screen and choose what to do with it. One of the options is "Table" — it sends the image to AI and gets back tab-separated values, which paste cleanly into Excel or Sheets.
>
> Works on most reasonably clean tables. Not magic, but better than retyping. Runs locally on Ollama (minicpm-v model) or via Gemini in cloud mode.
>
> Free to try at dikta.me.

---

### AUDIO DUCKING — "It knows when you're talking" posts

**LinkedIn**

> **A small feature that signals a lot about how much a product cares.**
>
> Audio ducking: when you start dictating, dIKta.me automatically fades down whatever else is playing — Spotify, YouTube, background audio. When you stop, it fades back up.
>
> You can set the attenuation level (how much it fades) and the fade duration (how smooth the transition is). 100% attenuation = complete mute. 20% = barely noticeable dip. The fade can be a hard cut or a smooth crossfade.
>
> Nobody asks for this feature. Nobody puts it in a list of requirements. But once you've used it for a week, you notice every dictation tool that doesn't have it.
>
> It's the kind of detail that tells you the developer actually uses the product.
>
> dikta.me

**X / Twitter**

> dIKta.me auto-fades Spotify when you dictate. Fades it back when you stop.
>
> Configurable attenuation. Configurable crossfade duration.
>
> Nobody asks for this feature. Nobody stops noticing it once they have it.
>
> dikta.me

**Reddit (r/selfhosted, r/productivity)**

> **Title:** The audio ducking in dIKta.me is the kind of detail that tells you the developer uses their own product
>
> Small thing: when you press the dictation hotkey, the app automatically lowers the volume of other applications. When you release, they fade back. You configure how much (attenuation %) and how smoothly (fade duration).
>
> I know this is a minor feature. But it's the feature that tells you a product was made by someone who actually uses it every day, not just built to a spec. The developer clearly dictates with music on.
>
> dIKta.me is dikta.me. Free trial, $20 one-time for local mode.

---

### GHOST MODE — "Nothing written to disk" posts

**LinkedIn**

> **Four privacy levels. The last one is called Ghost Mode.**
>
> Most apps claim to be "privacy-first." Usually that means a toggle that changes where data is sent.
>
> dIKta.me has four logging levels:
>
> 1. Full logging — everything, for debugging
> 2. Balanced — strips PII before writing to disk
> 3. Stats only — timestamps and token counts, no text
> 4. Ghost Mode — nothing. No logs. No token counts. No latency records. Not even error messages.
>
> In Ghost Mode, the application operates completely namelessly. If something goes wrong, there's no log to debug from. That's the tradeoff — and it's documented honestly.
>
> For legal professionals, medical dictation, executive communications — this isn't a nice-to-have. It's the requirement.
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> dIKta.me privacy levels:
>
> 1. Full logging
> 2. Balanced (PII stripped)
> 3. Stats only
> 4. Ghost Mode — nothing written to disk. Not even error messages.
>
> The tradeoff: if something breaks in Ghost Mode, there's no log to debug from. Documented honestly.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

**Reddit (r/privacy, r/selfhosted)**

> **Title:** dIKta.me has a "Ghost Mode" that writes absolutely nothing to disk — not even token counts
>
> Privacy levels in dIKta.me:
>
> - Full logging: captures everything including transcriptions and LLM output
> - Balanced: runs a local PII scrubber before writing (strips emails, SSNs, phone numbers, credit card patterns)
> - Stats only: timestamps, error codes, token counts — no text
> - Ghost Mode: nothing. Zero. The logs folder stays empty.
>
> The docs are honest about the tradeoff: if you encounter a bug in Ghost Mode, there's no stack trace to help debug it.
>
> Combined with local mode (Whisper + Ollama, no internet), Ghost Mode means your voice never leaves your machine and nothing is written to disk. That's about as private as a desktop AI tool gets.
>
> Code is MIT-licensed and on GitHub if you want to verify the claims.

---

### MACROS — "AI-proof templates" posts

**LinkedIn**

> **The dictation feature nobody talks about: post-LLM macro expansion.**
>
> dIKta.me has a macro engine. You define a trigger phrase and a replacement block. When you say the trigger in dictation, the macro expands after the LLM finishes processing.
>
> That last part matters: *after* the LLM.
>
> The LLM can't reword, hallucinate, or reformat your macro content. If your trigger maps to a legal disclaimer, a precise URL, or a formatted signature block — it arrives verbatim. Every time. The AI touched everything else. Your template is untouched.
>
> Use cases I've seen: email signatures, code snippets, legal boilerplate, meeting note templates, git commands, API endpoints.
>
> **[PERSONALIZE]** — add one macro you personally use.
>
> dikta.me

**X / Twitter**

> dIKta.me macros expand AFTER the LLM finishes.
>
> The AI formats your words. Your template expands last. Verbatim. Every time.
>
> Legal disclaimers, signatures, code snippets — untouched by AI.
>
> Say the trigger. Get the template.
>
> dikta.me

**Reddit (r/productivity, r/LocalLLaMA)**

> **Title:** dIKta.me's macro expansion happens post-LLM — your templates are AI-proof
>
> Most text expanders run before any AI processing. The risk: the LLM might reword your macro content as part of formatting the surrounding text.
>
> dIKta.me macros run after the LLM pipeline finishes. The LLM formats your dictated words. Then the macro engine does a final sweep and expands your triggers. Verbatim replacement, no AI involved.
>
> Practical impact: your email signature, your legal disclaimer, your boilerplate template — exactly as you defined them, every time, regardless of what model you're using or how the surrounding text was processed.
>
> Small architectural detail, meaningful if you rely on precise templates.

---

### TRANSLATE (any language) posts

**LinkedIn**

> **The translation mode isn't EN↔ES. It's any language pair.**
>
> dIKta.me's Translate mode is driven by a system prompt, not a language dropdown.
>
> Default prompt: "Translate to Spanish." But you can set it to anything:
> - "Translate to formal Japanese."
> - "Translate to Brazilian Portuguese, keeping technical terms in English."
> - "Translate to French and maintain a formal register."
> - "Translate to simplified Mandarin."
>
> One hotkey. `Ctrl+Alt+T`. Speak your source language. Get the target language at your cursor.
>
> The language detection is automatic — you don't set the source. Speak in whatever language you're thinking in.
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> dIKta.me Translate isn't a dropdown.
>
> It's a system prompt. Set it to anything:
> "Translate to formal Japanese."
> "Translate to Brazilian Portuguese."
> "Keep technical terms in English."
>
> One hotkey. Any language pair. Auto-detects your source.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### NOTE → OBSIDIAN posts

**LinkedIn**

> **Voice notes going directly into your Obsidian vault.**
>
> dIKta.me's Note mode (`Ctrl+Alt+N`) appends timestamped notes to a markdown file. The file path is configurable — you can point it anywhere.
>
> I pointed mine at a file inside my Obsidian vault.
>
> Now when I'm mid-focus and a thought hits: `Ctrl+Alt+N`, speak it, release. It's in the vault. Timestamped. Formatted by the LLM (or raw transcription if I want speed).
>
> No tab switch. No context loss. No "I'll write that down later."
>
> The Obsidian connector (V2.1) will go deeper — tags, folders, backlinks. But for voice → vault, this works right now.
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> dIKta.me Note mode: point the file path at your Obsidian vault.
>
> Ctrl+Alt+N → speak → timestamped entry in your vault.
>
> No tab switch. No plugin. No friction.
>
> V2.1 will go deeper. This works today.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

**Reddit (r/ObsidianMD)**

> **Title:** Voice notes into Obsidian from any app — no plugin, just a file path
>
> dIKta.me has a Note pipeline (`Ctrl+Alt+N`) that appends timestamped voice notes to a markdown file. The file path is a free-form setting — you can point it at any `.md` file, including one inside your Obsidian vault.
>
> I set mine to `C:\Users\...\ObsidianVault\Inbox\voice-notes.md`. Now I can capture a thought mid-focus without switching away from whatever I'm doing. Note lands in the vault, timestamped, formatted.
>
> Not a full integration — no tags, no folder routing, no graph links. But for capturing and reviewing later, it works well.
>
> The app is dikta.me. Free to try with a small cloud credit. Local mode available ($20 one-time) if you want it fully offline.

---

### VIDEO → DOCUMENT posts

**LinkedIn**

> **I recorded my workflow once. I got the documentation automatically.**
>
> dIKta.me has a screen recording mode with a post-capture "Document" action.
>
> You record up to 120 seconds of yourself doing a task. Stop. Click Document. Gemini watches the recording and writes step-by-step instructions for what it observed.
>
> Not perfect. But for creating first-draft SOPs, training guides, or onboarding documentation — it's a starting point that takes 2 minutes instead of 45.
>
> **[PERSONALIZE]** — mention the type of process documentation you create: onboarding flows, client handoffs, internal procedures, etc.
>
> I'm aware this is a feature that sells itself to ops people, team leads, and L&D professionals who have never heard of dIKta.me. That's exactly who I'm talking to with this post.
>
> dikta.me
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> dIKta.me: record your screen for 90 seconds → click "Document" → Gemini writes step-by-step instructions for what it watched.
>
> First-draft SOP in 2 minutes.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### CUSTOM DICTATION PRESETS posts

**LinkedIn**

> **16 AI behaviors. One hotkey each.**
>
> dIKta.me has a Dictation Modes system. You create presets — each one a named configuration with its own system prompt for cloud models and a separate optimized prompt for local models.
>
> Switch between them from the Control Panel dropdown while you work.
>
> Some presets I'd configure:
> - "C# XML Doc Comments" — formats spoken descriptions as XML documentation
> - "Casual Slack" — conversational, no punctuation formality
> - "Client Email" — formal, full punctuation, professional register
> - "Meeting Notes" — bullet-pointed, action items flagged
> - "Spanish Email" — translates and writes in Spanish directly
>
> Each preset has a cloud prompt (complex, detailed instruction for GPT/Claude/Gemini) and a local prompt (concise, direct instruction for Ollama — smaller models perform better with shorter prompts).
>
> You configure it once. You use it forever.
>
> **[PERSONALIZE]** — list 2-3 real presets you actually use.
>
> **[ES VERSION NEEDED]**

**X / Twitter**

> dIKta.me presets:
>
> "C# XML Comments" — speaks function descriptions, gets XML docs
> "Client Email" — formal register, full punctuation
> "Meeting Notes" — auto-bullets, flags action items
> "Spanish Email" — writes in Spanish directly
>
> One dropdown. Switch mid-session.
>
> dikta.me

**[ES VERSION NEEDED]** for X/Twitter

---

### BUILD LOG posts (Eduardo's voice — personal, ongoing series)

> These are written in first person as Eduardo. They build the audience that buys V2.1.
> **[PERSONALIZE]** throughout — these are outlines, not finished posts. Eduardo fills in the real details.

---

**Build log #1 — "I'm not a software engineer. I built this anyway."**

**LinkedIn**

> **I have 20 years of experience in consulting, digital media, and project management. I have never worked as a software engineer.**
>
> dIKta.me V2.0 is my first desktop application. Written in C# and WinUI 3. 1,134 unit tests. A CI/CD pipeline. A payment system. A local AI stack that runs Whisper, Ollama, and Kokoro on consumer hardware.
>
> I built it in about 10 weeks, with AI coding assistance, because I was frustrated enough with the available tools to try.
>
> The product decisions come from two decades of building businesses and understanding what knowledge workers actually need. The code was written by someone learning while doing — with AI as a pair programmer.
>
> I'm not sure what that means for the product. I think it means the features are right even where the implementation could be better. I'm willing to live with that tradeoff.
>
> The app is free to try at dikta.me. I read every piece of feedback.
>
> **[PERSONALIZE]** — add one specific moment from the build that was difficult or surprising.

---

**Build log #2 — "Why I chose local-first when cloud would have been easier"**

**LinkedIn**

> **Cloud-only would have been simpler to build. I chose local-first anyway.**
>
> The cloud version of dIKta.me is straightforward: record audio, send to Deepgram, send transcript to Gemini, inject result. Three API calls. Done.
>
> The local version means bundling Whisper.net, managing ONNX runtimes, handling GPU acceleration across NVIDIA and AMD via Vulkan, managing Ollama's HTTP keep-alive, solving a provider caching problem that was adding 2500ms of latency every call.
>
> I chose it because of the users I was building for. Privacy-conscious professionals don't send voice recordings to third-party servers — period. That's not a preference, it's a requirement. If I built cloud-only, I was building for a different audience than the one I wanted to serve.
>
> Local-first also means $0/month running cost after setup. That changes the entire economic argument.
>
> **[PERSONALIZE]** — add one specific technical problem from the local implementation that was unexpectedly hard.

---

**Build log #3 — "The 2,500 millisecond problem"**

**LinkedIn (technical, developer audience)**

> **Every dictation call was adding 2,500ms of latency I couldn't explain.**
>
> The pipeline: Whisper transcribes audio (~800ms). LLM refines the text (~600ms). Text injects at cursor. Total should be ~1.5 seconds.
>
> I was seeing 3+ seconds consistently. Sometimes 4. The pipeline metrics showed the latency appearing before the LLM call even started.
>
> After a lot of digging: I was creating a new `HttpClient` for every dictation call. Every call was establishing a new TCP connection to Ollama. TCP handshake + connection setup = ~2,500ms on first call.
>
> Fix: `ConcurrentDictionary<string, ILLMProvider>` cache keyed by `"{type}:{model}"`. GetOrAdd pattern. The first call pays the connection cost. Every subsequent call reuses the connection.
>
> Latency dropped from ~3,000ms to ~550ms for the LLM step.
>
> The lesson isn't "cache your providers." The lesson is that performance problems in dictation software are felt in a way that performance problems in web apps aren't. 2,500ms in a UI is annoying. 2,500ms between speaking and seeing your words appear is unbearable.
>
> **[PERSONALIZE]** — add how you discovered the root cause (profiler? timing logs? intuition?).

---

**Build log #4 — "What shipping alone feels like"**

**LinkedIn**

> **There's no one to ask if the product is good enough to ship.**
>
> With a team, someone else has opinions. Product says yes or no. QA finds the last bug. Someone else decides when "done" is.
>
> Solo, you're the one who decides.
>
> **[PERSONALIZE]** — this is the most personal post. Write the real version. What did the last week before shipping feel like? What did you fix that you didn't need to? What did you leave that you wanted to fix? What did it feel like to push v2.0.0?
>
> The audience for this post is other indie builders. They will feel it. They will share it.

---

## 9. SEO Blog Outlines (4)

> Outlines only. Full posts to be written separately.
> All posts link to dikta.me and cross-link each other.
> Spanish versions: use same structure, translate titles and section headers. Separate URL slugs (`/es/blog/...`). Eduardo to review keyword targeting for MX/LATAM search intent.

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
10. **Conclusion + CTA** → dikta.me Full Version (local mode)

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
11. **Conclusion + CTA** → dikta.me Full Version

---

---

*LAUNCH_CONTENT.md — dIKta.me V2.0*
*Generated: March 2026*
*Brand voice: BRAND_BOOK.md · Facts: RELEASE_ROADMAP.md*
