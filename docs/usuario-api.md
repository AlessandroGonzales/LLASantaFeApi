# Usuario: contrato HTTP y verificación

## Requisito de base de datos

Aplicar **una vez** `database/002_usuario_seguridad.sql`, después de 001 y antes de
usar esta versión de la API. Hacer backup y detener las escrituras durante la
migración. El script no se aplicó a tu base existente; se probó en PostgreSQL
16.10 temporal. El modelo EF ya incluye los cambios: no requiere scaffolding.

```powershell
& 'C:/Program Files/PostgreSQL/16/bin/psql.exe' -h localhost -p 5432 -U postgres -d LLASantaFe -W -v ON_ERROR_STOP=1 -f ./database/002_usuario_seguridad.sql
```

También puede ejecutarse completo en pgAdmin. Ante un error, ejecutar `ROLLBACK;`.
El script se cancela si encuentra varias respuestas del mismo usuario a una
encuesta o varias solicitudes de afiliación del mismo usuario. No borra ni
selecciona automáticamente registros para resolver estos conflictos.

Diagnóstico previo, si hubiera conflictos:

```sql
SELECT encuesta_id, usuario_id, count(*)
FROM public.encuesta_respuestas
WHERE usuario_id IS NOT NULL
GROUP BY encuesta_id, usuario_id HAVING count(*) > 1;

SELECT usuario_id, count(*)
FROM public.solicitudes_afiliacion
GROUP BY usuario_id HAVING count(*) > 1;
```

002 agrega `version_acceso`, `intentos_fallidos`, `bloqueado_hasta`, dos reglas
de unicidad y un trigger que rota la versión de acceso cuando cambian contraseña,
rol, activo o deleted_at. Todos los JWT anteriores a esta entrega quedan inválidos.
Se crea el rol `Usuario` si falta; no se otorgan permisos administrativos a cuentas.

## Endpoints

Base: `/api/Usuario`. Todas las operaciones salvo registro y login requieren
`Authorization: Bearer <token>`. El identificador del usuario sale del JWT;
ningún cuerpo recibe `usuarioId`, rol, hash, estado de aprobación o versión de acceso.

| Método | Ruta | Resultado |
| --- | --- | --- |
| POST | `/api/Usuario` | 202, respuesta genérica de registro |
| POST | `/api/Usuario/login` | 200, token y expiración |
| GET | `/api/Usuario/me` | 200, perfil propio |
| PATCH | `/api/Usuario/me` | 204 |
| DELETE | `/api/Usuario/me` | 204, baja lógica y revocación |
| POST | `/api/Usuario/me/encuestas/{encuestaId}/respuesta` | 201; 409 si ya respondió o no está abierta |
| POST | `/api/Usuario/me/afiliacion` | 201 pendiente; 409 si ya existe |
| POST | `/api/Usuario/me/solicitudes` | 201 recibida |

Errores de datos: 400. Falta de autenticación, token inválido o cuenta dada de
baja: 401. Privilegios insuficientes: 403. Encuesta/perfil inexistente: 404.
Conflictos: 409. Cuerpo mayor a 64 KiB: 413. Límite de solicitudes: 429.
Los errores de negocio y persistencia usan ProblemDetails sin detalles SQL.

## Registro y login

```json
{
  "nombre": "Nombre",
  "apellido": "Apellido",
  "email": "persona@example.com",
  "password": "Una-frase-larga-2026!",
  "fechaNacimiento": "1995-06-15",
  "telefono": "+5493411234567",
  "genero": "prefiero_no_decir",
  "profesion": null,
  "fotoPerfilUrl": null,
  "ciudadId": null
}
```

Nombre/apellido: 1–100 caracteres útiles; email: máximo 254 y formato admitido por
la restricción existente en PostgreSQL. Contraseña nueva: mínimo 15 caracteres,
máximo 72 **bytes UTF-8** por el límite de BCrypt. Las contraseñas de cuentas
existentes más cortas siguen siendo válidas para login. No se devuelve el hash.
La fecha de nacimiento es obligatoria y no puede ser futura. La ciudad debe existir.
Teléfono: formato de la restricción existente; género: masculino, femenino, otro
o prefiero_no_decir; profesión: máximo 150; foto: URL HTTPS, máximo 2048.

El registro siempre devuelve 202 para datos válidos, tanto si crea la cuenta como
si el correo ya existía. Esto evita exponer la existencia de cuentas mediante la
respuesta de registro. No modifica ni reactiva cuentas existentes, ni envía correo.
El rol se fija a Usuario en el servidor. La unicidad usa email normalizado.

