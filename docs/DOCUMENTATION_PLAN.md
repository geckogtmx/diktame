# Documentation Strategy & Plan

**Goal**: Create comprehensive, dual-audience documentation for **dIKta.me V2** (C# / WinUI 3), leveraging the foundation of V1 (Electron / Python).

## 1. Structure Overview

We will maintain a clear separation between **User Documentation** (how to *use* the app) and **Developer Documentation** (how to *build/contribute* to the app).

```
diktame/
├── docs/
│   ├── user/                  # End-user guides
│   │   ├── getting-started.md # Installation & First Run
│   │   ├── features/          # Feature deep-dives
│   │   │   ├── dictation.md
│   │   │   ├── refine.md
│   │   │   └── commands.md    # Voice commands reference
│   │   └── troubleshooting.md # Common issues & fixes
│   │
│   ├── dev/                   # Developer guides
│   │   ├── architecture/      # High-level design (mirrors ARCHITECTURE.md)
│   │   ├── setup.md           # Dev environment setup
│   │   ├── api/               # Core internal APIs
│   │   │   ├── STT-providers.md
│   │   │   ├── LLM-providers.md
│   │   │   └── pipeline.md
│   │   └── testing.md         # Running & writing tests
│   │
│   └── migration/             # V1 -> V2 transition
│       └── v1-to-v2-guide.md  # Key differences for upgraders
```

## 2. Knowledge Gathering Plan (V1 → V2)

We will port and adapt relevant sections from `e:\git\diktate\docs`.

| V1 Source (diktate) | V2 Destination (diktame) | Adaptation Strategy |
|---------------------|--------------------------|---------------------|
| `docs/user_guide` | `docs/user/` | **Heavy Rewrite**. V2 UI (WinUI 3) is completely different from V1 (Electron). Core concepts (Dictate, Refine) remain, but workflows differ. |
| `docs/developer_guide` | `docs/dev/` | **Complete Rewrite**. Python/JS specific docs are obsolete. Replace with C#/.NET 8/WinUI 3 specific guides. |
| `docs/api` | `docs/dev/api` | **Reference Only**. Use V1 API logic to inform V2 `DiktaMe.Core` documentation, but specific endpoints/classes will differ. |
| `README.md` | `README.md` | **Align**. Ensure V2 README is the single source of truth for high-level project status. |
| `ARCHITECTURE.md` | `docs/dev/architecture/` | **Expand**. The root `ARCHITECTURE.md` is excellent; expanding it into detailed sub-pages (Audio, STT, LLM) in `docs/` is the next step. |

## 3. Immediate Action Items

### Phase 1: Foundation (Current)
- [x] Create `docs/` directory structure.
- [ ] Create `docs/user/index.md` stub.
- [ ] Create `docs/dev/index.md` stub.

### Phase 2: User Documentation
- [ ] Draft `getting-started.md`: Focus on the new self-contained installer simplicity.
- [ ] Draft `features/dictation.md`: Explain the specialized "Dictate" mode vs "Refine" mode.
- [ ] Draft `troubleshooting.md`: Common Windows-specific issues (Scaling, Permissions).

### Phase 3: Developer Documentation
- [ ] Draft `setup.md`: `dotnet workload install` steps, VS 2022 recommended config.
- [ ] Document `DiktaMe.Core` interfaces (`ISTTProvider`, `ILLMProvider`) as these are the extension points.
- [ ] Document the `Pipeline` architecture for contributors.

## 4. Tools & Standards
- **Format**: Markdown (`.md`).
- **Style**: Clear, concise, active voice.
- **Diagrams**: Mermaid.js for flows and architecture.
- **Images**: Screenshots of the new WinUI 3 interface (stored in `docs/assets/`).

---
*Plan created: 2026-02-15*
