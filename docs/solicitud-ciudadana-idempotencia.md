# Paso 4: solicitudes, reintentos y PDF

20 de octubre de 2026. Se completa el flujo de la API con idempotencia persistente y recuperación del resultado. La prueba general por entidad de la aplicación completa se realizará al terminar las entregas, como acordado.

## Preparación

1. Detener la API.
2. Ejecutar **una sola vez** `database/008_solicitudes_idempotencia.sql` en pgAdmin, después de 001–007. No reejecutar las migraciones anteriores. El script es transaccional, añade columnas/índice y funciones; conserva las solicitudes y archivos existentes.
3. Recompilar y ejecutar `Presentation`.
4. Para una carga nueva se mantiene ClamAV real disponible y configurado. La solicitud de texto funciona sin el antivirus. El simulador de `scripts` es exclusivo de las pruebas automatizadas.

No se incorpora otro DbContext, proveedor de caché, worker, sondeo ni paquete NuGet. La base habitual no se migra automáticamente desde las pruebas.

## Crear una solicitud y recuperar su resultado

Con el JWT del usuario, POST `/api/SolicitudCiudadana`. En Swagger aparece el header opcional **Idempotency-Key**. Para este flujo usar siempre un UUID nuevo por operación; se puede generar en PowerShell:

```powershell
[guid]::NewGuid().ToString()
```

Conservar ese UUID mientras se confirma esa solicitud. Cuerpo:

```json
{
  "motivo": "proyecto",
  "mensaje": "Presento una propuesta formal para mejorar los espacios públicos de mi localidad."
}
```

- Primera confirmación: **201**, `Location` del detalle y `{ "id": "...", "estado": "recibida" }`.
- Mismo usuario, clave y datos normalizados: **200**, mismo acuse e ID, sin insertar ni consumir otro cupo de solicitudes. Se recortan únicamente los espacios exteriores del mensaje.
- Misma clave con otro motivo o mensaje: **409**, `code: "idempotency_conflict"`. La operación anterior permanece intacta.
- Otra cuenta puede usar el mismo UUID sin obtener el expediente de la primera; la clave está vinculada al usuario.
- La resolución administrativa posterior no cambia el acuse de creación. Consultar el detalle para conocer el estado actual; repetir el POST no reabre la solicitud.

El alias POST `/api/Usuario/me/solicitudes` utiliza la misma lógica y el mismo ámbito de claves. Reintentar por otra ruta no crea otro expediente. La clave y la huella quedan en la fila de solicitud, con índice único por usuario; no se añade un registro por cada intento HTTP. Persisten al reiniciar la API y entre instancias conectadas a la misma base. Se conservan mientras exista la solicitud.

Por compatibilidad, **sin header se mantiene el comportamiento anterior**: cada POST aceptado representa una solicitud nueva y consume cuota. No se garantiza idempotencia si el cliente omite o cambia la clave en cada reintento. Un header vacío, repetido, inválido o UUID de ceros devuelve 400.

Los motivos y límites existentes se mantienen: `proyecto`, `sugerencia`, `duda`, `voluntariado`, `denuncia`; mensaje de 10–5000 caracteres, sin NUL. Hasta tres solicitudes por cuenta/24 horas y mil globales/24 horas. Una solicitud ya confirmada puede recuperarse con su clave aunque se haya agotado el cupo para nuevas altas. Cada petición conserva los límites HTTP por IP, escritura y concurrencia; idempotencia no los omite.

## Adjuntar un PDF

Guardar el `id` de la solicitud creada. POST `/api/SolicitudCiudadana/{id}/pdf`, `multipart/form-data`, un único archivo en el campo `archivo`. Usar **otra Idempotency-Key** para la operación del PDF y conservarla si se reintenta el mismo archivo.

- Carga nueva válida y limpia: **201**, `{ "estado": "disponible" }`.
- Misma clave y bytes de una carga confirmada: **200**, mismo resultado. Se compara longitud y SHA-256 del archivo recibido; no se crea otra reserva, no se vuelve a analizar ni guardar el PDF. El request sigue leyendo hasta 2 MiB y mantiene los límites de transferencia.
- Misma clave con contenido distinto: **409**. Nunca reemplaza el PDF confirmado.
- Otra clave, o sin clave, cuando ya hay un PDF: **409**. Los adjuntos históricos sin clave se consultan por GET; no se les atribuye una clave retroactivamente.
- Misma clave mientras una carga anterior sigue reservada: **409**, `code: "upload_in_progress"`, `Retry-After: 30`. Consultar el detalle antes de reintentar, sin abrir un bucle automático.

En una falla de formato, antivirus, cancelación o guardado, el intento de carga sigue consumiendo cuota. La API procura cerrar la reserva fallida con una operación de limpieza de hasta dos segundos, independiente de la desconexión del cliente. Si se pierde el proceso/DB, la exclusión temporal de esa clave caduca a los cinco minutos. Un reintento aún no confirmado puede consumir otro intento; solo los resultados confirmados se recuperan sin otro cupo.

Se mantienen PDF de hasta **2 MiB**, un archivo por solicitud, propietario autenticado, solicitud pendiente y con menos de 24 horas para una carga nueva. Tres intentos PDF por cuenta/24 horas, cien globales y cien MiB de contenido almacenado. Un reintento confirmado continúa funcionando aunque el expediente se haya gestionado, haya vencido la ventana para adjuntar o esté agotada la cuota; no añade contenido.

