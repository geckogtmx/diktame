> **Language / Idioma:** [English](README.md) | Español

# ![dIKta.me](docs/images/ReadmeHead.png)

# dIKta.me V2

**Dictado de voz a texto para Windows** — Reescritura en C# + WinUI 3.

> Anteriormente *dIKtate*. Reescritura completa de Python + Electron a aplicación nativa de Windows.

![dIKta.me — Configuración y Panel de Control](docs/images/app-overview.jpeg)
>
> 📚 **Documentación**: [Guía de Usuario](https://dikta.me/es/docs) • [Guía de Desarrollo](https://dikta.me/es/docs/dev) • [Privacidad](PRIVACY.md) • [Contribuir](CONTRIBUTING.md)

## Stack Tecnológico

| Capa | Tecnología |
|------|-----------|
| **UI** | WinUI 3 (Fluent Design) |
| **Lógica** | C# / .NET 8 |
| **STT** | Nube (Deepgram, Gemini) + Local (Whisper.net con Vulkan GPU) |
| **TTS** | Nube (Deepgram, Inworld, OpenAI) + Local (Kokoro-ONNX) |
| **LLM** | Gemini, Anthropic, OpenAI, Ollama |
| **Datos** | SQLite (Microsoft.Data.Sqlite) |
| **Instalador** | Autocontenido, optimizado |

## Estructura de la Solución

```
DiktaMe.sln
├── src/
│   ├── DiktaMe.App/          # Aplicación WinUI 3 (capa UI)
│   └── DiktaMe.Core/         # Lógica de negocio (librería de clases)
│       ├── Audio/             # Grabación NAudio, gestión de dispositivos
│       ├── STT/               # Proveedores de voz a texto
│       ├── TTS/               # Proveedores de texto a voz
│       ├── LLM/               # Proveedores LLM
│       ├── Pipeline/          # Orquestación de flujos de trabajo
│       ├── Input/             # Teclas rápidas, inyección de texto
│       ├── Config/            # Configuración, perfiles, snippets
│       ├── Data/              # Historial SQLite, métricas
│       ├── Security/          # Secretos DPAPI, limpieza de PII
│       └── System/            # Gestión de Ollama
└── tests/
    └── DiktaMe.Core.Tests/    # xUnit + Moq + FluentAssertions
```

## Modos de Trabajo

| # | Modo | Tecla rápida | Descripción |
|---|------|------------|-------------|
| 1 | **Dictate** | `Ctrl+Alt+D` | Voz → Inyección de texto |
| 2 | **Refine** | `Ctrl+Alt+R` | Mejora de selección |
| 3 | **Ask** | `Ctrl+Alt+A` | Preguntas y respuestas por voz |
| 4 | **Translate** | `Ctrl+Alt+T` | Traducción bidireccional EN↔ES |
| 5 | **Oops** | `Ctrl+Alt+V` | Reinyectar último texto |
| 6 | **Note** | `Ctrl+Alt+N` | Notas de voz rápidas |
| 7 | **Read Selection** | `Ctrl+Alt+Q` | Reproducción de texto a voz |

## Desarrollo

### Requisitos Previos

- .NET 8 SDK
- Windows 10 (versión 2004+) o Windows 11
- Windows App SDK workload: `dotnet workload install maui-windows`

### Compilar y Probar

```bash
dotnet build DiktaMe.sln
dotnet test DiktaMe.sln
```

### Convenciones de Git

- Desarrollo **trunk-based** (commits directos a `main`)
- **Conventional Commits**: `feat(scope): descripción [TASK_ID]`
- Ver `DEVELOPMENT_ROADMAP.md` §9 para la estrategia completa

## Estado

**Fase del Proyecto:** Funcionalidades completas + Testing ✅

| Stream / Spec | Tareas | Estado |
|---------------|:------:|--------|
| **A** — Scaffolding | A.0–A.2 | ✅ Completo |
| **B** — Motor Principal | B.1–B.5 | ✅ Completo |
| **C** — Proveedores STT y LLM | C.1–C.7 | ✅ Completo |
| **D** — Orquestación de Pipeline | D.1–D.4 | ✅ Completo |
| **E** — Datos y Seguridad | E.0–E.3 | ✅ Completo |
| **F** — UI (WinUI 3) | F.1–F.5 | ✅ Completo |
| **G** — Testing y CI/CD | G.1–G.2 | ✅ Completo |
| **I** — Funciones Promovidas | I.1–I.5 | ✅ Completo |
| **J** — CRUD Modos de Dictado | J.1–J.7 | ✅ Completo |
| **K** — OAuth y Créditos de Prueba | K.1–K.7 | ✅ Completo |
| **L** — Deepgram Streaming | L.1–L.5 | ✅ Completo |
| **SPEC_003** — TTS | Fase A–G | ✅ Completo |
| **SPEC_007** — Mejora de Chat | 14/14 tareas | ✅ Completo |
| **SPEC_009** — Modo Local/Wizard | 15 correcciones | ✅ Completo |
| **SPEC_011** — Gestión de Ollama | API + UI | ✅ Completo |
| **H** — Distribución | H.1–H.2 | ⏳ Pendiente |

### Métricas

- **Compilación:** 0 errores, 0 advertencias (configuración Release, `TreatWarningsAsErrors=true`)
- **Tests:** 950 pasando localmente (479 en filtro CI unitario; DPAPI/Clipboard omitidos en runners)
- **Cobertura:** 74.1% líneas, 52.4% ramas (solo Core; capa UI probada manualmente)
- **CI/CD:** GitHub Actions en verde (`Lint ✓ Build ✓ Test ✓ Secret scan ✓ Publish ✓`)
- **Tamaño de publicación:** ~173MB sin comprimir (x64), ~70MB comprimido
- **Calidad de código:** Meziantou.Analyzer, NuGetAudit, gitleaks, seguimiento de cobertura

### Funcionalidades Actuales

✅ **Grabación e Inyección:** Push-to-talk con teclas globales, inyección de texto vía clipboard
✅ **Transcripción:** STT en la nube (Deepgram, Gemini) + local con Vulkan GPU (Whisper.net)
✅ **TTS (Texto a Voz):** Modo Read Selection (`Ctrl+Alt+Q`), Kokoro local + fallback en la nube
✅ **LLM:** Nube (OpenAI, Anthropic, Gemini) + Ollama local con caché de proveedores y auto-start
✅ **Los 7 Flujos de Trabajo:** Dictate, Refine, Ask, Translate, Oops, Note, Read Selection
✅ **Configuración y Perfiles:** Sistema de doble perfil, 16 prompts personalizados, proveedores por modo
✅ **Voice Snippets:** Expansión de macros por trigger (Fase 1)
✅ **Quick Chat:** Overlay flotante con LLM (entrada de texto + voz)
✅ **Gestión de Ollama:** Detección de versión, health checks, UI de librería de modelos, keep-alive
✅ **Datos:** Historial SQLite (90 días), métricas de sesión, niveles de privacidad
✅ **Seguridad:** Secretos DPAPI, limpieza de PII, validación de API keys
✅ **UI:** Ventana de configuración WinUI 3 (10 pestañas), Panel de Control, Wizard de primera ejecución, Notificaciones
✅ **Sitio Web Renovado:** Documentación completa y marketing en dikta.me

### Próximos Pasos (Sprint Modular y Distribución)

⏳ **Conectores e Integraciones:** SPEC_015 Plugins nativos para Slack, Notion, etc.
⏳ **Reuniones y Scribe:** Transcripción inteligente y notas de reuniones.
⏳ **Vision y See:** Capacidades multimodales con reconocimiento de pantalla.
⏳ **Capa de Memoria:** Persistencia contextual estratégica.
⏳ **Sistema de Wallet Unificado:** Créditos y preparación BYOK (`SPEC_008_WALLET.md`)
⏳ **Instalador:** MSIX nativo o Inno Setup (~30MB tamaño objetivo)

---

**Ver** [`DEVELOPMENT_ROADMAP.md`](DEVELOPMENT_ROADMAP.md) **para la arquitectura completa, desglose de tareas y estrategia de Git.**
