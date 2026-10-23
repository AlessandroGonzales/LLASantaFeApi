# CU-U01: recorrido integral de un usuario en Swagger

Fecha: 23 de octubre de 2026. Contratos contrastados con los controladores,
DTO, validadores y repositorios actuales. Esta es una guía de ejecución manual:
los resultados esperados no son resultados observados de una corrida nueva.

## Estado informado al cerrar la sesión del 23 de octubre

El usuario informó que el recorrido de cuenta y participación probado funcionó
correctamente y que completó la baja de la cuenta. La encuesta inicialmente no
aparecía porque era un borrador: se diagnosticó el estado y se indicó su publicación
administrativa, conservando los filtros de disponibilidad.

La carga/descarga manual de PDF **no se ejecutó**: por decisión del usuario, la
integración de almacenamiento y análisis del archivo se retomará al preparar Azure.
La reactivación de cuentas dadas de baja queda pospuesta. Las demás entidades
continuarán sus pruebas en una sesión posterior. Este informe del usuario no marca
como ejecutadas todas las ramas negativas, pruebas de carga o casillas siguientes.

## Objetivo y alcance

Una persona crea su cuenta, demuestra acceso a su email, inicia sesión, consulta y
actualiza su perfil, solicita afiliación, responde una encuesta, presenta una
solicitud con PDF, consulta su resolución, recupera su contraseña, cierra sus
sesiones y finalmente se da de baja de la aplicación.

Se comprueba comportamiento funcional, privacidad, permisos, unicidad, reintentos y
revocación. Una prueba funcional correcta no demuestra capacidad ante picos ni
concluye la preparación de Azure. La integración real con React sigue pendiente.

Actores:

- **A:** cuenta nueva de una persona de prueba. Hace el recorrido principal.
- **B:** otra cuenta común, verificada. Comprueba aislamiento y ramas alternativas.
- **ADM:** administrador existente con rol Admin/SuperAdmin. Publica una encuesta,
  aprueba/rechaza afiliaciones y gestiona solicitudes. Sus acciones no son permisos
  de A o B.
- **Servicios:** PostgreSQL, worker/Gmail y antivirus local del flujo PDF actual.

Usar cuentas y archivos de prueba. Los marcadores entre `<...>` se sustituyen por
los valores de esta corrida, sin compartir contraseñas, códigos ni JWT en informes.

## Preparación

1. Usar una base de pruebas con los scripts **001–009** aplicados una vez y en orden.
   No reejecutar migraciones ya aplicadas ni reiniciar datos de la base habitual.
2. Compilar/iniciar **Presentation** en Development. Abrir el Swagger que indique
   el perfil elegido; `launchSettings.json` define HTTP 5275 y HTTPS 7044.
3. Disponer de dos buzones de prueba accesibles y del acceso ADM. Gmail debe estar
   configurado y habilitado para comprobar entregas reales. La API no devuelve
   códigos de verificación/recuperación en sus respuestas HTTP.
   Si solo existe el rol Usuario, preparar la cuenta ADM siguiendo
   [administrador inicial](administrador-inicial.md) antes de las intervenciones
   administrativas. Mantener A/B con rol Usuario para las pruebas de permisos.
4. Tener ClamAV real operativo para el PDF, según
   [preparación local](prueba-solicitud-pdf-swagger.md). El texto funciona sin él;
   una carga nueva no debe aceptarse omitiendo el análisis. Blob/Defender es una
   preparación posterior para Azure, no el almacenamiento implementado actual.
5. Preparar un PDF válido y limpio de hasta **2.097.152 bytes (2 MiB)**. Se admite
   uno por solicitud. Adjuntarlo cuando la solicitud aún esté pendiente y tenga
   menos de 24 horas.
6. En Swagger usar **Authorize** para cambiar entre JWT_A, JWT_B y JWT_ADM. La
   petición privada debe enviar `Authorization: Bearer <token>` una sola vez.
   Quitar la autorización cuando se pruebe acceso anónimo.
7. JWT vence en 15 minutos por defecto: obtener uno nuevo mediante login si vence
   durante el recorrido. Para comprobar revocación, usar tokens recién emitidos;
   un 401 de un token ya vencido no demuestra que el cierre/recuperación lo revocó.

Datos que conservar durante la prueba:

