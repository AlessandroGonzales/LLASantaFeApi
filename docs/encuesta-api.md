# Encuestas: contrato v1, consumo y prueba en Swagger

Paso 6 del cierre de API. Este módulo mantiene los endpoints existentes y añade
un comprobante propio para resolver envíos de resultado incierto. No añade workers,
polling, paquetes, otro DbContext ni migraciones. Las pantallas React y su conexión
con la sesión común se integran en el paso 7; este repositorio contiene la API.

## Preparación

La versión actual de la aplicación requiere los scripts **001–009**, aplicados una
sola vez y en orden. `database/003_encuestas.sql` incorpora las reglas específicas
de encuestas; 007 incorpora la verificación de nuevas cuentas. Este paso no exige
otro script ni scaffolding. Las pruebas automáticas usan PostgreSQL 16 temporal;
no aplican cambios a tu base habitual. Después, ejecutar Presentation en Debug.

La migración preserva los registros existentes. Considera publicadas las encuestas
anteriores activas o con respuestas. Las demás quedan como borradores. No modifica
sus preguntas ni reinterpreta respuestas antiguas. Antes de utilizar encuestas
heredadas, comprobar que su configuración cumple el contrato v1.

El administrador necesita un JWT de una cuenta con rol `Admin` o `SuperAdmin`.
Los usuarios con sesión vigente pueden consultar encuestas disponibles y responderlas.
Las cuentas nuevas verifican su email antes del login. Una baja revoca el acceso.
El registro público nunca asigna privilegios administrativos.

## Flujo

1. `POST /api/Encuesta` crea un borrador inactivo y devuelve `id` y `revision: 1`.
2. `GET /api/Encuesta/administracion/{id}` permite revisar el borrador.
3. `PUT /api/Encuesta/{id}/borrador` reemplaza sus datos editables. Enviar el mismo
   contrato de creación y la `revision` obtenida al consultarlo. Se utiliza PUT
   porque se reemplaza el documento completo de preguntas y metadatos.
4. `POST /api/Encuesta/{id}/publicar` con `{"revision":1}` publica esa revisión.
5. El usuario consulta `GET /api/Encuesta` y `GET /api/Encuesta/{id}`.
6. Responde mediante el endpoint existente de Usuario, mostrado más abajo.
   El 201 incluye `id`, `estado: "registrada"` y `Location` al comprobante propio.
7. El administrador consulta `GET /api/Encuesta/{id}/respuestas`.
8. `POST /api/Encuesta/{id}/cerrar` con la revisión actual cierra la recepción.

Consultar nuevamente después de editar, publicar o cerrar: cada modificación
incrementa `revision`. Una revisión obsoleta devuelve 409, incluso si dos peticiones
llegan simultáneamente. Las preguntas, fechas y metadatos publicados son inmutables.
Una encuesta cerrada no se reabre. Para cambiar preguntas publicadas se crea otra
encuesta, preservando la interpretación de las respuestas anteriores.

La publicación puede programarse con `fechaInicio`; no se publica una encuesta cuya
`fechaFin` ya pasó. Los límites son inclusivos, en la zona horaria de Argentina.
Una fecha nula no impone límite en ese extremo. Ciudad/departamento son metadatos
de ubicación, no restricciones de residencia ni selección de destinatarios.

## Ejemplo de creación

```json
{
  "titulo": "Experiencia con el sitio",
  "descripcion": "Opiniones sobre el funcionamiento de la aplicación",
  "tipo": "general",
  "fechaInicio": null,
  "fechaFin": null,
  "ciudadId": null,
  "departamentoId": null,
  "configuracion": {
    "version": 1,
    "preguntas": [
      {
        "id": "facilidad",
        "texto": "¿Qué tan fácil fue usar el sitio?",
        "tipo": "numero",
        "obligatoria": true,
        "min": 1,
        "max": 5
      },
      {
        "id": "seccion",
        "texto": "¿Qué sección consultaste?",
        "tipo": "opcion_unica",
        "obligatoria": true,
        "opciones": [
          { "id": "noticias", "texto": "Noticias" },
          { "id": "sedes", "texto": "Sedes" }
        ]
      },
      {
        "id": "comentario",
        "texto": "¿Qué mejorarías del sitio?",
        "tipo": "texto",
        "maxLongitud": 500
      }
    ]
  }
}
```

