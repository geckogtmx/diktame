# Modelo de Amenazas para dIKta.me V2

## 1. Activos (Assets)

1. **Datos del Usuario (User Data)**
   - Grabaciones de voz (temporales en memoria, posiblemente guardadas en archivo si la función está habilitada)
   - Texto transcrito (almacenado en la base de datos de historial, registros, notas)
   - Ajustes del usuario (incluyendo claves API almacenadas cifradas)
   - Notas (guardadas en archivos markdown)
   - Contenido del portapapeles (temporal)

2. **Integridad de la Aplicación (Application Integrity)**
   - Binarios de la aplicación y configuración
   - Almacenamiento seguro de claves API

3. **Privacidad (Privacy)**
   - La voz del usuario y el contenido transcrito
   - Información personal en las transcripciones (PII)

## 2. Actores de Amenazas (Threat Actors)

1. **Usuario Malicioso Local**
   - Tiene acceso físico o lógico a la máquina
   - Puede ejecutar procesos, leer archivos, etc.

2. **Sitio Web Malicioso**
   - Puede activar enlaces profundos (deep links `diktame://`) a través del navegador
   - Puede intentar ataques CSRF

3. **Malware**
   - Puede ejecutarse en la máquina del usuario
   - Puede intentar robar claves API, registros, etc.

4. **Atacante de Red**
   - Puede interceptar el tráfico de red (si no usa HTTPS)
   - Puede realizar ataques de hombre en el medio (man-in-the-middle)

5. **Amenaza Interna**
   - Usuario legítimo con intención maliciosa
   - Puede abusar de su acceso

## 3. Matriz de Amenazas (STRIDE)

### 3.1 Aplicación de Escritorio (DiktaMe.App)

| Tipo de Amenaza | Descripción | Impacto | Mitigación |
|-------------|-------------|--------|------------|
| Suplantación (Spoofing) | El atacante intenta hacerse pasar por la aplicación o el usuario mediante enlaces profundos | Medio | Validar el formato JWT, usar un parámetro state para CSRF, limitar la frecuencia de enlaces |
| Alteración (Tampering) | El atacante modifica binarios o la configuración | Alto | Firma de código, arranque seguro, verificaciones de integridad |
| Repudio (Repudiation) | El usuario niega una acción; falta de no repudio | Bajo | Registros de auditoría (BD de historial), aunque no firmados criptográficamente |
| Divulgación (Info Disclosure) | El atacante accede a datos (claves API, transcripciones) | Alto | Cifrado DPAPI para claves API, limpieza PII en registros, controles de acceso |
| Denegación de Servicio (DoS) | El atacante inactiva la app (ej., inundación de enlaces) | Medio | Limitación de frecuencia en enlaces, validación de entrada |
| Privilegios (Elevation) | El atacante gana mayores privilegios | Bajo | La app se ejecuta con privilegios estándar, sin elevación a admin |

**(Nota: Similar análisis STRIDE aplica a la Base de Datos, SecureStorage y Registro).*

## 4. Resumen de Mitigaciones

- **Gestión de Secretos**: Claves API cifradas con DPAPI, no en texto plano.
- **Validación de Entradas**: Enlaces de tokens validados para formato JWT; rutas validadas.
- **Codificación de Salida**: Limpieza de PII aplicada a registros según el nivel de privacidad.
- **Autenticación y Autorización**: El enlace profundo usa JWT con state para CSRF.
- **Seguridad en Comunicación**: Todo HTTP usa HTTPS.
- **Controles de Privacidad**: Cuatro niveles de privacidad permiten control al usuario.
- **Auditoría y Registro**: Eventos de seguridad registrados (fallos de enlace o validación).
- **Valores Seguros por defecto**: La aplicación se ejecuta como usuario estándar.

## 5. Notas del Modelado

- Gran parte de la superficie de ataque es la máquina local.
- Las amenazas más críticas involucran la divulgación de claves API o transcripciones.
- Las mitigaciones abordan fallas detectadas en auditorías previas (PII, recorrido de directorios).
- El monitoreo continuo es estrictamente recomendado.
