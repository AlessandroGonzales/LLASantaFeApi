# Solicitudes de afiliación

## Preparación

Detener la API y ejecutar una vez `database/005_afiliaciones.sql`, después de
001–004. La migración se probó sobre PostgreSQL temporal; no se aplicó a la base
habitual. El mapeo EF está actualizado y no requiere scaffolding.

El script agrega `aprobada_at`, `aprobada_por` y un índice `(estado,id)`. Preserva
las solicitudes existentes. En aprobaciones históricas esos campos permanecen
nulos porque no se conoce su autor ni fecha; no se inventan esos datos.
Las nuevas aprobaciones quedan auditadas y las solicitudes resueltas no se reabren.

## Endpoints

| Método y ruta | Acceso | Función |
| --- | --- | --- |
| `POST /api/SolicitudAfiliacion` | Usuario autenticado y activo | Enviar su solicitud |
| `GET /api/SolicitudAfiliacion/me` | Usuario autenticado | Consultar su propio estado |
| `GET /api/SolicitudAfiliacion/pendientes` | Admin/SuperAdmin | Bandeja paginada de pendientes |
| `GET /api/SolicitudAfiliacion/{id}` | Admin/SuperAdmin | Detalle y datos de contacto |
| `POST /api/SolicitudAfiliacion/{id}/aprobar` | Admin/SuperAdmin | Aprobar una solicitud |
| `GET /api/SolicitudAfiliacion/totales` | Admin/SuperAdmin | Contadores agregados |

Se conserva `POST /api/Usuario/me/afiliacion` por compatibilidad. Ambos POST usan
el mismo servicio y la misma restricción única de PostgreSQL: no permiten enviar
dos solicitudes de afiliación por usuario, tampoco después de una resolución.

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

5. Consultar `GET /api/SolicitudAfiliacion/me`. Devuelve el ID, estado, fecha de
   solicitud y fecha de aprobación cuando corresponde. Si no tiene solicitud,
   devuelve 404. No expone observaciones internas ni solicitudes de otras personas.
6. Iniciar sesión con una cuenta que ya tenga rol **Admin o SuperAdmin** y cambiar
   el token en Authorize. El registro público no concede esos roles.
7. Consultar `GET /api/SolicitudAfiliacion/pendientes?limite=20`.
8. Usar el ID de una solicitud en `GET /api/SolicitudAfiliacion/{id}` para revisar
   nombre, apellido, email, teléfono, estado de cuenta y datos de la solicitud.
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
Una solicitud rechazada, ya aprobada o cuya cuenta esté inactiva devuelve 409;
un ID inexistente devuelve 404. Dos aprobaciones simultáneas producen una sola
transición: una responde 204 y la otra 409. La aprobación no cambia el rol del usuario.

10. El administrador puede usar email/teléfono del detalle para contactar a la
    persona. El teléfono es opcional; puede ser null. Los datos corresponden al
    perfil actual y no implican que el canal esté verificado. Este flujo **no envía
    mensajes automáticamente** ni comprueba que el administrador haya contactado.
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

## Seguridad y consultas

- Los datos de contacto y los totales solo se entregan a Admin/SuperAdmin.
- Los listados no incluyen email/teléfono: se consultan en el detalle autorizado.
- La bandeja muestra solo estado pendiente, incluido el indicador usuarioActivo;
  no permite aprobar una cuenta inactiva. En_revision se conserva como estado
  histórico compatible con aprobación, pero no pertenece a la bandeja pendiente.
- Paginación por cursor UUID ascendente, `limite` entre 1 y 50, por defecto 20.
  Respuesta: `items` y `siguienteCursor`; null significa fin. No es un orden por
  fecha ni una instantánea de registros que cambian durante la navegación.
- Queries AsNoTracking, proyecciones de campos, sin cargar colecciones completas.
  El índice `(estado,id)` soporta la bandeja; los totales se calculan en una sola
  consulta agregada. No se mantienen contadores manuales que puedan duplicarse.
- Se mantienen autenticación JWT, revocación, límite de cuerpo, rate limiting,
  concurrencia global y cancelación. Las respuestas usan no-store.
- No se añadieron endpoints genéricos para cambiar usuario, rol, estado arbitrario
  o borrar solicitudes. La aprobación solo modifica estado, observación y auditoría.

## Pruebas

`scripts/verify-usuario.ps1` restaura el esquema original en una base temporal,
aplica 001–005 y ejecuta `scripts/verify-afiliacion-cases.ps1` junto con los módulos
anteriores. Cubre autenticación, permisos, duplicados entre ambos POST, paginación,
contacto, cuentas inactivas, aprobación concurrente, auditoría, estados resueltos,
totales vacíos y totales después de aprobar.
