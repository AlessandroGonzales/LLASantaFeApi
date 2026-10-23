# Solicitudes de afiliación

Paso 5, 21 de octubre de 2026. Se completa el contrato de backend para React y la resolución administrativa; las pantallas se conectarán en la entrega del frontend.

## Preparación

La API actual requiere 001–009. Si ya están 001–008, detener la API y ejecutar **una sola vez** `database/009_afiliacion_resolucion.sql` en pgAdmin. Recompilar y ejecutar `Presentation`. No reejecutar migraciones aplicadas; no se requiere scaffolding ni otro DbContext. La base habitual no se migra automáticamente desde las pruebas.

Se conserva la aprobación auditada de 005 y el índice `(estado,id)`. El script 009 añade `rechazada_at`, `rechazada_por` y `motivo_rechazo`, con su FK y reglas de consistencia. Preserva estados y auditorías existentes; los rechazos históricos quedan sin fecha/autor/motivo si no se conocían. No inventa información ni genera notificaciones de decisiones históricas. Las resoluciones nuevas quedan auditadas y son definitivas.

## Endpoints

| Método y ruta | Acceso | Función |
| --- | --- | --- |
| `POST /api/SolicitudAfiliacion` | Usuario autenticado y activo | Enviar su solicitud |
| `GET /api/SolicitudAfiliacion/me` | Usuario autenticado | Consultar su propio estado |
| `GET /api/SolicitudAfiliacion/pendientes` | Admin/SuperAdmin | Bandeja paginada; `estado=pendiente` por defecto, o `en_revision` |
| `GET /api/SolicitudAfiliacion/{id}` | Admin/SuperAdmin | Detalle y datos de contacto |
| `POST /api/SolicitudAfiliacion/{id}/aprobar` | Admin/SuperAdmin | Aprobar una solicitud |
| `POST /api/SolicitudAfiliacion/{id}/rechazar` | Admin/SuperAdmin | Rechazar con motivo público y auditoría |
| `GET /api/SolicitudAfiliacion/totales` | Admin/SuperAdmin | Contadores agregados |

Se conserva `POST /api/Usuario/me/afiliacion` por compatibilidad. Ambos POST usan
el mismo servicio y la misma restricción única de PostgreSQL: no permiten enviar
dos solicitudes de afiliación por usuario, tampoco después de una resolución.
Esta unicidad hace innecesaria otra tabla/clave de idempotencia para afiliación: ante un timeout o 409 de creación, consultar `/me` para recuperar su ID y estado. Los reintentos no crean otro registro. Una resolución repetida devuelve 409 y conserva la decisión anterior; consultarla antes de informar el resultado al usuario.

## Prueba en Swagger

1. Iniciar Presentation en Debug y abrir Swagger.
2. Iniciar sesión con un usuario registrado en `POST /api/Usuario/login`.
3. Copiar `token` en Authorize, sin escribir `Bearer`.
4. Ejecutar `POST /api/SolicitudAfiliacion`:

```json
{
  "confirmacion": true
}
```

La respuesta es 201, con `id` y `estado: "pendiente"`. No enviar usuarioId, rol,
estado ni datos personales: la identidad se obtiene del JWT y los datos del perfil.
`confirmacion: false` devuelve 400; una segunda solicitud devuelve 409.

5. Consultar `GET /api/SolicitudAfiliacion/me`. Devuelve ID, estado, fecha de solicitud, `aprobadaAt`, `rechazadaAt` y `motivoRechazo` según corresponda. Sin solicitud devuelve 404. No expone observaciones internas, identidad del administrador ni solicitudes de otras personas. Un parámetro `id`/`usuarioId` no permite elegir otra cuenta.
6. Iniciar sesión con una cuenta que ya tenga rol **Admin o SuperAdmin** y cambiar
   el token en Authorize. El registro público no concede esos roles.
7. Consultar `GET /api/SolicitudAfiliacion/pendientes?limite=20`. Para registros heredados en revisión, usar `?estado=en_revision&limite=20`. Estados finales o filtros inválidos devuelven 400; no se añade una interfaz genérica de edición de estados.
8. Usar el ID de una solicitud en `GET /api/SolicitudAfiliacion/{id}` para revisar
   nombre, apellido, email, teléfono, estado de cuenta y datos de la solicitud. `emailVerificado` refleja la verificación real; `requiereVerificarEmail` identifica las cuentas sometidas a ese requisito. El teléfono sigue siendo opcional y no tiene verificación implementada.
