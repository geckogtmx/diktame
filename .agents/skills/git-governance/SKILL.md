---
name: git-governance
description: Enforcement of Git conventions and documentation standards for the diktame repository.
---

# Git Governance

## Mental Model
Our Git history is our **legacy**. It must be readable, searchable, and structured.
Documentation is **first-class code**. It evolves with the codebase.
We use **Trunk-Based Development**.

## Core Principles
1.  **Conventional Commits**: Standardized commit messages for changelog generation.
2.  **No Feature Branches**: Commit small, workable chunks directly to `main`.
3.  **Documentation Sync**: `README.md` and `ARCHITECTURE.md` must reflect the current state of `main`.
4.  **Task Tracking**: Every commit tracks a task ID (e.g., `[A.1]`).

## Critical Anti-Patterns
- **"WIP" Commits**: Never push broken code to main. Stash or squash locally.
- **Vague Messages**: "Fix bug" is useless. "fix(ui): resolve overflow in settings panel [B.2]" is perfect.
- **Ignoring Docs**: Changing code without updating the relevant guide is a failure.
- **Commiting Secrets**: Never commit `.env` or API keys. Use `SecureStorage`.

## Instructions
1.  **Commit Format**
    - `<type>(<scope>): <description> [<TASK_ID>]`
    - **Types**: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`.
    - **Scopes**: `audio`, `stt`, `llm`, `pipeline`, `input`, `config`, `data`, `security`, `system`, `ui`, `ci`.

2.  **Scopes**
    - `audio`: NAudio, recording, device management.
    - `stt`: Speech-to-Text providers (Deepgram, Whisper).
    - `llm`: Language Model integration.
    - `pipeline`: Orchestration logic.
    - `ui`: WinUI views and viewmodels.

3.  **Workflow**
    - Pull latest `main`.
    - Run tests locally (`dotnet test`).
    - Commit with format.
    - Push.

4.  **Governance Files**
    - `AI_CODEX.md`: The law.
    - `GEMINI.md`: AI Context.
    - `DEV_HANDOFF.md`: Session notes.
    - Do not modify these unless explicitly instructed.
