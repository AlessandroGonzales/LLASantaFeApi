# Usuario: sesión, verificación y recuperación

Paso 2, 14 de octubre de 2026. API .NET 10; frontend React pendiente de conectar. No se agregan refresh tokens, cookies de sesión, login Google ni administración pública de roles.

**Preparación obligatoria**

1. Detener la API.
2. Aplicar una vez `database/007_usuario_verificacion_recuperacion.sql` después de 001–006 en pgAdmin. Es transaccional y no confirma emails ni envía correos históricos. No reejecutarlo sobre una base que ya tiene 007.
3. Recompilar y ejecutar `Presentation`.
4. Mantener Gmail correctamente configurado en `appsettings.Local.json`/variables privadas para recibir códigos. La API no devuelve los códigos por HTTP ni habilita un bypass de desarrollo.

El nuevo código necesita las columnas y funciones de 007. Se mantiene el DbContext existente; la tabla pequeña de acciones se consulta mediante SQL parametrizado.

**Transición de las cuentas anteriores**

Las filas que existían antes de 007 conservan `requiere_verificar_email=false` y `email_verificado_at=NULL`. Pueden seguir entrando bajo los controles anteriores, pero el sistema no afirma que su email esté verificado. Pueden solicitar y completar verificación; cambiar el email desde DB obliga a verificar la dirección nueva y revoca accesos.

Toda cuenta nueva creada por la API requiere verificación antes del login. La bienvenida se encola después de confirmar el email. La aprobación de afiliación conserva el comportamiento transaccional existente.

**Endpoints**

| Método y ruta | Acceso | Resultado normal |
| --- | --- | --- |
| POST `/api/Usuario` | Público | 202 genérico; cuenta nueva y verificación se guardan en una transacción. |
| POST `/api/Usuario/login` | Público | 200 con `token`, `expiresAt`, `tokenType`. |
| POST `/api/Usuario/email/solicitar-verificacion` | Público | 202 genérico, exista o no una cuenta elegible. |
| POST `/api/Usuario/email/confirmar` | Público | 204; exige código, email y contraseña elegida en el registro. |
| POST `/api/Usuario/password/solicitar-recuperacion` | Público | 202 genérico. |
| POST `/api/Usuario/password/restablecer` | Público | 204; nueva contraseña, sin login automático. |
| POST `/api/Usuario/me/cerrar-sesion` | JWT | 204; revoca todas las sesiones vigentes de esa cuenta. |
| GET/PATCH/DELETE `/api/Usuario/me` | JWT | Perfil, campos editables y baja existentes. |

Login incorrecto, cuenta ausente/inactiva o bloqueo mantienen 401 genérico. Solo después de comprobar la contraseña correcta se devuelve 403 con `code: "email_not_verified"` para una cuenta nueva pendiente. Los códigos inválidos, vencidos, utilizados o invalidables por cambio de acceso devuelven 400 genérico. Un GET a un enlace no confirma ni modifica una cuenta.

Perfil añade `emailVerificado` y `requiereVerificarEmail`. Los errores de negocio añaden `code` a ProblemDetails junto con `traceId`; se especifica UTF-8. No incluyen contraseñas, códigos, hashes ni detalles de PostgreSQL.

**Prueba paso a paso en Swagger**

Registrar una cuenta de prueba con un buzón que controles:

```json
{
  "nombre": "Persona",
  "apellido": "Prueba",
  "email": "TU_EMAIL",
  "password": "<password>",
  "fechaNacimiento": "1990-01-01"
}
```

Usar el email real en lugar del marcador y una contraseña de prueba que no uses en otros servicios. La contraseña admite hasta 72 bytes UTF-8, no solamente 72 caracteres. Los demás campos del contrato de registro siguen disponibles.

La respuesta 202 no confirma existencia ni titularidad. Si se creó una cuenta nueva, se encola un correo de verificación. Probar login antes de verificar: con contraseña correcta debe devolver 403/`email_not_verified`; con contraseña incorrecta, 401.

El correo contiene un enlace con esta estructura:

```text
<FrontendUrl>/verificar-email#codigo=<UUID>.<codigo>
```