| Dato | Obtención |
| --- | --- |
| EMAIL_A / CLAVE_A | Buzón y contraseña de prueba nuevos. |
| EMAIL_B / CLAVE_B | Segunda cuenta; repetir registro y verificación. |
| CIUDAD_ID | Catálogo geográfico real, opcional. |
| JWT_A_1 / JWT_A_2 / JWT_B / JWT_ADM | Respuestas de login. Registrar expiración aparte. |
| AFILIACION_ID | POST de afiliación de A. |
| ENCUESTA_ID / REVISION_ENCUESTA | Creación y consultas administrativas. |
| SOLICITUD_ID / REVISION_SOLICITUD | Creación y detalle de solicitud. |
| CLAVE_TEXTO / CLAVE_PDF | Dos UUID distintos; conservar cada uno en su operación. |

Para generar una clave de idempotencia en PowerShell:

```powershell
[guid]::NewGuid().ToString()
```

Las cuotas siguen activas. Por defecto: registro 5/hora/IP; login y confirmaciones
10/minuto/IP; escritura 20/minuto/IP; solicitar verificación/recuperación comparte
5/hora/IP. Hay además 120 solicitudes/minuto/IP y límites de concurrencia.
Verificación/recuperación admite tres emisiones por cuenta/tipo/día UTC y un
intervalo mínimo de un minuto. Respetar `Retry-After` ante 429; no subir límites
para ocultar un resultado de cuota esperado. La configuración privada puede
modificar los valores HTTP; aquí se describen los predeterminados del código.

## Flujo principal

### 1. Elegir ubicación sin autenticación

Consultar `GET /api/Departamento`: **200**. Elegir un `idDepartamento` y consultar
`GET /api/Ciudad?departamentoId=<ID>`: **200**. Guardar un `idCiudad` del resultado.
También se puede registrar sin ciudad, usando null. No inventar referencias.

### 2. Registrarse

`POST /api/Usuario`, sin JWT:

```json
{
  "nombre": "Ana",
  "apellido": "Prueba",
  "email": "<EMAIL_A>",
  "password": "<CLAVE_A>",
  "fechaNacimiento": "1995-06-15",
  "telefono": "+5493411234567",
  "genero": "prefiero_no_decir",
  "profesion": "Profesión de prueba",
  "fotoPerfilUrl": null,
  "ciudadId": null
}
```

Si se usa CIUDAD_ID, reemplazar null por ese UUID entre comillas. La contraseña
nueva requiere al menos 15 caracteres y como máximo 72 bytes UTF-8.

Esperado: **202**, respuesta genérica, sin token ni identificador de cuenta.
Si se creó una cuenta nueva, queda con rol Usuario y verificación pendiente; se
encola el correo de verificación. Todavía no corresponde el correo de bienvenida.
Un 202 no demuestra por sí solo creación ni entrega del correo.

### 3. Comprobar la barrera de verificación

`POST /api/Usuario/login`:

```json
{ "email": "<EMAIL_A>", "password": "<CLAVE_A>" }
```

Con clave correcta antes de verificar: **403**, `code: "email_not_verified"`.
Con clave incorrecta: **401**, credenciales inválidas. La API debe continuar
atendiendo otras peticiones; una excepción de negocio no implica caída del host.

### 4. Solicitar el código y confirmar el email

El registro ya emite el primer código. Para cubrir el endpoint de solicitud,
ejecutar una vez `POST /api/Usuario/email/solicitar-verificacion`:

```json
{ "email": "<EMAIL_A>" }
```

Esperado: **202** genérico. Si ya hay un código vigente, se conserva y no se
genera otro correo. Este resultado no obliga a que llegue un segundo mensaje.

El correo apunta a `<FrontendUrl>/verificar-email#codigo=...`. Como las pantallas
React todavía no están conectadas, copiar **todo el valor después de `#codigo=`**,
incluyendo UUID, punto y firma. Abrir el enlace no confirma automáticamente.

`POST /api/Usuario/email/confirmar`, sin JWT:

```json
{
  "email": "<EMAIL_A>",
  "password": "<CLAVE_A>",
  "codigo": "<CODIGO_VERIFICACION_COMPLETO>"
}
```

Esperado: **204**. Se confirma la cuenta y se encola bienvenida. Reutilizar el mismo
código: **400**. Un código incorrecto, vencido o usado con otra contraseña también
debe rechazarse sin confirmar la cuenta.

