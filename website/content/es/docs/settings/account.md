# Ajustes de Cuenta (Account Settings)

La pestaña de **Cuenta (Account)** es tu centro principal para administrar tu configuración primaria de dIKta.me, el sistema de créditos y la suscripción.

Si no quieres usar tus propias cuentas de Desarrollador para conectarte a Anthropic, OpenAI, o Google en la pestaña `API Keys` (Trae Tu Propia Clave), puedes utilizar la **Nube de dIKta.me (dIKta.me Cloud)** nativa.

## Opciones de Autenticación

Dependiendo de cómo pretendas usar la aplicación, dIKta.me te permite declarar cómo te autenticas:

1.  **Wallet Mode (Modo Billetera)**: Se autentica a través de la infraestructura en la nube de dIKta.me. Cada dictado, traducción y consulta de chat descuenta micro-centavos sin esfuerzo del saldo pre-cargado de tu Billetera según los tokens exactos que consumiste. No necesitas administrar claves de API, monitorear el tráfico, ni preocuparte por límites de consumo.
2.  **API Key Mode (Modo de Clave API)**: Omite por completo los servidores en la nube de dIKta.me. Debes proporcionar todas tus propias claves de desarrollador en la pestaña `API Keys`. Tus solicitudes se procesan completamente de forma local usando tus cuentas de proveedor personales.
3.  **Local Mode (Modo Local)**: Omite todo. dIKta.me se comunica exclusivamente con Ollama y Whisper.net ejecutándose de forma nativa en tu computadora, lo que significa que no se requiere internet ni autenticación en absoluto para dictar, preguntar o refinar texto.

## Gestión de Perfil

*   **Log In (Iniciar Sesión)**: Autentica tu cuenta de dIKta.me sin problemas a través de tu navegador web para acceder al saldo de tu Billetera de forma segura en múltiples PCs.
*   **Avatar Customization (Personalización de Avatar)**: Personaliza tu HUD de dIKta.me subiendo una foto de perfil personalizada. La interfaz de ajustes incluye una herramienta de recorte circular integrada para asegurar que tu avatar encaje perfectamente en tu panel de control y ventana de chat.
*   **Balance Top-Up (Recarga de Saldo)**: Compra créditos de computación adicionales que se abonan en tu sesión activa al instante sin interrumpir tu flujo de trabajo.
*   **Sign Out (Cerrar Sesión)**: Purga de manera segura tus tokens de autorización activos y vuelve a un estado No Autenticado. 

*Nota: Todos los ajustes locales de Dictado, Ajustes preestablecidos (Presets) y personalizaciones se conservan incluso si cierras sesión por completo o cambias los Modos de Autenticación.*