```json
{
  "email": "persona@example.com",
  "password": "Una-frase-larga-2026!"
}
```

Login devuelve `token`, `expiresAt` y `tokenType`. Ya no devuelve `reactivado`:
la reactivación automática fue eliminada. JWT predeterminado: 15 minutos, sin
refresh token en este alcance. Cuenta inexistente, contraseña incorrecta, cuenta
inactiva o bloqueo temporal reciben el mismo 401. Tras cinco fallos se bloquean
nuevos logins de esa cuenta por cinco minutos; el contador es persistente en DB.
Un login válido después del bloqueo reinicia el contador. Los tokens ya emitidos
no se invalidan por errores de contraseña de un tercero.

## Perfil y PATCH

El perfil incluye nombre, apellido, email, teléfono, nacimiento, género, profesión,
foto, ciudad, nombre del rol y fecha de registro. No incluye hash, versión de acceso
ni contadores de autenticación.

```json
{
  "nombre": "Nombre actualizado",
  "telefono": null
}
```

Campos editables: nombre, apellido, teléfono, fotoPerfilUrl y ciudadId. Campo
omitido: se conserva. `null`: elimina teléfono, foto o ciudad. Nombre/apellido no
admiten null, blanco ni cadenas vacías. El PATCH vacío se rechaza. Email, contraseña,
rol, fecha de nacimiento, género y profesión no son editables por este endpoint.
Campos desconocidos y propiedades JSON duplicadas se rechazan con 400.

DELETE `/me` es una baja de la aplicación: marca activo=false y deleted_at, y el
trigger rota la versión del JWT. El acceso queda invalidado en las siguientes
solicitudes. No borra físicamente la cuenta ni constituye una desafiliación partidaria.
Una reactivación administrativa futura no revive tokens anteriores.

## Encuestas: contrato inicial v1

La creación/administración de encuestas se implementará con esa entidad. Por ahora,
la encuesta debe existir en PostgreSQL y tener una configuración como esta:

```json
{
  "version": 1,
  "preguntas": [
    {
      "id": "p1",
      "texto": "Elegí una opción",
      "tipo": "opcion_unica",
      "obligatoria": true,
      "opciones": [
        { "id": "a", "texto": "Opción A" },
        { "id": "b", "texto": "Opción B" }
      ]
    },
    { "id": "p2", "texto": "Comentario", "tipo": "texto", "maxLongitud": 500 },
    { "id": "p3", "texto": "Valoración", "tipo": "numero", "min": 1, "max": 5 }
  ]
}
```

Respuesta enviada al endpoint:

```json
{ "respuestas": { "p1": "a", "p2": "Mi comentario", "p3": 4 } }
```

`opcion_multiple` usa las mismas opciones y recibe un array, por ejemplo
`["a", "b"]`, sin duplicados. Máximo 50 preguntas, 20 opciones por pregunta,
IDs únicos de hasta 64 caracteres y texto de respuesta de hasta 2000 caracteres
(o el máximo menor configurado). Los números deben estar dentro de min/max.
Las preguntas opcionales pueden omitirse; si se envían, deben tener un valor válido.
No se aceptan preguntas u opciones ajenas a la configuración.

Solo se admite responder cuando la encuesta está activa, no eliminada y dentro de
sus fechas inclusivas, usando la fecha de Argentina. La inserción vuelve a comprobar
estado, fechas, cuenta y configuración para no guardar respuestas validadas contra
otra configuración. Una configuración inválida recibe 409. No se registra IP ni
metadata suministrada por el cliente. Las respuestas sí están vinculadas a la cuenta.

La regla es **una respuesta por cuenta y encuesta**, garantizada por índice único
incluso con solicitudes concurrentes. Esto no demuestra que una persona no tenga
varias cuentas. Las filas históricas con usuario_id null se conservan; estos
endpoints no crean respuestas anónimas. La edición/versionado de encuestas una vez
respondidas debe definirse al implementar su administración.

## Afiliación y solicitudes

```json
{ "confirmacion": true }
```

Crea una solicitud de afiliación pendiente. La confirmación debe ser true. Solo
puede existir una solicitud por usuario, incluso si luego es rechazada. El endpoint
no aprueba la afiliación ni permite elegir observaciones administrativas. La
aprobación y eventual reconsideración pertenecen a un flujo administrativo posterior.

```json
{
  "motivo": "sugerencia",
  "mensaje": "Contenido de la sugerencia para el partido."
}
```