### 5. Iniciar sesión y ver el perfil

Repetir el login correcto: **200**, con `token`, `expiresAt` y `tokenType: "Bearer"`.
Guardar JWT_A_1 y autorizar Swagger. `GET /api/Usuario/me`: **200**.

Comprobar datos registrados, `roleNombre: "Usuario"` y `emailVerificado: true`.
`requiereVerificarEmail` representa la política de la cuenta y puede seguir siendo
true después de confirmar; no significa por sí solo que esté pendiente.
No deben aparecer hash, contraseña, versión de acceso ni contadores de fallos.

Volver a registrar el mismo email con datos válidos: **202** genérico. No duplica
la cuenta, no reemplaza sus datos/contraseña ni la convierte en administrador.

### 6. Actualizar solo los datos permitidos

`PATCH /api/Usuario/me`:

```json
{ "nombre": "Ana Actualizada", "telefono": "+5493417654321" }
```

Esperado: **204**. Consultar `GET /api/Usuario/me`: **200**, nombre/teléfono nuevos,
apellido/email y demás campos omitidos conservados.

Probar también un cambio de `apellido`, `ciudadId` por un UUID válido y
`fotoPerfilUrl` por una URL HTTPS de prueba. Los cinco campos editables son:
**nombre, apellido, telefono, fotoPerfilUrl, ciudadId**.

Para probar eliminación de valores opcionales:

```json
{ "telefono": null, "fotoPerfilUrl": null, "ciudadId": null }
```

Debe dar **204** y el perfil debe reflejar los null. Volver a poner el teléfono de
prueba para comprobar el contacto administrativo al revisar la afiliación.
Nombre/apellido no admiten null. Email, contraseña, rol, nacimiento, género y
profesión no se modifican por este PATCH.

### 7. Solicitar afiliación y consultar su estado

Con JWT_A, `GET /api/SolicitudAfiliacion/me`: **404** si aún no tiene solicitud.

`POST /api/SolicitudAfiliacion`:

```json
{ "confirmacion": true }
```

Esperado: **201**, `id` y `estado: "pendiente"`. Guardar AFILIACION_ID.
Consultar de nuevo `/me`: **200**, misma solicitud pendiente.
Repetir POST: **409**, sin una segunda solicitud.

`POST /api/Usuario/me/afiliacion` es un alias: tiene la misma unicidad; no constituye
otra funcionalidad ni permite pedir una segunda afiliación.

### 8. Intervención ADM: aprobación y contacto

Cambiar a JWT_ADM:

1. `GET /api/SolicitudAfiliacion/pendientes?estado=pendiente&limite=20`: **200**.
   Continuar con `siguienteCursor` si hace falta; localizar AFILIACION_ID.
2. `GET /api/SolicitudAfiliacion/<AFILIACION_ID>`: **200**. Comprobar nombre, email,
   teléfono, cuenta activa y flags de verificación. Son los datos de contacto
   actuales del perfil.
3. Anotar `GET /api/SolicitudAfiliacion/totales`: **200**, valores iniciales.
4. `POST /api/SolicitudAfiliacion/<AFILIACION_ID>/aprobar`:

   ```json
   { "observacion": "Datos revisados en la prueba funcional." }
   ```

   Esperado: **204**. Deben quedar aprobación y aviso en cola en la misma transacción.
5. Consultar totales: una aprobación nueva incrementa `totalAfiliados` en uno y
   reduce pendientes en uno, sin aumentar `totalSolicitudes`, si nadie más opera.

Volver a JWT_A. `/api/SolicitudAfiliacion/me`: **200**, estado aprobada y fecha de
aprobación. No expone la observación interna ni el identificador del administrador.
Comprobar recepción del correo de aprobación. Repetir solicitud: **409**.

El endpoint entrega datos para que ADM contacte a la persona; **no inicia una
llamada ni conversación de WhatsApp**. El total contabiliza aprobaciones en el
sistema, no certifica por sí solo un padrón externo.

### 9. Intervención ADM: preparar una encuesta

Con JWT_ADM, `POST /api/Encuesta` usando el archivo completo
[encuesta-v1.json](ejemplos/encuesta-v1.json). Resultado: **201**, ID y revisión 1.
Guardar ENCUESTA_ID. `POST /api/Encuesta/<ENCUESTA_ID>/publicar`:

```json
{ "revision": 1 }
```

