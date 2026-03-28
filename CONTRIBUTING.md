# Contributing to dIKta.me

Thanks for your interest.

## The honest situation

I'm a solo developer. I built this in ~10 weeks and I'm maintaining it on my own time. I don't currently have the bandwidth to review pull requests.

## What you can do

- **Found a bug?** File an issue. I read every one.
- **Have a feature idea?** Open a discussion. I want to hear it.
- **Want to fork and build?** Go for it — it's MIT. No permission needed.

## What I can't promise

- PR reviews or merges on any timeline
- Feature requests being implemented
- Responses faster than "when I get to it"

## If you're genuinely good

If you're a developer who wants to contribute meaningfully and consistently — reach out directly. I'm open to finding the right person to help manage the community side of things. But I'd rather be honest about my bandwidth than make promises I can't keep.

## Tech Stack

- **Language**: C# 12 (.NET 8)
- **UI Framework**: WinUI 3 (Windows App SDK 1.6)
- **Testing**: xUnit + Moq + FluentAssertions
- **Architecture**: MVVM (CommunityToolkit.Mvvm)

## Quick Start

```bash
git clone https://github.com/geckogtmx/diktame.git
dotnet build DiktaMe.sln
dotnet test DiktaMe.sln
```

## Project Structure

- `src/DiktaMe.App` — UI (Views, ViewModels, XAML)
- `src/DiktaMe.Core` — Business logic (Pipelines, Providers, Services)
- `tests/DiktaMe.Core.Tests` — xUnit tests (1,014 and counting)

## Code Standards

- Follow existing patterns (`.editorconfig` is included)
- Trunk-based: small, frequent commits to `main`
- Conventional commits with `[TASK_ID]` suffix
- All new business logic in `DiktaMe.Core` must have unit tests
- No telemetry, no external data persistence — privacy is non-negotiable

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
