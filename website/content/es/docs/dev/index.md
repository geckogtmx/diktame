# dIKta.me V2 - Documentación para Desarrolladores

Bienvenido a la documentación técnica, oficial y completa para desarrolladores y colaboradores del repositorio de código abierto de dIKta.me V2 (`geckogtmx/diktame`).

La aplicación está construida y escrita estrictamente en C# moderno, confiando ávidamente en el SDK ágil de .NET 8 y robusto marco de presentación WinUI 3 nativo de Windows. Esta guía central detalla exhaustivamente los componentes estructurales internos vitales y estrictamente necesarios para estudiar y modificar la aplicación subyacente correctamente sin romper características fundamentales.

## 💻 Fundamentos (Fundamentals)

Comienza obligatoriamente aquí si deseas aprender a compilar el proyecto base localmente o contribuir activamente a la interfaz nativa de Windows.
*   [Environment Setup (Configuración del Entorno)](setup.md)
*   [UI MVVM Architecture & DI (Arquitectura MVVM de UI y DI)](architecture/ui-mvvm.md)
*   [Audio Pipeline Architecture (Arquitectura de la Canalización de Audio)](architecture/audio-pipeline.md)
*   [Threat Model & Security (Modelo de Amenazas y Seguridad)](architecture/threat-model.md)

## 🔌 API y Extensibilidad (Extensibility)

Aprende metódicamente a interactuar directamente con el complejo contenedor avanzado de Inyección de Dependencias central de la aplicación para poder inyectar de forma totalmente segura nuevos e innovadores modelos encapsulados localizados, así como integrar servicios REST modernos basados en la nube de nivel empresarial.
*   [Speech-to-Text Providers (Proveedores de Voz a Texto)](api/stt-providers.md)
*   [Large Language Model Providers (Proveedores de Modelos de Lenguaje Grande)](api/llm-providers.md)
*   [Text-to-Speech Providers (Proveedores de Texto a Voz)](api/tts-providers.md)