Esperado: **204**. Consultar el detalle administrativo para obtener la revisión
actual, que será 2 si no hubo ediciones. Cada cambio de estado/edición incrementa
la revisión; no reutilizar un número antiguo.

### 10. Consultar y responder la encuesta

Volver a JWT_A:

1. `GET /api/Encuesta?limite=20`: **200**, encuesta disponible y `respondida: false`.
   Recorrer páginas si hace falta. No devuelve configuraciones completas.
2. `GET /api/Encuesta/<ENCUESTA_ID>`: **200**, cuatro preguntas y configuración v1.
3. `GET /api/Usuario/me/encuestas/<ENCUESTA_ID>/respuesta`: **404** antes de responder.
4. `POST /api/Usuario/me/encuestas/<ENCUESTA_ID>/respuesta`:

   ```json
   {
     "respuestas": {
       "facilidad": 3.5,
       "seccion": "noticias",
       "dispositivos": ["movil", "computadora"],
       "comentario": "La búsqueda de información podría ser más clara."
     }
   }
   ```

   Esperado: **201**, `estado: "registrada"` y `Location` del comprobante propio.
5. GET del comprobante: **200**, mismo ID y fecha. Repetir POST: **409**.
   Volver al listado: `respondida: true`.

Los IDs y tipos corresponden al archivo de ejemplo. Con otra encuesta deben
usarse sus preguntas reales. Una opcional no respondida se omite; no se manda null.

ADM puede consultar `GET /api/Encuesta/<ENCUESTA_ID>/respuestas?limite=10`: **200**,
valores almacenados sin identificadores de usuarios ni IP en esa respuesta HTTP.
La relación con la cuenta existe en DB; no son respuestas anónimas.

ADM cierra con la revisión actual en `POST /api/Encuesta/<ENCUESTA_ID>/cerrar`:
**204**. Para A, el detalle normal queda en **404**, pero el comprobante propio
permanece en **200**. Ningún nuevo envío debe guardarse tras el cierre.

### 11. Enviar una solicitud ciudadana formal

Con JWT_A, `GET /api/SolicitudCiudadana?limite=20`: **200**, historial propio.
Crear con `POST /api/SolicitudCiudadana`, header `Idempotency-Key: <CLAVE_TEXTO>`:

```json
{
  "motivo": "proyecto",
  "mensaje": "Presento una propuesta de mejora para el sistema de atención de mi localidad."
}
```

Esperado: **201**, `id`, acuse `estado: "recibida"` y `Location`. Guardar SOLICITUD_ID.
Consultar `GET /api/SolicitudCiudadana/<SOLICITUD_ID>`: **200**, estado persistido
**pendiente**, revisión 1 y `estadoPdf: "sin_adjunto"`. "Recibida" es el acuse del
POST, no el valor del estado persistido.

Repetir mismos usuario, clave y datos: **200**, mismo ID. Misma clave con mensaje
distinto: **409**, `code: "idempotency_conflict"`; el mensaje original no cambia.
El alias `/api/Usuario/me/solicitudes` comparte la misma operación y clave.

### 12. Adjuntar, comprobar y descargar el PDF

Antes de que ADM gestione la solicitud, ejecutar
`POST /api/SolicitudCiudadana/<SOLICITUD_ID>/pdf`:

- Header `Idempotency-Key: <CLAVE_PDF>`, distinto de CLAVE_TEXTO.
- Cuerpo `multipart/form-data` con **un único archivo**, campo `archivo`.
- Seleccionar el PDF limpio preparado; no enviar base64 ni campos adicionales.
- Dejar que Swagger/navegador genere el boundary de Content-Type.

Esperado: **201**, `estado: "disponible"` después del análisis. El detalle queda con
`tienePdf: true`, `estadoPdf: "disponible"`, tamaño y huella SHA-256.
Repetir misma clave y mismos bytes: **200**, sin almacenar otro archivo ni analizar
de nuevo. Una clave diferente con el PDF ya adjunto: **409**.

`GET /api/SolicitudCiudadana/<SOLICITUD_ID>/pdf`: **200**, descarga PDF. Comprobar
que coincide con el original; no basta el código HTTP. El enlace requiere JWT y
titularidad o permisos administrativos, no es una URL pública permanente.

Si falla el archivo, la solicitud textual debe seguir existiendo. Consultar detalle
antes de reintentar; no reenviar el POST de texto con otra clave para "recuperar" el PDF.