Las páginas `/verificar-email` y `/restablecer-password` todavía no existen en el frontend del ZIP. Para probar la API, copiar únicamente el valor después de `#codigo=` y enviar:

```json
{
  "email": "TU_EMAIL",
  "password": "TU_CONTRASENA_DE_REGISTRO",
  "codigo": "EL_CODIGO_COMPLETO_DEL_CORREO"
}
```

A POST `/api/Usuario/email/confirmar`. Debe responder 204. El email y contraseña deben corresponder al registro: así no se confirma automáticamente una cuenta creada por otra persona usando ese buzón. No añadir IDs de usuario, roles o banderas de verificación.

Repetir el código debe devolver 400. Ahora login devuelve un JWT; autorizar Swagger con el token y consultar `/api/Usuario/me`: `emailVerificado=true`. Se encola la bienvenida; la aceptación por Gmail no garantiza entrega en bandeja.

Para recuperar la contraseña, POST `/api/Usuario/password/solicitar-recuperacion`:

```json
{ "email": "TU_EMAIL" }
```

La respuesta siempre es genérica. Un correo de recuperación válido apunta a:

```text
<FrontendUrl>/restablecer-password#codigo=<UUID>.<codigo>
```

Copiar el código y enviar a POST `/api/Usuario/password/restablecer`:

```json
{
  "codigo": "EL_CODIGO_COMPLETO_DEL_CORREO",
  "nuevaPassword": "TU_NUEVA_CONTRASENA"
}
```

Debe devolver 204. Las sesiones anteriores deben recibir 401, la contraseña anterior deja de funcionar y se encola un aviso sin incluir la contraseña. Iniciar sesión nuevamente con la nueva contraseña. Este procedimiento también permite recuperar una cuenta aún no verificada: el código demuestra acceso al buzón y establece una contraseña propia.

Para salir, llamar POST `/api/Usuario/me/cerrar-sesion` con el JWT. Después, ese token y cualquier otro emitido con la versión anterior reciben 401. Este endpoint cierra todas las sesiones de la cuenta, no un solo dispositivo; no se añade una tabla de sesiones. El cierre, la baja y cambios de credenciales invalidan también los códigos asociados a la versión anterior.

**Vencimiento y límites**

- JWT: 15 minutos por defecto; `expiresAt` informa el vencimiento real y el servidor lo valida sin tolerancia adicional.
- Verificación: 60 minutos por defecto; `CuentaSecurity:VerificacionMinutes`, rango 5–120.
- Recuperación: 30 minutos por defecto; `CuentaSecurity:RecuperacionMinutes`, rango 5–60.
- Solicitudes de correo: `RateLimits:CuentaCorreo=5` por hora/IP, compartido por verificación y recuperación.
- Confirmación y restablecimiento: política `login`, además del límite de dos operaciones de autenticación simultáneas por instancia.
- Tres emisiones por cuenta/tipo/día UTC, con intervalo mínimo de un minuto. Solicitar de nuevo mientras hay un código vigente no lo reemplaza ni genera otro correo. Un código vencido/invalidado puede reemplazarse dentro de esos límites.
- La tabla conserva como máximo una acción por cuenta/tipo; la cola conserva eventos para deduplicación. Los límites del worker y la cuota diaria de Gmail siguen vigentes. Cuando se agota la cuota de envío, un código puede vencer antes de entregarse; la respuesta 202 no promete entrega.

No bloquear ni modificar la contraseña al solicitar recuperación. No reactivar cuentas dadas de baja. No permitir que el cliente elija el destinatario, origen del enlace, vencimiento, usuario objetivo o contenido del correo.

**Códigos y cola**

Cada emisión usa un UUID aleatorio nuevo y un código HMAC de 256 bits ligado al propósito, con clave derivada y separación de dominio desde la clave JWT del servidor. Solo la huella SHA-256 queda en `usuario_acciones`. El worker regenera el código para el correo y comprueba la huella, evitando almacenar códigos en claro o añadir otro secreto/configuración de cifrado. La base por sí sola no permite regenerar el código sin la clave del servidor.

El código se vincula a usuario, propósito, versión de acceso, vencimiento y consumo. PostgreSQL consume y actualiza de forma atómica, con usuario antes de acción en el orden de bloqueo. Dos peticiones concurrentes no pueden consumirlo dos veces. Código y contraseña nueva no se incluyen en logs.