Motivos: proyecto, sugerencia, duda, voluntariado o denuncia. Mensaje: 10–5000 caracteres
útiles. Se guarda en solicitudes_ciudadanas con aceptacion=false y usuario del JWT.
Los mensajes son texto, no HTML confiable: React debe renderizarlos como texto.
No se aceptan URLs arbitrarias de archivos ni adjuntos en este alcance; su carga
necesita almacenamiento privado y validación en un flujo separado.

## Accesos y límites

- Escrituras de Sede, Ciudad y Departamento requieren Admin o SuperAdmin.
- GET públicos de sedes y ciudades se mantienen anónimos.
- Autenticación obligatoria por defecto; registro, login y lecturas públicas se
  exceptúan explícitamente. Swagger está habilitado solo en Development.
- JWT: HS256, emisor, audiencia, expiración, identificador, rol vigente y versión
  de acceso comprobados. Sin nombre, email o foto en el token.
- Caché de respuestas deshabilitada; no se publican archivos estáticos privados.
- Límite global: 120 solicitudes/minuto/IP; máximo 8 solicitudes simultáneas
  por instancia, sin cola. Login: 10/minuto/IP; registro: 5/hora/IP;
  escrituras de Usuario: 20/minuto/IP. 429 incluye Retry-After cuando hay ventana.
- Los límites ocurren antes de validar JWT contra DB y de calcular BCrypt.
- No se confía en X-Forwarded-For del cliente. Al desplegar detrás de Azure, configurar
  proxies conocidos; de otro modo varias personas podrían compartir la cuota del proxy.
- RateLimits:General/Login/Registro/Escritura permiten ajustar cuotas. Son límites
  locales por instancia; la protección distribuida/WAF y los límites de costos
  del despliegue siguen siendo trabajo de infraestructura.

## Prueba manual con Swagger

1. Aplicar 002. Configurar conexión y clave JWT local como en la entrega anterior.
2. Ejecutar `./scripts/dotnet.ps1 run --project Presentation` desde la raíz.
3. Abrir `/swagger` en la URL que muestra la consola.
4. Registrar una cuenta, ejecutar login y copiar el valor de `token`.
5. Pulsar Authorize y pegar el token **sin** escribir el prefijo Bearer.
6. Probar GET/PATCH `/me`, afiliación, solicitudes y una encuesta existente v1.
7. Repetir afiliación/respuesta: 409. Probar escritura administrativa con Usuario: 403.
8. Ejecutar DELETE `/me`; el token anterior debe recibir 401 y el login no reactivar.

Para probar administración, usar una cuenta administrativa provisionada por el
responsable de la base. El registro público nunca concede Admin.

## Verificación automatizada

`scripts/verify-usuario.ps1` restaura el backup y aplica 001+002 en una base nueva
`net10_usuario_*`, dentro de `.artifacts/postgres-net10` en 127.0.0.1:55432.
Comprueba que el servidor sea ese cluster antes de crear datos. No usa la conexión
real del appsettings. Prueba HTTP y PostgreSQL reales, no EF InMemory.

```powershell
./scripts/verify-usuario.ps1 -SchemaPath 'C:/Users/jaman/OneDrive/Documentos/esquema_sin_datos.sql'
```

Cubre autenticación, aislamiento entre usuarios, sobreasignación, PATCH con null,
límites del cuerpo, encuestas y configuración inválida, afiliación, concurrencia,
revocación, bloqueo temporal, límites 429 y contrato Swagger. La prueba ajusta las
cuotas de su propio proceso y las reduce en una segunda fase para verificar 429.
Detiene la API de prueba al finalizar. Los datos sintéticos permanecen en el cluster
temporal para inspección. También se verificó que 002 haga rollback ante duplicados
históricos sin eliminar registros.

No se integraron Google/Gmail, verificación de propiedad del email, recuperación
de contraseña ni frontend. Son flujos adicionales: no se simula el envío de correos.
El cliente debe usar HTTPS en producción y evitar persistir el bearer token en
almacenamiento accesible a scripts de terceros. No hay renovación de sesión automática.
Compilación Debug y Release: 0 errores y 0 advertencias.

# Integración con Encuesta

El módulo de encuestas requiere también `database/003_encuestas.sql`, después de
001 y 002. Solo se responde a encuestas publicadas, activas y dentro de fecha.
Creación, edición de borradores, publicación, cierre y ejemplos están en
`docs/encuesta-api.md`. Las pruebas conjuntas actuales completan 186 comprobaciones.