### 13. Intervención ADM: respuesta formal a la solicitud

Con JWT_ADM, consultar detalle para leer su revisión actual. Enviar
`PATCH /api/SolicitudCiudadana/<SOLICITUD_ID>/gestion`:

```json
{
  "revision": 1,
  "estado": "en_revision",
  "respuesta": "La presentación está siendo revisada."
}
```

Sustituir 1 por la revisión consultada. Esperado: **204**. Consultar otra vez para
obtener la nueva revisión y finalizar con:

```json
{
  "revision": 2,
  "estado": "resuelta",
  "respuesta": "La propuesta fue revisada. Esta es la respuesta formal de prueba."
}
```

Esperado: **204**. Volver a JWT_A: detalle **200** con estado resuelta y respuesta.
El PDF continúa descargable. Una revisión vieja o un intento de reabrir/finalizar
otra vez debe dar **409**. No se presupone un correo de resolución: no es uno de
los eventos transaccionales implementados actualmente.

### 14. Recuperar la contraseña y revocar accesos anteriores

Antes de recuperar, hacer otro login y guardar JWT_A_2. Ambos deben permitir
`GET /api/Usuario/me` mientras estén vigentes. Quitar autorización para las
operaciones públicas de recuperación.

`POST /api/Usuario/password/solicitar-recuperacion`:

```json
{ "email": "<EMAIL_A>" }
```

Esperado: **202** genérico. Solicitarlo no cambia la contraseña ni revoca la sesión.
Comprobar que JWT_A_2 todavía funciona y copiar el código del correo
`<FrontendUrl>/restablecer-password#codigo=...`.

`POST /api/Usuario/password/restablecer`:

```json
{
  "codigo": "<CODIGO_RECUPERACION_COMPLETO>",
  "nuevaPassword": "<CLAVE_A_NUEVA>"
}
```

Esperado: **204**. La nueva clave cumple los mismos límites y difiere de CLAVE_A.
Reutilizar código: **400**. JWT_A_1 y JWT_A_2 deben recibir **401**; para probar la
revocación, usarlos antes de su vencimiento natural. Login con clave vieja: **401**.
Login con clave nueva: **200**. Comprobar aviso de contraseña actualizada, que no
debe contener la contraseña. Se recupera la cuenta, no se crea otra identidad.

### 15. Cerrar todas las sesiones

Con la clave nueva, obtener dos JWT vigentes. Con uno llamar
`POST /api/Usuario/me/cerrar-sesion`, sin cuerpo: **204**.
Probar ambos contra `/api/Usuario/me`: **401**. La cuenta sigue activa y un nuevo
login correcto devuelve **200**. El cierre actual revoca todas las sesiones de la
cuenta, no solo la del dispositivo que hizo la llamada.

### 16. Dar de baja la cuenta, al final del recorrido

Iniciar sesión nuevamente; con ese JWT llamar `DELETE /api/Usuario/me`: **204**.
Es baja lógica de la aplicación; no borra físicamente la cuenta ni sus expedientes
y no constituye una desafiliación partidaria.

Conservar el token e intentar perfil, PATCH y participación: **401**. Login correcto
de la cuenta dada de baja: **401**. Solicitar recuperación puede responder **202**
genérico, pero no debe reactivarla. Volver a registrar el mismo email: **202**
genérico, sin reactivación ni una segunda cuenta. No hay reactivación pública.

## Ramas alternativas y pruebas negativas

Ejecutarlas antes de dar de baja A, o sobre B/fixtures separados. Las cuotas y el
bloqueo de login son comportamientos esperados: no confundirlos con errores de
negocio distintos ni agotar los cupos antes del flujo principal.

### A. Identidad, validación y permisos

