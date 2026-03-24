# Documentation Screenshot Requirements

**Production Notes for Image Assets**

This document tracks all the screenshots needed to complete the dIKta.me V2 User Documentation.
**Note:** All images should be saved in `docs/assets/` using the exact filenames listed below in `.png` format.

## General Guidelines
- Please capture screenshots using the standard Windows scaling (preferably 100% or 125% for sharpness).
- Whenever possible, crop out unrelated desktop background elements.
- For UI windows, capture the active window entirely (e.g., `Alt + PrintScreen` or Snipping Tool -> Window Mode).

---

## 1. Getting Started (`docs/user/getting-started.md`)
| Filename | Description |
| :--- | :--- |
| `wizard-welcome.png` | The initial screen of the Onboarding Wizard (`WizardWindow.xaml`). |
| `wizard-providers.png` | The Wizard step showing STT and LLM provider selection. |
| `wizard-complete.png` | The final completion screen of the Wizard. |
| `system-tray-icon.png` | A cropped view of the dIKta.me icon in the Windows systemic tray menu. |
| `system-tray-menu.png` | The right-click context menu expanded from the system tray icon. |

---

## 2. Core Features
### Dictation (`docs/user/features/dictation.md`)
| Filename | Description |
| :--- | :--- |
| `control-panel-collapsed.png` | The main `ControlPanelPage.xaml` in its default, compact "HUD" state. |
| `control-panel-expanded.png` | The Control Panel expanded, showing the mode rows and volume visualizer. |
| `control-panel-recording.png` | The Control Panel actively recording or transcribing (showing the Stop/Progress state). |

### Quick Chat (`docs/user/features/quick-chat.md`)
| Filename | Description |
| :--- | :--- |
| `quick-chat-window.png` | The overlay chat window (`QuickChatWindow.xaml`) with an example conversation shown. |

### Utilities (Ask, Refine, Translate, Note)
| Filename | Description |
| :--- | :--- |
| `refine-demo.png` | A before/after or split view demonstrating the Refine feature (if applicable), or simply the relevant toast notification showing "Refined". |
| `ask-toast.png` | A Windows toast notification demonstrating the Ask pipeline completion. |

---

## 3. The 12 Settings Tabs (`docs/user/settings/*`)
*Capture these with `SettingsWindow.xaml` open, selecting the respective tab.*

| Filename | Tab | Description |
| :--- | :--- | :--- |
| `settings-general.png` | General | Showing Language, AutoStart, and Ask Output modes. |
| `settings-account.png` | Account | Showing Wallet credit balance and Auth Mode (Trial vs Account). |
| `settings-apikeys.png` | API Keys | Showing BYOK text boxes (blurred/fake keys if desired). |
| `settings-ai-engine.png`| AI Engine| Showing the global model selection grid. |
| `settings-audio.png` | Audio | Showing the microphone device selector and audio ducking sliders. |
| `settings-dictation-modes.png`| Dictation| Showing the CRUD view for custom dictionary/dictation presets. |
| `settings-modes.png` | Modes | Showing the Utility Pipelines configuration. |
| `settings-hotkeys.png` | Hotkeys | Showing the keybindings (Dictate, Refine, Oops, etc.). |
| `settings-Macros.png` | Macros | Showing the text-expansion / find-and-replace grid. |
| `settings-ollama.png` | Ollama | Showing local engine configuration and model downloading lists. |
| `settings-privacy.png` | Privacy | Showing the Privacy Level slider (Ghost/Stats/Balanced/Full). |
| `settings-control-panel.png`| HUD | Showing the visibility toggles for the main control panel. |

---
**Status**: [ ] Captured (0 / 23)
