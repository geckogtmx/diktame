# Contribuir a dIKta.me

Gracias por tu interés.

## La situación real

Soy un desarrollador solo. Construí esto en ~10 semanas y lo mantengo en mi tiempo libre. No tengo capacidad para revisar pull requests por ahora.

## Lo que puedes hacer

- **¿Encontraste un bug?** Abre un issue. Los leo todos.
- **¿Tienes una idea?** Abre una discusión. Quiero escucharla.
- **¿Quieres hacer fork y construir?** Adelante — es MIT. No necesitas permiso.

## Lo que no puedo prometer

- Revisión o merge de PRs en ningún plazo específico
- Que los feature requests se implementen
- Respuestas más rápidas que "cuando pueda"

## Si eres realmente bueno

Si eres un desarrollador que quiere contribuir de forma significativa y constante — escríbeme directo. Estoy abierto a encontrar a la persona correcta para manejar la parte de comunidad. Pero prefiero ser honesto sobre mi capacidad que hacer promesas que no puedo cumplir.

## Stack Tecnológico

- **Lenguaje**: C# 12 (.NET 8)
- **UI Framework**: WinUI 3 (Windows App SDK 1.6)
- **Testing**: xUnit + Moq + FluentAssertions
- **Arquitectura**: MVVM (CommunityToolkit.Mvvm)

## Inicio Rápido

```bash
git clone https://github.com/geckogtmx/diktame.git
dotnet build DiktaMe.sln
dotnet test DiktaMe.sln
```

## Estructura del Proyecto

- `src/DiktaMe.App` — UI (Views, ViewModels, XAML)
- `src/DiktaMe.Core` — Lógica de negocio (Pipelines, Providers, Services)
- `tests/DiktaMe.Core.Tests` — Tests xUnit (1,134 y subiendo)

## Estándares de Código

- Sigue los patrones existentes (`.editorconfig` incluido)
- Trunk-based: commits pequeños y frecuentes a `main`
- Commits convencionales con sufijo `[TASK_ID]`
- Toda lógica de negocio nueva en `DiktaMe.Core` debe tener unit tests
- Cero telemetría, cero persistencia externa de datos — la privacidad no es negociable

## Sobre el Creador

dIKta.me es desarrollado y mantenido por Eduardo Garcia-Torres — un ejecutivo de marketing y negocios de México con más de 20 años de experiencia en consultoría de TI, medios digitales, producción audiovisual y gestión de proyectos en empresas internacionales y startups. No es ingeniero de software de formación. dIKta.me es su primera aplicación de escritorio, construida desde cero con C#, WinUI 3 y herramientas de desarrollo con IA.

Las decisiones de producto detrás de dIKta.me vienen de dos décadas construyendo negocios y lanzando productos — no de un título en ciencias de la computación.

Bilingüe (inglés/español). [LinkedIn](https://www.linkedin.com/in/eduardogarciatorres/)

## Licencia

Al contribuir, aceptas que tus contribuciones serán licenciadas bajo la [Licencia MIT](LICENSE).
