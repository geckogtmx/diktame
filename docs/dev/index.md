# dIKta.me V2 - Developer Documentation

Welcome to the official developer and contributor documentation for the dIKta.me V2 repository (`geckogtmx/diktame`).

The application is written strictly in C# relying on the .NET 8 SDK and the WinUI 3 presentation framework. This guide details the internal structural components necessary to modify the application correctly.

## 💻 Fundamentals

Start here if you want to compile the project locally or contribute to the Windows interface.
*   [Environment Setup](setup.md)
*   [UI MVVM Architecture & DI](architecture/ui-mvvm.md)
*   [Audio Pipeline Architecture](architecture/audio-pipeline.md)
*   [Threat Model & Security](architecture/threat-model.md)

## 🔌 API & Extensibility

Learn how to interact with the Dependency Injection container to securely inject new localized models or cloud REST services.
*   [Speech-to-Text Providers](api/stt-providers.md)
*   [Large Language Model Providers](api/llm-providers.md)
*   [Text-to-Speech Providers](api/tts-providers.md)
*   [Vision Pipeline & Multimodal LLM](api/vision.md)

## 👁️ Vision Architecture

*   [Vision Pipeline internals — ScreenCapture, ImageProcessor, VisionPipeline, VideoCapture, ColorPicker](api/vision.md)

