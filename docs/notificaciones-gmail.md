# Correos transaccionales con Gmail

Implementados: verificación de email, recuperación de contraseña, aviso de contraseña actualizada, bienvenida al confirmar una cuenta nueva y aprobación de afiliación. El contrato de identidad está en [usuario-sesion-recuperacion.md](usuario-sesion-recuperacion.md). El registro no constituye una suscripción a campañas; no se implementan avisos segmentados.

## 1. Aplicar la base

La versión actual requiere 001–007. Si ya se aplicó 006, detener la API y aplicar una vez `database/007_usuario_verificacion_recuperacion.sql`; no volver a ejecutar 006. Los scripts no generan correos históricos. El repositorio conserva SQL parametrizado y el mismo DbContext.

La tabla `correos_transaccionales` mantiene los envíos privados por destinatario. No se utiliza la tabla general `notificaciones` para exponer correspondencia privada. Los triggers encolan dentro de la misma transacción del registro/aprobación: un rollback también revierte el aviso. Una restricción única por tipo y referencia evita duplicar el evento, incluso ante concurrencia.

Con Gmail desactivado se siguen guardando avisos. Al activar se procesan los eventos vigentes; los códigos usados, vencidos o ligados a un acceso invalidado se cancelan. El límite general de 14 días sigue aplicándose a los otros avisos.

## 2. Configurar la cuenta remitente