Respuesta: `POST /api/Usuario/me/encuestas/{id}/respuesta`

```json
{
  "respuestas": {
    "facilidad": 4,
    "seccion": "noticias",
    "comentario": "Facilitar la búsqueda de noticias."
  }
}
```

El archivo `docs/ejemplos/encuesta-v1.json` contiene un ejemplo completo con los
cuatro tipos; también se usa en las pruebas de integración. Para ese archivo,
una respuesta válida es:

```json
{
  "respuestas": {
    "facilidad": 3.5,
    "seccion": "noticias",
    "dispositivos": ["movil", "computadora"],
    "comentario": "Más claridad en la búsqueda."
  }
}
```

`opcion_multiple` recibe un array de IDs distintos, por ejemplo `["a","b"]`.
No enviar `usuarioId`: se obtiene exclusivamente del JWT. Una segunda respuesta
devuelve 409. No hay edición o eliminación de respuestas en este módulo.

## Contrato de renderizado para React

El orden visual es el del array `preguntas`. Usar `pregunta.id` como clave y como
nombre dentro de `respuestas`; en opciones, enviar `opcion.id`, nunca su etiqueta.
Los IDs distinguen mayúsculas y minúsculas. Mostrar textos como contenido de React,
sin `dangerouslySetInnerHTML`. Si `configuracion.version` no es 1, impedir el envío
y mostrar que el formulario no es compatible.

| `pregunta.tipo` | Control | Valor JSON enviado | Validación del cliente |
| --- | --- | --- | --- |
| `texto` | `textarea` | string | `maxLength={maxLongitud ?? 2000}`; sin texto vacío. |
| `numero` | `input type="number" step="any"` | number | `min`/`max` cuando existen; convertir después de comprobar que el campo no está vacío y que el número es finito. |
| `opcion_unica` | Grupo de radios o `select` | string con un ID | Una opción del catálogo. |
| `opcion_multiple` | Grupo de checkboxes | Array de IDs | IDs distintos; al menos uno si se envía esta pregunta. |

`obligatoria` ausente equivale a false. Para una pregunta opcional sin contestar,
omitir su clave; no enviar null, string vacío o array vacío. Debe existir al menos
una respuesta válida. Un número cero no representa un campo vacío. No usar
`Number("")`, que convertiría un campo vacío en cero, ni enviar el valor textual
del input de número. Estas validaciones ayudan al usuario; la API vuelve a validar
todo contra la configuración publicada.

Consulta bajo demanda:

1. Al abrir la sección privada, `GET /api/Encuesta?limite=20`. Compartir el resultado
   entre componentes de esa sesión; solicitar otra página solo cuando se necesite.
2. Al seleccionar una encuesta sin responder, consultar su detalle una vez.
   Si la lista indica `respondida: true`, mostrar el estado registrado y deshabilitar
   otro envío. Para una entrada directa sin listado previo, consultar el comprobante
   propio para conocer ese estado.
3. Mantener respuestas en memoria y enviar un solo POST al confirmar. Deshabilitar
   el botón mientras está en curso; no hacer llamadas por cada cambio de campo.
4. Tras 201, marcar `respondida: true` en la copia cliente y mostrar confirmación.
   No hace falta volver a cargar todas las encuestas o el perfil.
5. Cancelar lecturas obsoletas con `AbortController`; limpiar datos al cambiar de
   cuenta/cerrar sesión. Las páginas públicas no consultan este módulo. No programar
   `setInterval`, actualización en cada render ni reintentos automáticos del POST.

## Comprobar un envío y tratar errores

`GET /api/Usuario/me/encuestas/{encuestaId}/respuesta` necesita JWT y devuelve:

```json
{
  "id": "uuid-de-la-respuesta",
  "createdAt": "2026-10-21T12:00:00Z"
}
```