9. Aprobar mediante `POST /api/SolicitudAfiliacion/{id}/aprobar`:

```json
{
  "observacion": "Datos revisados por el administrador."
}
```

También se admite `{}`: la observación es opcional y tiene un máximo de 2000
caracteres. Se devuelve **204 No Content**. Consultar nuevamente el detalle para
ver `estado: "aprobada"`, `aprobadaAt` y `aprobadaPor`.

Solo se aprueban solicitudes pendientes o en_revision, con una cuenta activa.
Cuando la cuenta requiere verificar email, la verificación también debe estar completa. Las cuentas anteriores conservan la transición definida en el paso 2; no se marcan verificadas automáticamente. La DB vuelve a comprobar al administrador activo y su rol al ejecutar la resolución, y valida la cuenta destinataria antes de aprobar.
Una solicitud rechazada, ya aprobada o cuya cuenta esté inactiva devuelve 409;
un ID inexistente devuelve 404. Dos aprobaciones simultáneas producen una sola
transición: una responde 204 y la otra 409. La aprobación no cambia el rol del usuario.

10. El administrador puede usar email/teléfono del detalle para contactar personalmente. Los datos corresponden al perfil actual; consultar el detalle no registra ni demuestra que hubo contacto. La aprobación encola el aviso Gmail existente en la misma transacción y despierta el worker después de confirmar. Un reintento no crea otro trabajo de correo. La entrega depende de Gmail, sus cuotas y su configuración; 204 confirma el estado y la cola, no la recepción en bandeja.
11. Consultar `GET /api/SolicitudAfiliacion/totales`:

```json
{
  "totalAfiliados": 1,
  "totalSolicitudes": 3,
  "pendientes": 1,
  "enRevision": 0,
  "rechazadas": 1
}
```

**totalAfiliados cuenta exclusivamente solicitudes con estado aprobada en el
sistema.** totalSolicitudes incluye todos los estados. Los contadores parten de
cero si la tabla está vacía. Una baja lógica de la cuenta no elimina la afiliación
aprobada: son estados distintos y el total conserva ese registro.

## Rechazar y consultar el motivo

El rechazo permite cerrar solicitudes que no deben aprobarse, incluidas las pendientes de cuentas dadas de baja. Lo realiza exclusivamente un administrador vigente. No exige que el solicitante siga activo; la aprobación sí lo exige.

POST `/api/SolicitudAfiliacion/{id}/rechazar`:

```json
{
  "motivo": "La solicitud no cumple las condiciones informadas para este trámite."
}
```

Motivo obligatorio de 10–2000 caracteres, recortando espacios exteriores y sin NUL. Es visible para el solicitante; no colocar observaciones privadas de otras personas. `observacion` es un campo interno del flujo de aprobación y no se reutiliza como motivo público.

Respuesta **204**. El detalle administrativo registra `estado=rechazada`, `rechazadaAt`, `rechazadaPor` y `motivoRechazo`; el usuario ve fecha/motivo mediante `/me`. Los campos de aprobación permanecen nulos. Una resolución ya aprobada o rechazada devuelve 409; ID inexistente, 404; sin token, 401; usuario común, 403. El cliente no envía autor, fecha, usuario objetivo ni estado arbitrario.

Una carrera entre aprobar y rechazar produce una sola decisión: 204 para quien confirme primero y 409 para la otra operación. Solo una aprobación encola su aviso. El rechazo se consulta desde la cuenta; esta versión no añade otro tipo de correo. Una solicitud rechazada continúa siendo la única de esa cuenta y no se reabre ni se borra.

Prueba adicional en Swagger: crear una solicitud con otra cuenta de prueba, rechazarla como administrador, volver a consultar `/me` con el token del solicitante y comprobar motivo/fecha. Repetir aprobación/rechazo o crear otra solicitud debe devolver 409. Usar cuentas diferentes para probar aprobación y rechazo, porque ambos resultados son definitivos.

## Contrato de consumo para React