Gmail API usa OAuth 2.0. Se necesita una cuenta remitente administrada por el responsable de la aplicación, un cliente OAuth y un refresh token con acceso offline. Los destinatarios no necesitan cuentas Gmail ni autorizar Google. Referencia: [autorización de servidor de Google](https://developers.google.com/workspace/gmail/api/auth/web-server).

Para la prueba inicial:

1. En Google Cloud, crear o seleccionar el proyecto y habilitar Gmail API.
2. Configurar la pantalla de consentimiento OAuth; si está en modo de prueba, agregar la cuenta remitente como usuario de prueba.
3. Crear un cliente OAuth de aplicación web. Para obtener el token mediante el Playground oficial, registrar `https://developers.google.com/oauthplayground` como URI de redirección autorizada.
4. En [OAuth Playground](https://developers.google.com/oauthplayground/), abrir la configuración, activar **Use your own OAuth credentials** e introducir el Client ID y Client Secret de ese cliente. Usar acceso **Offline** y solicitar únicamente `https://www.googleapis.com/auth/gmail.send`.
5. Autorizar con la cuenta remitente e intercambiar el código por tokens. Guardar el refresh token únicamente en la configuración privada del servidor. No pegarlo en chats, commits ni en React. El access token temporal no sustituye al refresh token.
6. Usar como `Sender` la dirección de esa misma cuenta. No usar un remitente arbitrario.

Se solicita solamente el [scope de envío](https://developers.google.com/workspace/gmail/api/auth/scopes); no lectura de la bandeja. Los tokens pueden caducar o revocarse. Antes del despliegue, resolver el estado de publicación/verificación del cliente OAuth; el modo de prueba de Google no es una configuración permanente. [Ciclo de vida de tokens](https://developers.google.com/identity/protocols/oauth2).

Agregar **una sola sección** `Gmail` al objeto raíz de `Presentation/appsettings.Local.json`, conservando ConnectionStrings y JwtSettings existentes. Este archivo ya está ignorado por Git y excluido de publicación. No agregar secretos a `appsettings.json`.

En Development, `appsettings.Local.json` se carga después de `appsettings.json`:
sus valores tienen prioridad, y las variables de entorno/argumentos se agregan
después. Actualizar credenciales solo en el archivo general no corrige unas
credenciales antiguas presentes en el local. `GmailOptions` y el cliente de envío
se construyen al arrancar: **reiniciar la API después de modificar OAuth**.

```json
"Gmail": {
  "Enabled": false,
  "Sender": "cuenta-remitente@gmail.com",
  "ClientId": "CLIENT_ID",
  "ClientSecret": "CLIENT_SECRET",
  "RefreshToken": "REFRESH_TOKEN",
  "DailyLimit": 100
}
```

El fragmento debe integrarse con la coma correspondiente dentro del JSON existente. Cambiar `Enabled` a `true` solo después de completar las credenciales y revisar los pendientes. Reiniciar la API. Si se activa con campos faltantes, se rechaza la configuración al iniciar.

En Azure se usarán los mismos nombres mediante configuración segura: `Gmail__Enabled`, `Gmail__Sender`, `Gmail__ClientId`, `Gmail__ClientSecret`, `Gmail__RefreshToken`, `Gmail__DailyLimit`. Esta implementación no depende de instalar Gmail ni de un servicio exclusivo de Windows. No se desplegó nada en Azure.

## 3. Probar los disparadores automáticos

- Registrar una cuenta nueva con un email de prueba que controles: encola la verificación. Confirmar el email encola la bienvenida. Repetir el registro no crea otra cuenta ni otro aviso de bienvenida.
- Con ese usuario, solicitar afiliación; con un administrador, aprobarla: crea un único aviso de aprobación. No se envía al crear la solicitud pendiente.
- Con envío activo, el worker recibe una señal local tras registro/aprobación/encolado confirmado. Si no hay trabajo espera progresivamente 30, 60, 120, 240 y hasta 300 segundos, interrumpibles por esa señal. Cuando hay trabajo procesa un correo por iteración; la base conserva al menos dos segundos entre reservas globales. Ver [configuración de consumo](consumo-api.md) para límites, recuperación y verificación del 14 de octubre.
- Verificar el correo recibido y la carpeta Enviados del remitente. `enviado` significa que Gmail aceptó el mensaje, no que se haya confirmado la lectura o entrega en la bandeja de entrada.

Si el correo no llega, consultar `estado`, `intentos` y `error_codigo` del trabajo:
`fallido / oauth_rechazado` indica que Google rechazó la autorización. Comprobar el
conjunto ClientId/ClientSecret/RefreshToken de la configuración efectiva; no imprimir
sus valores en logs. Corregirlo y reiniciar no reintenta automáticamente los
trabajos fallidos. Si venció el código de verificación, solicitar uno nuevo con
`POST /api/Usuario/email/solicitar-verificacion`. Si todavía existe un código vigente,
la protección contra bombardeo conserva ese código y no emite otro mensaje.

## 4. Único endpoint

`POST /api/Notificacion`, requiere administrador y aplica el límite de escritura existente. No es necesario llamarlo desde React después del registro o la aprobación: los avisos ya se encolan automáticamente.

Bienvenida: `referenciaId` es el ID del usuario activo.

```json
{
  "tipo": "bienvenida",
  "referenciaId": "UUID_DEL_USUARIO"
}
```

Aprobación: `referenciaId` es el ID de una solicitud que ya está aprobada y pertenece a una cuenta activa.

```json
{
  "tipo": "afiliacion_aprobada",
  "referenciaId": "UUID_DE_LA_SOLICITUD"
}
```

Respuesta `202`:

```json
{ "id": "UUID_DEL_AVISO", "estado": "pendiente" }
```

Repetir el POST devuelve el mismo aviso y su estado actual: no reenvía correos enviados, fallidos ni inciertos. El cuerpo no acepta email, destinatarios, ciudad, asunto, texto libre ni HTML. El servidor verifica el evento y selecciona destinatario y plantilla. Sin sesión: `401`; usuario común: `403`; datos inválidos: `400`; evento inexistente/no elegible: `404`.

## 5. Operación y límites

- Por defecto, 100 intentos diarios UTC compartidos entre instancias; configurable entre 1 y 500. Es un límite propio, no una promesa sobre las cuotas de la cuenta Google.
- Hasta cinco intentos por aviso cuando Google rechaza explícitamente por cuota; espera exponencial desde dos minutos. Los intentos fallidos también consumen el presupuesto diario.
- Timeout HTTP de 20 segundos. Estados ambiguos —desconexión, timeout, 5xx o proceso caído tras reservar— quedan `incierto`. No se promete entrega exactamente una vez: Gmail no ofrece una transacción atómica con nuestra base.
- No se reenvía automáticamente un resultado incierto, para evitar duplicados. El Message-ID estable ayuda a revisar la carpeta Enviados; no garantiza deduplicación del proveedor.
- Un rechazo permanente o de OAuth queda `fallido`; corregir configuración y revisar los avisos con el operador. El endpoint no fuerza reenvíos.
- Se comprueba el estado activo del usuario al reservar y se cancelan pendientes de cuentas inactivas o avisos de más de 14 días. Una baja posterior a la reserva puede coincidir con un envío ya en curso.
- Un destinatario por mensaje, texto plano, sin datos personales en asunto, sin contraseñas, PDFs ni observaciones internas. No se registran tokens ni respuestas completas de Google en logs.
- La caída de Gmail no revierte el registro ni la aprobación. Un error al persistir la cola sí revierte la operación de base, para no perder el evento.

Los estados y diagnósticos se consultan desde PostgreSQL, sin añadir un endpoint público:

```sql
SELECT estado, count(*) FROM public.correos_transaccionales GROUP BY estado;

SELECT id, tipo, referencia_id, estado, intentos, created_at, enviado_at, error_codigo
FROM public.correos_transaccionales
ORDER BY created_at DESC LIMIT 50;
```

No borrar filas como limpieza rutinaria: conservan las claves que evitan reenvíos. La tabla guarda referencias y estados, no copias del cuerpo o direcciones. Revisar una política de retención junto con el ciclo de vida de cuentas antes de desplegar.

## Verificación

- Build Release sin errores ni advertencias.
- 402 comprobaciones de integración en PostgreSQL desechable: incluyen registros duplicados, aprobación concurrente, rollback, permisos, idempotencia, cuota global, reservas vencidas y cuentas inactivas.
- 26 comprobaciones del dispatcher y transporte Gmail simulado, incluyendo formato MIME, respuestas 429/403/500, límite de reintentos, interrupción, finalización SQL y dos workers concurrentes.
- No se enviaron correos reales. Queda pendiente comprobar OAuth y entrega con la cuenta remitente configurada.

Ejecutar pruebas del transporte sin Gmail ni DB:

```powershell
.\scripts\dotnet.ps1 run --project scripts/CorreoChecks/CorreoChecks.csproj -c Release
```

Ese comando ejecuta 22 comprobaciones. Las cuatro adicionales requieren `CORREO_TEST_CONNECTION` apuntando al cluster desechable `127.0.0.1:55432`, base con prefijo `net10_usuario_`, poblado por la suite. Modifican exclusivamente filas de esa base de prueba. La suite API se ejecuta como antes mediante `scripts/verify-usuario.ps1` y ahora aplica también el script 006; fuerza Gmail desactivado.