Solo consulta la relación del usuario del JWT y la encuesta indicada. Devuelve 404
si no encuentra una respuesta propia, tanto si la encuesta existe como si no.
No acepta un `usuarioId` para consultar a otra persona. Mantiene disponible el
comprobante después del cierre, vencimiento o baja lógica de la encuesta, mientras
se conserve la respuesta. No lee preguntas, respuestas JSON, IP ni contactos.
Todas estas respuestas son privadas y `no-store`.

Si el POST termina en timeout/error de red o 409, conservar el formulario y consultar
este GET una vez. Un 200 confirma que se registró una respuesta; no compara el
contenido enviado en ese último intento con el primero. Un 404 solo describe lo
confirmado en ese instante: un POST anterior aún podría estar en curso. Mantener
estado incierto y permitir comprobación manual, sin bucles ni afirmar éxito/fallo
definitivo. Si se vuelve a enviar manualmente, la restricción única impide almacenar
otra respuesta.

| Estado | Acción del cliente |
| --- | --- |
| 400 | Conservar respuestas y corregir contrato/tipos/límites. |
| 401 | Pedir inicio de sesión; no repetir automáticamente la escritura. |
| 403 | Sin permiso para esa operación administrativa. |
| 404 del detalle | Encuesta no disponible, cerrada o fuera de fechas; el comprobante propio aún puede existir. |
| 409 del POST | Comprobar respuesta propia; si no existe, revisar disponibilidad. No asumir que todo 409 es duplicado. |
| 413 | El cuerpo excede el límite; no repetir sin reducirlo. |
| 429 | Respetar `Retry-After` y conservar el formulario. |
| 503 / error de red | Conservar datos y comprobar el resultado antes de volver a enviar. |

## Límites y consultas

- Título: 3–200 caracteres; descripción: hasta 2000.
- `tipo`: etiqueta de 1–50 caracteres, comienza por letra minúscula y solo admite
  letras ASCII minúsculas, números y guion bajo. No es el tipo de cada pregunta.
- Ubicación opcional: ciudad **o** departamento, con FK válida.
- Configuración: objeto JSON v1, máximo 32 KiB, de 1 a 50 preguntas con IDs únicos.
- Pregunta: ID hasta 64 caracteres y texto hasta 500.
- Opciones: de 2 a 20, IDs únicos hasta 64 caracteres, etiquetas hasta 300.
- Texto: `maxLongitud` entre 1 y 2000; por defecto 2000.
- Número: decimal JSON, con `min` y `max` opcionales. No enviar números como strings.
- Se rechazan tipos desconocidos, campos desconocidos, claves duplicadas, opciones
  inválidas, obligatorias ausentes y parámetros incompatibles con el tipo.
- NUL (`\u0000`) se rechaza en títulos, descripción, preguntas, opciones y textos
  de respuesta antes de llegar a PostgreSQL. La respuesta serializada por la API
  admite hasta 64 KiB; el escape de Unicode puede ocupar más bytes que el texto
  introducido. El techo HTTP de 64 KiB incluye todo el cuerpo de la petición.
- Los escapes UTF-16 incompletos se rechazan con 400; los caracteres Unicode válidos,
  incluidos emojis, se conservan.
- Listados: `?limite=20&cursor=<uuid>`, máximo 50 encuestas o 20 respuestas.
  La respuesta contiene `items` y `siguienteCursor`; null indica fin.
- Orden por UUID ascendente, no cronológico. La paginación no es una instantánea:
  los registros creados concurrentemente pueden requerir comenzar otra consulta.
- El listado de encuestas incluye `respondida` del usuario actual, sin cargar
  configuraciones ni colecciones de respuestas. El listado administrativo incluye
  borradores, publicadas y cerradas, excepto bajas lógicas existentes.
- El listado de respuestas solo admite administradores y no incluye identificador
  del usuario, IP ni metadata. Los textos libres pueden contener datos personales;
  esta omisión no convierte las respuestas en anónimas. La relación usuario/encuesta
  se conserva en DB para controlar unicidad.

Se usan consultas `AsNoTracking`, proyecciones y `Take(limite+1)`, sin COUNT total ni
OFFSET creciente. Índices específicos soportan el cursor de respuestas y las
encuestas activas. Todas las operaciones propagan cancelación. Se mantienen los
límites globales de cuerpo (64 KiB), concurrencia y frecuencia de la API.