Cambiar la clave JWT invalida también la validación criptográfica de estos códigos. Para que una rotación permita solicitar reemplazos inmediatamente, con la API detenida expirar las acciones no consumidas; de lo contrario la emisión repetida conserva el enlace vigente en DB hasta su vencimiento. No borrar el contador diario:

```sql
UPDATE public.usuario_acciones SET vence_at=now() WHERE usado_at IS NULL;
```

Esto es parte de una rotación autorizada, no una tarea periódica ni una acción que se haya ejecutado sobre la base real en esta entrega.

**Contrato de sesión para React**

Guardar el JWT y `expiresAt` en memoria del cliente. Enviar `Authorization: Bearer <token>` únicamente a operaciones privadas. No introducir tokens de sesión en URLs ni persistirlos en `localStorage`. Recargar la página requiere login nuevamente con esta primera versión; no hay renovación silenciosa.

Cargar el perfil al entrar a la sección privada y compartir esa copia entre componentes. En PATCH exitoso, actualizar/refrescar el perfil. Al recibir 401 o vencer `expiresAt`, borrar la sesión y solicitar login. Un 403 `email_not_verified` pertenece al flujo de confirmación; un 403 por rol no debe provocar reintentos automáticos.

Al salir, borrar la sesión local incluso si la llamada falla; la revocación en servidor solo queda confirmada con 204. Si hay desconexión, el JWT sigue sujeto a su vencimiento y controles vigentes. No confirmar que fue revocado si no se recibió respuesta.

Las páginas del correo deben leer `codigo` desde el fragmento, mantenerlo en memoria y quitarlo de la URL. No confirmar automáticamente al abrir la página ni cargar scripts externos con el código. Solicitar datos/confirmación explícita y enviar el POST a la API. En restablecimiento, comprobar las dos entradas de contraseña en React; la API recibe una y vuelve a validar su calidad.

`FrontendUrl` es el origen configurado por el servidor, HTTPS en Vercel y HTTP permitido solo para localhost/127.0.0.1. No se acepta una URL de retorno enviada por quien solicita el correo. Las rutas del frontend se incorporarán cuando se conecte React.

**Administrador**

El registro siempre asigna `Usuario`. El rol nunca se toma del JSON ni se concede por un email específico. Los endpoints de administración siguen exigiendo `Admin` o `SuperAdmin`, con vigencia comprobada en PostgreSQL por cada petición.

Para una cuenta administrativa nueva: registrarla, verificarla, asignar el rol desde un canal privado de DB y volver a iniciar sesión. La modificación de rol revoca los JWT anteriores. Las cuentas administrativas existentes mantienen la transición descrita arriba; no se cambia su rol automáticamente.

Seguir [administrador inicial](administrador-inicial.md) y ejecutar manualmente
[crear_admin.sql](../database/operaciones/crear_admin.sql) con el UUID de la cuenta
verificada. El script comprueba las precondiciones y aborta si no puede asignar el
rol; no confirma silenciosamente una actualización de cero cuentas.

No usar esta asignación como endpoint público ni poner credenciales de PostgreSQL en React.

**Verificación**

La suite temporal aplica 007 y prueba compatibilidad de cuentas anteriores, registro/verificación, no enumeración de cuentas, rechazo de datos extra, propósito, vencimiento, reutilización, concurrencia, cuotas, rollback, recuperación, revocación de varias sesiones, permisos y baja. Gmail se simula; no se enviaron mensajes reales durante estas pruebas. La conectividad real de Gmail se mantiene como la integración existente, no como una nueva prueba ejecutada.

Resultado final: compilación Release sin errores ni advertencias; 503 comprobaciones de integración HTTP/PostgreSQL, 54 de consumo/worker/caché y 45 de correo/dispatcher, incluidas metadata y reserva SQL. Total: 602 comprobaciones correctas. El servidor PostgreSQL temporal quedó detenido. Gitleaks Worktree se revisa antes de entregar los cambios.

Las recomendaciones de respuestas genéricas, códigos de uso único, expiración y revocación después de recuperar una cuenta se contrastaron con [OWASP: recuperación de contraseña](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html).