| Prueba | Esperado |
| --- | --- |
| Registro con contraseña de menos de 15 caracteres, fecha futura o ciudad inexistente | 400, sin alta válida. |
| Registro con `rolId`, `activo` o campos no admitidos | 400, sin privilegios elegidos por el cliente. |
| Registro repetido del mismo email | 202 genérico, una sola cuenta y datos originales conservados. |
| Login con email inexistente o clave incorrecta | 401 genérico; el servidor sigue operativo. |
| Cinco fallos consecutivos en B, sin login correcto intermedio | Bloqueo de nuevos logins de B por cinco minutos; después, login correcto 200. No agotar antes la cuota HTTP del minuto. |
| JWT de B emitido antes de esos fallos | Sigue válido hasta su vencimiento/revocación; los fallos de login no revocan por sí solos tokens ya emitidos. |
| JWT ausente, alterado o vencido en `/api/Usuario/me` | 401. |
| Código usado, vencido, de otro propósito o mal formado | 400, sin confirmar/restablecer. Probar expiración sin modificar relojes/datos de la base habitual. |
| PATCH vacío, nombre null, teléfono inválido o foto HTTP | 400; perfil original conservado. |
| PATCH con email, password, rol, nacimiento, género o profesión | 400; no son campos editables de este endpoint. |
| JSON con claves duplicadas o campos extra | 400. |
| A/B consultan pendientes/totales de afiliación, resultados de encuestas o administración de solicitudes | 403. |
| A/B intentan publicar/cerrar encuestas o aprobar afiliaciones | 403. |

### B. Afiliación rechazada

Con B, crear su propia afiliación: **201**. ADM rechaza mediante
`POST /api/SolicitudAfiliacion/<ID_B>/rechazar`:

```json
{ "motivo": "Motivo público de rechazo utilizado en esta prueba." }
```

Esperado: **204**. B consulta `/me`: **200**, estado rechazada, fecha y motivo público.
No ve notas internas ni actor administrativo. Repetir solicitud de B: **409**.
Repetir rechazo o intentar aprobar esa misma solicitud: **409**. Motivo ausente o
de menos de diez caracteres: **400**, sin resolver la solicitud.
No se presupone un correo de rechazo: actualmente no se implementó ese evento.

### C. Encuestas

| Prueba | Esperado |
| --- | --- |
| B consulta el comprobante de la encuesta que solo respondió A | 404; no obtiene el comprobante de A. |
| A repite un envío ya guardado | 409; se conserva una única respuesta por cuenta/encuesta. |
| Enviar número como string, opción inexistente, selección repetida, NUL o Unicode mal formado | 400, sin guardar respuesta. |
| Omitir pregunta obligatoria o agregar una desconocida | 400. |
| Responder un borrador, encuesta cerrada o fuera de fechas | 409 si existe; no se guarda. |
| Consultar detalle normal de borrador/cerrada/fuera de fechas | 404. |
| Responder encuesta inexistente | 404. |
| Dos POST concurrentes de la misma cuenta en una encuesta nueva | Uno 201 y otro 409; una fila. Ejecutar en entorno de pruebas. |
| Listar con límite 0 o 51 / respuestas administrativas con límite 21 | 400. |
| Listar con `limite=1` y continuar con `siguienteCursor` | 200, sin repetir IDs; cursor null al terminar. |

Para un timeout del envío, consultar el comprobante una vez. 200 confirma una
respuesta propia registrada; 404 solo indica que no se encontró una confirmada
en ese instante. No demuestra que una petición anterior ya haya terminado.
No hacer reenvíos automáticos ni sondeos continuos.

### D. Solicitudes, archivos y aislamiento

| Prueba | Esperado |
| --- | --- |
| B consulta detalle/PDF de SOLICITUD_ID de A | 404. |
| B lista sus solicitudes | No incluye las de A. |
| B intenta adjuntar un PDF a la solicitud de A | 404, sin modificación. |
| A intenta gestionar su propia solicitud como administrador | 403. |
| Motivo fuera de los cinco admitidos o mensaje fuera de 10–5000 caracteres | 400. |
| Mismo texto y clave de idempotencia | 200, mismo ID; no consume una nueva alta. |
| Misma clave y otro contenido | 409, sin reemplazar lo anterior. |
| Clave de idempotencia vacía, repetida, inválida o UUID cero | 400. |
| Mismos PDF/clave ya confirmados | 200; un archivo. |
| Misma clave del PDF con otros bytes | 409, sin reemplazar el archivo. |
| PDF mayor de 2 MiB | 413, sin almacenarlo; el texto permanece. |
| Archivo no PDF o formulario con varios archivos/campos | 400, sin almacenarlo. Probar en una solicitud pendiente sin adjunto. |
| Intentar un adjunto nuevo en solicitud ya gestionada o de más de 24 horas | 409. |
| Antivirus no disponible | 503, sin aceptar una nueva carga; el texto permanece. |
| Tres altas válidas por cuenta/24 h y una cuarta con clave nueva | 429; un reintento de una ya confirmada conserva 200 con su clave. |
| Más de tres intentos nuevos de PDF por cuenta/24 h | 429. Los intentos fallidos también pueden consumir cuota. |

