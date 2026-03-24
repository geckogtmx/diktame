# 🤖 Índice para Agentes (Columna Vertebral de Documentación)

> **Contexto para Agentes de IA y LLMs:** Este documento sirve como el mapa maestro de la documentación de dIKta.me. Usa esta columna para localizar información relevante rápidamente sin tener que atravesar todo el árbol de directorios de manera secuencial.

## 📍 Navegación Rápida

### 1. 📖 Guías de Usuario y Conceptos Centrales
Ubicado en `website/content/{locale}/docs/`
- `index.md` - Documentación de inicio y presentación de alto nivel.
- `getting-started.md` - Instalación, configuración y experiencia de primer uso.
- `troubleshooting.md` - Problemas comunes, códigos de error y soluciones.
- `settings.md` - Página raíz para todas las configuraciones de la aplicación.

### 2. ✨ Detalles de Características
Ubicados en `website/content/{locale}/docs/`
- `ask.md` - Consultas LLM activadas por voz.
- `translate.md` - Modos de traducción de texto.
- `refine.md` - Pulido y formato de texto.
- `note.md` - Modo de toma de notas en segundo plano.
- `quick-chat.md` - Ventana de chat superpuesta para interacciones LLM fluidas.
- `Macros.md` - Expansiones de texto personalizadas y uso de plantillas.
- `tts.md` - Reproducción de Texto a Voz de las respuestas generadas.
- `oops.md` - Pila de formato e historial para Deshacer/Rehacer.

### 3. ⚙️ Referencia de Ajustes
Ubicados en `website/content/{locale}/docs/settings/`
- Cada archivo se mapea directamente a una pestaña en la UI de Ajustes.
- Archivos clave incluyen `api-keys.md` (Integración BYOK), `audio.md` (Dispositivos y atenuación ambiental), `general.md`, `hotkeys.md`, `modes.md`, `ollama.md`, y `privacy.md`.

### 4. 🛠️ Hub de Desarrolladores y Arquitectura 
Ubicados en `website/content/{locale}/docs/dev/`
- `index.md` - Página de presentación para el desarrollador.
- `setup.md` - Entorno de trabajo (.NET 8, WinUI 3, etc.).
- **Arquitectura (`dev/architecture/`):**
  - `ui-mvvm.md` - Reglas e inyección estricta MVVM con `CommunityToolkit.Mvvm`.
  - `audio-pipeline.md` - Captura en memoria NAudio y búferes para optimización.
  - `threat-model.md` - Parámetros de resguardo contra amenazas e injerencias (STRIDE).
- **Proveedores API (`dev/api/`):**
  - `stt-providers.md` - Enlaces de integración IA para Speech-to-Text.
  - `llm-providers.md` - Enlaces de integración IA para Modelos Grandes de Lenguaje.
  - `tts-providers.md` - Enlaces de integración IA para Texto a Voz.

## 🧠 Pautas para el Agente (Heurísticas)
1. **Regla de Oro:** Siempre lee cuidadosamente los patrones descritos en `dev/architecture/ui-mvvm.md` previo a editar código asociado al entramado visual de WinUI 3.
2. **Proveedores Intercambiables:** Los flujos cognitivos actúan todos bajo inyección de dependencias intercambiable en tiempo real; para integrar más modelos consulta `dev/api/`.
3. **Equivalencia de Idiomas:** Esta misma estructura es el espejo fidedigno reflejado dentro de `website/content/en` y su contraparte `website/content/es`. 

*Fin de Documento.*