La validación de formato y el resultado limpio del antivirus son obligatorios para cargas nuevas. Sin antivirus, una carga nueva devuelve 503; repetir una carga ya confirmada verifica los bytes y puede devolver 200 sin contactar al scanner. Los nombres originales no se convierten en rutas. La descarga mantiene autorización, `attachment`, `nosniff`, `no-store` y CSP restrictiva.

## Consultar el resultado sin descargar el archivo

GET `/api/SolicitudCiudadana/{id}` sigue limitado al propietario o administrador. Conserva `tienePdf` y añade:

```json
{
  "estadoPdf": "disponible",
  "pdfBytes": 1024,
  "pdfSha256": "HUELLA_SHA256_REAL_DE_64_CARACTERES"
}
```

`estadoPdf` vale `sin_adjunto` mientras no exista un PDF confirmado; bytes y huella son null. No se persisten archivos pendientes de análisis ni se declara disponible un archivo por haber iniciado su subida. Ante 503 o pérdida de conexión, consultar este detalle: el mensaje conserva su ID y estado, incluso si falla el PDF. No volver a crear la solicitud para reintentar el adjunto.

Detalle, listados y reservas consultan solo metadatos del archivo; los bytes se leen de DB únicamente al descargar. El listado permanece paginado sin textos completos ni bytes. Las reglas de gestión y revisión administrativa no cambian.

## Secuencia de pruebas en Swagger

1. Crear una solicitud con una clave UUID y guardar la respuesta 201.
2. Repetir el mismo POST con la misma clave: esperar 200, mismo ID y acuse.
3. Cambiar el mensaje manteniendo la clave: esperar 409. Consultar el expediente y confirmar que conserva el mensaje original.
4. Con el ID original, cargar un PDF limpio usando una segunda clave: esperar 201.
5. Repetir el mismo archivo y clave: esperar 200. Cambiar los bytes conservando la clave: esperar 409.
6. Consultar el detalle y comprobar `tienePdf`, `estadoPdf`, tamaño y huella. Descargar desde el endpoint autenticado; no hay URL pública.
7. Probar con otro usuario: el detalle/descarga/carga de un expediente ajeno devuelve 404.
8. En otra solicitud de prueba, detener el antivirus y cargar un PDF nuevo: esperar 503. El detalle conserva el mensaje, `estadoPdf=sin_adjunto`. Restablecer el antivirus y reintentar el mismo adjunto y clave dentro de la cuota.

El registro/login conserva los requisitos del paso 2. Las pruebas de cuota deben hacerse con cuentas de prueba: las altas e intentos fallidos se contabilizan durante 24 horas. Conservar los borradores y mostrar el error; no limpiar el formulario antes de confirmar el resultado.

## Contrato para React

Generar la clave con `crypto.randomUUID()` una sola vez al confirmar una operación. Conservarla con ese borrador mientras se resuelve su resultado, sin credenciales ni contenido personal en URLs. Mantener claves separadas para texto y PDF. Si se pierde la respuesta, conservar clave y payload; no crear automáticamente una operación nueva.

Enviar JSON para el texto. Para el PDF usar `FormData` con `archivo` y añadir el header de idempotencia; el navegador define el boundary y `Content-Type`. Un 201/200 confirma esa etapa. Ante 401, recuperar sesión; ante 409, consultar el expediente y distinguir conflicto de datos/carga en curso; ante 429/503, conservar los datos y respetar espera. No repetir la creación del texto por un error del archivo.

La clave no es una credencial ni permite consultar un expediente sin JWT. El servidor deriva el usuario de la sesión y no admite `usuarioId`, estado, URL del archivo o hash elegidos por el cliente en el cuerpo de creación.

## Almacenamiento y preparación de producción

Esta entrega conserva `bytea` privado y separado, ClamAV y los topes actuales; operaciones de reserva/guardado son breves y el análisis ocurre fuera de la transacción. No hay archivos sin resultado limpio ni contenido huérfano en un segundo almacenamiento. La huella del PDF también se contrasta con los bytes en PostgreSQL antes de guardar.

La integración elegida de Blob Storage privado y Defender for Storage corresponde a la preparación de Azure del **paso 8**, antes de desplegar con ese proveedor. Todavía no hay un adaptador Blob ni receptor de resultados Defender implementados; no se afirma que Defender analice los bytes guardados únicamente en PostgreSQL. En esa entrega habrá que coordinar almacenamiento externo, versión exacta, estado de análisis y descarga autorizada. El despliegue sigue pendiente y no se han creado recursos de Azure.

## Pruebas de esta entrega

La suite temporal aplica 008 e incluye `scripts/verify-solicitud-idempotencia-cases.ps1`, además de las regresiones anteriores. Comprueba claves/datos, alias, normalización, concurrencia, cuotas, aislamiento de cuentas, PDF repetido sin antivirus, contenido distinto, reserva en curso, recuperación tras fallo de scanner, metadatos y persistencia después de reiniciar. Los datos y servicios de análisis/correo son de prueba.

Resultado final: compilación Release con **0 errores y 0 advertencias**; **658 comprobaciones HTTP/PostgreSQL**, incluidas 80 del flujo idempotente JSON/PDF y preservación de datos anteriores al aplicar 008; **62 de consumo/worker/caché** y **45 de correo/dispatcher**. Total: **765 comprobaciones correctas**. También se comprobó rollback de una inserción fallida, contrato de headers en Swagger y persistencia tras reiniciar. PostgreSQL temporal quedó detenido y no se enviaron correos reales. Gitleaks Worktree terminó sin hallazgos.

Referencias: [bloqueos en PostgreSQL 16](https://www.postgresql.org/docs/16/explicit-locking.html), [carga de archivos en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0), [protecciones de carga OWASP](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html).
