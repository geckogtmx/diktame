# Ajustes del Panel de Control

![General — sub-pestaña Panel de Control](/images/docs/settings-general-control-panel.png)

La pestaña **Control Panel (Panel de Control)** personaliza la apariencia, las métricas y la funcionalidad de la superposición flotante de dIKta.me.

## Estados del HUD de un vistazo

El Panel de Control flotante cambia su estado visual según lo que esté ocurriendo:

![Listo — inactivo esperando un atajo](/images/docs/cp-hud-ready.png)

![Escuchando — grabando audio](/images/docs/cp-hud-listening.png)

![Pensando — transcripción / procesamiento de IA en curso](/images/docs/cp-hud-thinking.png)

![Colapsado — solo barra superior](/images/docs/cp-hud-collapsed.png)

<div data-detail-section data-summary="Capas del Rodillo de Inactividad (estado, reloj + logo, clima)">

Cuando el HUD está colapsado e inactivo, cicla entre tres capas:

![Capa 1 — pastilla de estado](/images/docs/cp-hud-idle-status.png)

![Capa 2 — logo dIKta.me + reloj](/images/docs/cp-hud-idle-clock.png)

![Capa 3 — clima](/images/docs/cp-hud-idle-weather.png)

</div>

<div data-detail-section data-summary="Todas las sub-secciones del Panel de Control en detalle">

**Posición** — Siempre Encima, Dirección de Expansión, rejilla de 6 posiciones:

![Controles de Posición](/images/docs/settings-general-control-panel-position-detail.png)

**Efectos Visuales** — Efectos de Fondo, Alcance del Efecto, Intensidad del Glow, Onda de Voz:

![Efectos Visuales](/images/docs/settings-general-control-panel-effects-detail.png)

**Rodillo de Inactividad** — Animación de Branding, Mostrar Reloj, Mostrar Clima, Formato del Reloj, Duración:

![Controles del Rodillo de Inactividad](/images/docs/settings-general-control-panel-idle-detail.png)

**Auto-Colapsar + Auto-Ocultar**:

![Auto-Colapsar y Auto-Ocultar](/images/docs/settings-general-control-panel-autohide-detail.png)

</div>

## Visibilidad de la Interfaz
Alterna las filas visibles de la interfaz principal del Panel de Control:
*   **Show Actions (Mostrar Acciones)**: Muestra la fila de Enlaces Rápidos (ej., engranaje de Ajustes, Modelos, atajo de Chat Rápido).
*   **Show Engine Selection (Mostrar Selección de Motor)**: Muestra los menús desplegables de proveedores, permitiéndote cambiar al instante tu motor STT o modelo LLM activo.
*   **Show Presets List (Mostrar Lista de Ajustes Preestablecidos)**: Muestra el menú desplegable del Modo de Dictado activo, permitiendo cambiar rápidamente entre perfiles (ej., estándar vs. sintaxis de programación).

![Métricas completas visibles — tiles de preset, fila de modos y estadísticas de sesión](/images/docs/cp-hud-visibility-full.png)

![Solo fila de modos — estadísticas de sesión ocultas](/images/docs/cp-hud-visibility-medium.png)

![Mínimo — solo los tiles de preset](/images/docs/cp-hud-visibility-minimal.png)

## Apariencia y Comportamiento
*   **Themes & Glassmorphism (Temas y Cristalismo)**: dIKta.me cuenta con una interfaz de cristalismo (glassmorphism) totalmente consciente del tema. Puedes cambiar sin problemas entre paletas estéticas premium (como Midnight, Ember o Frost) para asegurar que los ajustes y el Panel de Control coincidan con el ambiente personal de tu escritorio.

Arrastra los deslizadores de abajo para ver el Panel de Control en cada tema:

<div data-theme-compare
     data-before="/images/docs/cp-hud-ready.png"
     data-after="/images/docs/cp-hud-ready-ember.png"
     data-before-label="Midnight"
     data-after-label="Ember"
     data-alt="Comparación del Panel de Control: Midnight vs Ember"></div>

<div data-theme-compare
     data-before="/images/docs/cp-hud-ready.png"
     data-after="/images/docs/cp-hud-ready-frost.png"
     data-before-label="Midnight"
     data-after-label="Frost"
     data-alt="Comparación del Panel de Control: Midnight vs Frost"></div>

*   **Snap-to-Position (Ajustar a la Posición)**: Arrastra el Panel de Control hacia cualquier borde o esquina de tu pantalla. Se fijará en una de las 6 posiciones predefinidas (arriba-izquierda, arriba-centro, arriba-derecha, abajo-izquierda, abajo-centro, abajo-derecha). La posición elegida se guarda y se restaura automáticamente la próxima vez que inicies la aplicación.
*   **Auto-Collapse Bar (Barra de Auto-Colapso)**: Habilita esto para minimizar el desorden en la pantalla. Cuando no estés dictando activamente, el Panel de Control se colapsará suavemente a un estado mínimo, expandiéndose automáticamente solo cuando interactúes con él.
*   **Idle Branding Animation (Animación de Marca en Reposo)**: Cuando el Panel de Control está colapsado y en reposo, esta función hace rodar el indicador de estado como un cilindro mecánico, alternando fluidamente entre tu estado de dictado actual, un reloj con la marca, y el clima local.
*   **Voice Waveform & VU Meter (Forma de Onda de Voz y Medidor VU)**: Mientras grabas, el Panel de Control muestra una forma de onda de voz dinámica. Este medidor VU visual en tiempo real proporciona confianza inmediata de que tu micrófono está capturando activamente tu voz.

## Métricas
Alterna la visualización de información de fondo útil en tiempo real directamente en el HUD después de que se completa una canalización:
*   **Show Session Total Tokens (Mostrar Total de Tokens de la Sesión)**: Muestra exactamente cuántos tokens de IA has acumulado dentro de la sesión de computación actual. Útil para gestionar costos o límites de uso.
*   **Show Diagnostics (Mostrar Diagnósticos)**: Habilita marcadores de rendimiento avanzados que muestran exactamente cuántos milisegundos tardó el motor de Voz a Texto, el motor LLM, y el Inyector de Texto en procesar tu último dictado.

## Comportamiento en Segundo Plano
*   **Dark Mode (Modo Oscuro)**: Anula el tema predeterminado de Windows de tu sistema para mostrar de forma nativa el Panel de Control, los menús de Ajustes y la superposición de Chat Rápido con una estética elegante y de bajo reflejo. 
*   **Startup Minimized (Minimizado al Inicio)**: Controla si el Panel de Control debería aparecer vívidamente al iniciar Windows, o esconderse completamente dentro de tu Bandeja del Sistema esperando una tecla de acceso rápido.
