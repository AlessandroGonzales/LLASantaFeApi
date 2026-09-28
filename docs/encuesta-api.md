# Encuestas: contrato y prueba en Swagger

## Preparación

Detener la API y ejecutar **una sola vez** `database/003_encuestas.sql` en pgAdmin,
después de 001 y 002. El script no fue aplicado a la base habitual: se verificó
en PostgreSQL 16 temporal. El modelo EF ya incluye las columnas; no requiere
scaffolding. Después, ejecutar Presentation en Debug.

La migración preserva los registros existentes. Considera publicadas las encuestas
anteriores activas o con respuestas. Las demás quedan como borradores. No modifica
sus preguntas ni reinterpreta respuestas antiguas. Antes de utilizar encuestas
heredadas, comprobar que su configuración cumple el contrato v1.

El administrador necesita un JWT de una cuenta con rol `Admin` o `SuperAdmin`.
Los usuarios registrados pueden consultar encuestas disponibles y responderlas.
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

`opcion_multiple` recibe un array de IDs distintos, por ejemplo `["a","b"]`.
No enviar `usuarioId`: se obtiene exclusivamente del JWT. Una segunda respuesta
devuelve 409. No hay edición o eliminación de respuestas en este módulo.

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

## Verificación

`scripts/verify-usuario.ps1` restaura el backup en una base temporal, aplica 001–003
y ejecuta `scripts/verify-encuesta-cases.ps1` junto con la regresión de Usuario.
Incluye permisos, JSON inválido, fechas, publicación, paginación, revisión obsoleta,
ediciones simultáneas, respuestas duplicadas y envío concurrente con cierre.
Son pruebas funcionales; no constituyen una medición de capacidad de Azure.