Consultar `/me` al abrir la sección de afiliación, compartiendo el resultado durante esa sesión. Con 404, mostrar la confirmación para solicitar; con `pendiente`/`en_revision`, mostrar que sigue en trámite; con `aprobada`/`rechazada`, mostrar el resultado y su fecha/motivo disponible. Un rechazo histórico puede no tener motivo ni auditoría: mostrar el estado sin inventar datos.

Enviar únicamente `{ "confirmacion": true }`. Actualizar la consulta propia después de crear; ante timeout/409, recuperar `/me` en lugar de insistir con nuevas altas. Un fallo de red no demuestra que la operación no se guardó. No consultar estados mediante polling ni desde la portada pública.

En administración, cargar la bandeja paginada y abrir contacto solo al seleccionar una solicitud. Tras resolver, refrescar el detalle/bandeja y consultar totales cuando se necesiten. No repetir el agregado por cada fila. Un 409 de resolución obliga a leer la decisión vigente; 429 respeta `Retry-After` si está presente, sin bucles automáticos. Mostrar motivos y observaciones como texto.

El formulario público legado `/sumate` sigue siendo contacto/interés de voluntariado. Este paso no lo convierte en una afiliación ni importa datos de Google Sheets. Los contadores describen estados registrados en la aplicación.

## Seguridad y consultas

- Los datos de contacto y los totales solo se entregan a Admin/SuperAdmin.
- Los listados no incluyen email/teléfono: se consultan en el detalle autorizado.
- La bandeja separa pendiente y en_revision con el mismo índice y paginación; conserva el indicador usuarioActivo. El detalle entrega contactos bajo autorización, sin convertirlos en datos públicos.
- Paginación por cursor UUID ascendente, `limite` entre 1 y 50, por defecto 20.
  Respuesta: `items` y `siguienteCursor`; null significa fin. No es un orden por
  fecha ni una instantánea de registros que cambian durante la navegación.
- Queries AsNoTracking, proyecciones de campos, sin cargar colecciones completas.
  El índice `(estado,id)` soporta la bandeja; los totales se calculan en una sola
  consulta agregada. No se mantienen contadores manuales que puedan duplicarse.
- Se mantienen autenticación JWT, revocación, límite de cuerpo, rate limiting,
  concurrencia global y cancelación. Las respuestas usan no-store.
- Aprobación y rechazo ejecutan primero la escritura condicionada al estado. Una resolución exitosa evita cargar previamente el contacto; solo si no cambia ninguna fila se consulta existencia para distinguir 404 de 409. Los controles de autor/cuenta y las fechas están centralizados en el trigger transaccional.
- Se conserva una sola restricción de solicitud por cuenta y un solo trigger de resolución. No se añade historial redundante ni contadores manuales, paquetes o workers. Las notas internas no forman parte de la respuesta propia.

## Pruebas

`scripts/verify-usuario.ps1` restaura el esquema original en una base temporal,
aplica 001–009 y ejecuta `scripts/verify-afiliacion-cases.ps1` y `verify-afiliacion-cierre-cases.ps1` junto con las regresiones anteriores. Comprueba preservación de registros anteriores al aplicar 009, permisos, unicidad entre rutas, estados propios, privacidad, contacto, ambas bandejas, cuentas inactivas/email pendiente, rechazo auditado, carrera aprobar/rechazar y rollback de aprobación cuando falla la cola. Gmail se simula/deshabilita; no se envían correos reales.

`scripts/ConsumptionChecks` verifica además que resolver no cargue contactos innecesarios y que solo la aprobación exitosa despierte al worker. La prueba general de todas las entidades con frontend sigue programada para el final de las entregas; estas son comprobaciones de esta etapa.

Resultado final: compilación Release con **0 errores y 0 advertencias**; **770 comprobaciones HTTP/PostgreSQL**, incluidas 107 del cierre de Afiliación; **69 de consumo/servicios/worker/caché** y **45 de correo/dispatcher**. Total: **884 comprobaciones correctas**. PostgreSQL temporal quedó detenido y no se enviaron correos reales. La revisión de secretos del árbol de trabajo se realiza antes de entregar.

Referencias: [autorización OWASP](https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html) y [concurrencia de UPDATE en PostgreSQL 16](https://www.postgresql.org/docs/16/transaction-iso.html).