Para cubrir los cinco motivos sin violar la cuota, A puede crear **proyecto,
sugerencia y duda**; B, **voluntariado y denuncia**, cada operación con clave propia.
En A, la cuarta alta nueva debe dar 429. No repetir estos cinco casos sobre A en
la misma ventana esperando cinco altas correctas.

Los límites globales también pueden producir 429, y alcanzar la capacidad total
de adjuntos puede producir 503. Las pruebas de archivo inválido/tamaño/antivirus
deben usar fixtures separados si ya consumieron los tres intentos permitidos.
No hace falta enviar malware real para comprobar el recorrido funcional del PDF.

### E. Notificaciones y consumo

Comprobar los eventos actuales: verificación, bienvenida al confirmar, aprobación
de afiliación, recuperación y aviso de contraseña actualizada. Revisar que un
reintento de confirmación/aprobación rechazado no cree otro evento de bienvenida
o aprobación. La entrega del proveedor no tiene una garantía absoluta de una sola
copia ante fallos ambiguos de red.

Guardar y entregar son hechos distintos: 202/204 confirma la operación indicada,
no que Gmail ya entregó el correo. Registrar también su llegada o el estado del
trabajo. No repetir registro/aprobación en bucle para intentar forzar envíos.
Las notificaciones segmentadas por ciudad/candidato no forman parte de estos
correos transaccionales implementados.

Los GET privados deben responder `Cache-Control: no-store`. Las páginas editoriales
de Vercel no necesitan estas llamadas. Probar límites HTTP de forma controlada
debe devolver 429, con espera indicada, manteniendo recuperable la API. La
medición de p95, conexiones, memoria y picos se hace en una prueba de carga
separada; este caso no define todavía capacidad de producción.

## Criterio de aceptación y registro de resultados

Marcar como correcto únicamente después de observar cada resultado:

- [ ] Registro/verificación/login/perfil/PATCH de A completados.
- [ ] Cuenta repetida no duplica ni reemplaza datos.
- [ ] Afiliación de A aprobada y de B rechazada; estados propios correctos.
- [ ] Totales administrativos coherentes y contacto disponible para ADM.
- [ ] Encuesta respondida una sola vez; comprobante recuperable tras cierre.
- [ ] Solicitud/PDF guardados, recuperados por idempotencia y descargados.
- [ ] Respuesta administrativa visible para A; revisión vieja rechazada.
- [ ] B no accede a expedientes/comprobantes de A ni ejerce permisos de ADM.
- [ ] Recuperación cambia contraseña y revoca JWT anteriores aún no vencidos.
- [ ] Cierre de sesión revoca ambos JWT; permite volver a iniciar sesión.
- [ ] Baja final impide acceso y no reactiva la cuenta por registro/recuperación.
- [ ] Correos de cada evento comprobados por separado de los códigos HTTP.
- [ ] Casos inválidos/cuotas no guardan datos fuera del contrato ni detienen la API.

Plantilla de evidencia, sin contraseñas, códigos, cookies ni JWT:

| Caso/paso | Cuenta | Método y ruta | HTTP esperado | HTTP observado | Estado/dato comprobado | Resultado / traceId si falló |
| --- | --- | --- | --- | --- | --- | --- |
| CU-U01 / 2 | A | POST /api/Usuario | 202 | Pendiente | Alta y verificación pendientes de comprobar | Pendiente |

Anotar fecha, versión/commit y entorno de ejecución; conservar IDs de fixtures
para seguir cada expediente. No inferir éxito solo por un 2xx: comprobar la lectura
posterior, el rechazo del duplicado y el efecto sobre la sesión donde corresponda.
No publicar capturas de Swagger que contengan Authorization o códigos del correo.

La regresión automatizada del paso 6, repetida el 23 de octubre antes del commit,
aprobó 846 comprobaciones HTTP/PostgreSQL, 72 de consumo y 45 de correo simulado
(963 en total). Usa una base temporal y transportes de correo/antivirus simulados;
no envía correos reales. El estado informado de la prueba manual está al inicio
de esta guía; la integración y prueba del PDF en Azure continúan pendientes.