El trigger de respuestas coordina mediante bloqueo de fila el envío con el cierre;
los envíos simultáneos entre usuarios distintos pueden avanzar juntos. Los índices
únicos del script 002 impiden duplicados. La API valida preguntas y respuestas;
PostgreSQL refuerza estado, tamaño, forma del JSON y contrato publicado inmutable.
El INSERT también bloquea en modo compartido la cuenta y vuelve a comprobar su
estado/verificación, evitando aceptar una escritura que perdió una carrera con una
baja de cuenta. Los bloqueos se mantienen solo durante esa sentencia SQL.
Editar un borrador o cerrar una encuesta no carga su configuración anterior.
Consultar resultados comprueba existencia sin leer preguntas; cargar el detalle o
validar una publicación sí requiere configuración. Se eliminaron únicamente los
esqueletos vacíos de EncuestaRespuesta y sus registros DI: el envío y comprobante
usan Usuario, y la lectura administrativa permanece en Encuesta.

## Prueba manual completa

1. Iniciar Presentation en Development y abrir Swagger. Tener un usuario común y
   un administrador con login válido. No usar credenciales reales en ejemplos.
2. Autorizar Swagger con el JWT administrativo y crear una encuesta pegando
   `docs/ejemplos/encuesta-v1.json`. Guardar `id` y `revision: 1` del 201.
3. Consultar el borrador administrativo. Publicar con `{"revision":1}`; debe dar 204.
   Consultar nuevamente: ahora la revisión es 2. Si editaste antes, usar la revisión
   obtenida después de editar, no la del ejemplo.
4. Cambiar a JWT común. Consultar lista y detalle; deben incluir la encuesta y sus
   cuatro preguntas. El comprobante propio todavía debe dar 404.
5. Enviar la respuesta completa de arriba: 201, `estado: "registrada"`. Abrir la URL
   de `Location` o ejecutar el GET propio: 200 con el mismo ID y fecha.
6. Repetir el POST: 409. El GET propio conserva el ID. La lista muestra
   `respondida: true`. Con otra cuenta que no respondió, el comprobante da 404.
7. Con JWT administrativo, consultar las respuestas: contiene el número decimal,
   los IDs, el array y el texto, sin datos de identificación del usuario. Consultar
   con `limite=1` y continuar con `siguienteCursor` si hay más respuestas.
8. Cerrar con `{"revision":2}`. El detalle normal da 404 y el comprobante del usuario
   que respondió continúa en 200. Otra respuesta no puede guardarse. El administrador
   conserva acceso al detalle y resultados. Una revisión obsoleta o reapertura da 409.
9. Para probar errores de formato, crear/publicar otra encuesta y enviar una opción
   inexistente, un número como string, una selección repetida o un obligatorio
   ausente: 400 y ninguna fila nueva. Sin JWT: 401; crear/publicar/cerrar con usuario
   común: 403.

## Verificación

`scripts/verify-usuario.ps1` restaura el backup en una base temporal, aplica 001–009
y ejecuta `scripts/verify-encuesta-cases.ps1` y `scripts/verify-encuesta-cierre-cases.ps1`
junto con la regresión de los demás módulos.
Incluye permisos, JSON inválido, fechas, publicación, paginación, revisión obsoleta,
ediciones simultáneas, respuestas duplicadas y envío concurrente con cierre.
Añade recorrido de los cuatro tipos hasta jsonb, comprobantes privados, persistencia
tras el cierre, fechas inclusivas de Argentina, Unicode/NUL, tamaño serializado y
baja concurrente de cuenta. Son pruebas funcionales; no constituyen una medición de
capacidad de Azure. La medición con el frontend y sus pantallas queda en el paso 7.

Verificación del 21 de octubre de 2026: compilación Release con cero errores y
advertencias; 846 comprobaciones HTTP/PostgreSQL (76 nuevas de este cierre),
72 de consumo y 45 de correo simulado: **963 en total**. PostgreSQL temporal se
detuvo al finalizar. La revisión Worktree de Gitleaks no encontró secretos ni
configuraciones privadas versionables.
