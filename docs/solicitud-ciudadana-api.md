# Solicitud ciudadana y PDF

## Antes de probar

1. Detener la API y ejecutar una vez `database/004_solicitudes_ciudadanas.sql`,
   después de 001–003. No fue aplicado a la base habitual. Los mapeos ya están
   actualizados; no hace falta scaffolding.
2. Iniciar Presentation en Debug. La creación de texto y la gestión funcionan
   independientemente del antivirus.
3. Para adjuntar PDF hace falta un servicio **ClamAV/clamd real**, con firmas
   actualizadas. Configurar `PdfAntivirus__Host` y `PdfAntivirus__Port` (3310).
   También se pueden definir en `appsettings.Local.json`. Si no está configurado,
   falla, agota el tiempo o devuelve un resultado desconocido, la carga devuelve
   503 y **no guarda el archivo**. No hay bypass de antivirus en Development.

La API implementa el protocolo INSTREAM y envía bytes, nunca rutas del equipo del
usuario. El servidor no se elige desde el request. Ver la
[documentación oficial del protocolo](https://docs.clamav.net/manual/Usage/ClamdProtocol.html).
Mantener clamd en loopback o en una red privada aislada: este protocolo TCP no
aporta autenticación ni cifrado. No publicar el puerto en Internet.

Incorporar `deployment/clamd-limits.conf.example` a la configuración del servicio.
Es un fragmento, no un instalador: completar las rutas de firmas y permisos del
entorno, ejecutar la actualización de firmas y supervisar su antigüedad. Se activa
análisis PDF, rechazo de documentos cifrados y alertas al exceder límites de
análisis. Los parámetros se basan en la
[configuración oficial de ClamAV](https://github.com/Cisco-Talos/clamav/blob/main/etc/clamd.conf.sample).
La espera de la API se limita a 10 segundos; el proceso antivirus requiere además
límites propios de CPU/memoria en el despliegue. Cerrar la conexión no garantiza
que un motor externo interrumpa instantáneamente su trabajo.

## Usuario: crear y adjuntar

Con el JWT obtenido en login, usar Authorize en Swagger.

`POST /api/SolicitudCiudadana`:

```json
{
  "motivo": "proyecto",
  "mensaje": "Presento una propuesta formal y adjuntaré el documento con sus detalles."
}
```

Motivos permitidos: `proyecto`, `sugerencia`, `duda`, `voluntariado`, `denuncia`.
El mensaje tiene entre 10 y 5000 caracteres. No se admiten usuarioId, estado,
aceptacion ni pdfUrl enviados por el cliente. Se devuelve 201 con el ID y el
estado `recibida` del acuse; en el expediente se representa como `pendiente`.
El endpoint anterior `POST /api/Usuario/me/solicitudes` sigue disponible y aplica
las mismas cuotas, porque se controlan al insertar en PostgreSQL.

Opcionalmente, `POST /api/SolicitudCiudadana/{id}/pdf`, multipart/form-data:

- Un único campo de archivo llamado **archivo**, tipo `application/pdf`.
- Un PDF de hasta **2 MiB = 2.097.152 bytes**.
- Solo el propietario puede adjuntarlo; la solicitud debe estar pendiente y tener
  menos de 24 horas. No se puede reemplazar un adjunto ya aceptado.
- Se valida extensión, MIME, encabezado y cierre; después se exige un resultado
  limpio del antivirus. El filtro de encabezado/cierre no es un parser PDF completo
  y un antivirus no garantiza que un documento sea inocuo.
- El nombre original no se usa como ruta ni se publica. Se asigna un nombre de
  descarga basado en el UUID. La carga devuelve 201 tras persistir el adjunto.

La creación del texto es independiente de la carga: un PDF rechazado no borra
la solicitud. No crear otra solicitud para reintentar una carga.

## Consultar y gestionar

| Operación | Acceso | Resultado |
| --- | --- | --- |
| `GET /api/SolicitudCiudadana` | Usuario | Solo solicitudes propias |
| `GET /api/SolicitudCiudadana/{id}` | Propietario o administrador | Mensaje, estado, revisión, respuesta y presencia de PDF |
| `GET /api/SolicitudCiudadana/{id}/pdf` | Propietario o administrador | Descarga del PDF aceptado |
| `GET /api/SolicitudCiudadana/administracion` | Admin/SuperAdmin | Bandeja de solicitudes |
| `PATCH /api/SolicitudCiudadana/{id}/gestion` | Admin/SuperAdmin | Gestión con control de revisión |

Los listados admiten `limite=20` (máximo 50), `cursor=<uuid>` y `estado` opcional.
Devuelven `items` y `siguienteCursor`; null indica fin. Orden por UUID ascendente,
sin COUNT total, OFFSET creciente, textos completos ni bytes de PDF. Para registros
nuevos durante una navegación, comenzar otra consulta; no es un snapshot.

Ejemplo de gestión:

```json
{
  "revision": 1,
  "estado": "en_revision",
  "respuesta": "La propuesta está siendo evaluada por el equipo."
}
```

Estados: `pendiente` → `en_revision` → `resuelta` o `rechazada`. También se puede
finalizar directamente desde pendiente. Para finalizar es obligatoria una respuesta
de 10–5000 caracteres. No se reabren solicitudes finalizadas. Cada gestión incrementa
`revision`; volver a consultar antes de otra modificación. Una revisión obsoleta
devuelve 409. Se registra el administrador de la última gestión y updated_at.
El usuario ve la respuesta al consultar su expediente; no se envían emails aún.

## Protección y capacidad

- **3 solicitudes por usuario en una ventana móvil de 24 horas**, persistente y
  atómica. Se mantienen al reiniciar y se comparten entre instancias.
- **3 intentos de carga PDF por usuario/24 h**, incluso si el formulario es inválido,
  falla el antivirus o el archivo es rechazado. La reserva ocurre después de
  autorizar la propiedad y antes de leer/bufferizar el multipart.
- Topes globales iniciales: **1000 solicitudes/24 h**, **100 intentos PDF/24 h** y
  **100 MiB de contenido PDF almacenado**. Están fijados en el script SQL; revisarlos
  según capacidad real antes de ampliarlos. Si se alcanza almacenamiento, se
  rechazan nuevos adjuntos con 503; no se borran expedientes automáticamente.
- El límite de 100 MiB mide contenido, no todo el espacio físico: PostgreSQL
  utiliza además índices, TOAST, WAL y backups. El texto se acumula; definir una
  política de retención y monitorizar disco antes del despliegue.
- 20 transferencias PDF/minuto por cuenta e instancia, además del límite por IP.
  Las cuotas persistentes de creación/carga cubren también múltiples instancias.
- Máximo 2 análisis simultáneos por instancia, sin cola; espera antivirus de 10 s.
  La carga completa dispone de una cancelación a los 30 s.
- Cuerpo multipart máximo 2 MiB + 16 KiB de estructura. Los demás endpoints siguen
  limitados a 64 KiB. Se conservan los controles globales de concurrencia/frecuencia.
- Descarga autenticada con `Content-Disposition: attachment`, `nosniff`, `no-store`
  y CSP restrictiva; sin enlaces públicos, sin previsualización y sin rangos.
- Hash SHA-256 y unicidad por usuario evitan almacenar el mismo archivo repetido
  en varias solicitudes. Las solicitudes ajenas devuelven 404.

Los PDF se almacenan como bytea en una tabla separada bajo el techo indicado. Esto
permite confirmar archivo, unicidad, reserva y cuota en una transacción, sin archivos
huérfanos ni almacenamiento público. Las lecturas generales no cargan esa columna.
Para un volumen mayor, el almacenamiento puede extraerse a un servicio privado
desde el repositorio; requerirá coordinar cuotas, limpieza y persistencia externa.
Los valores antiguos de `pdf_url` se conservan, pero no se descargan ni se siguen
automáticamente: no son adjuntos verificados por este flujo.

No hay protección absoluta frente a DDoS: los límites de aplicación no impiden que
tráfico rechazado llegue a la infraestructura. El despliegue debe limitar tráfico
también antes de la API y vigilar consumo y almacenamiento.

## Pruebas

`scripts/verify-usuario.ps1` aplica 001–004 sobre una base temporal y ejecuta los
casos de `scripts/verify-solicitud-cases.ps1` junto con las regresiones previas.
Comprueba acceso ajeno, permisos de gestión, paginación, cuotas, altas concurrentes,
archivos sobredimensionados, múltiples archivos, formato inválido, descarga y
rechazo/indisponibilidad del scanner.

`scripts/test-clamd-protocol.ps1` es un **simulador exclusivo de pruebas**, no un
antivirus. Estas pruebas verifican el cliente y sus decisiones de seguridad, no
la detección real de malware. Antes del despliegue, probar el ClamAV real con firmas
actualizadas y archivos de prueba de su proveedor. El simulador no se registra en
DI ni se activa desde la API y no debe utilizarse como servicio de producción.

