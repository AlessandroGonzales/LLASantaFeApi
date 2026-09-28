# Noticias: contrato y pruebas en Swagger

No requiere migraciones nuevas ni scaffolding. Utiliza la tabla `noticias`, su índice de publicación y el DbContext existentes. Las migraciones anteriores del proyecto siguen siendo necesarias para el resto de la API.

## Endpoints

| Método y ruta | Acceso | Resultado |
| --- | --- | --- |
| POST `/api/Noticia` | Administrador | `201`, `{ "id": "uuid" }` y encabezado Location |
| GET `/api/Noticia` | Público | Array con hasta seis noticias publicadas |
| GET `/api/Noticia/administracion/{id}` | Administrador | Detalle para consultar o editar, incluidos borradores |
| PATCH `/api/Noticia/{id}` | Administrador | `204`, sin cuerpo |

## 1. Crear

Iniciar sesión con un administrador y autorizar Swagger con su token. Ejecutar POST con:

```json
{
  "titulo": "Presentación de un nuevo proyecto",
  "resumen": "Información sobre el proyecto presentado.",
  "contenido": "Contenido completo de la noticia, con la información de la presentación.",
  "imagenPrincipalUrl": null,
  "fechaPublicacion": null,
  "publicado": true,
  "destacada": false,
  "categoria": "institucional",
  "ciudadId": null
}
```

Guardar el `id` devuelto. Para asociar una ciudad, enviar un UUID existente. Para una imagen, enviar una URL HTTPS sin credenciales: este endpoint guarda la referencia, no sube ni descarga imágenes.

El servidor asigna el identificador y el autor autenticado; la base mantiene los timestamps. Las visualizaciones comienzan en cero y no aumentan al consultar el listado.

## 2. Consultar las últimas seis

Ejecutar GET `/api/Noticia`, incluso sin autenticación. Ordena por `fechaPublicacion` descendente y desempata por `id` descendente. Devuelve menos de seis cuando no hay suficientes noticias disponibles, y `[]` cuando no hay ninguna.

No incluye borradores, fechas futuras ni bajas lógicas. Incluye el contenido completo para que el frontend pueda mostrar estas seis noticias. No acepta ampliar el límite mediante parámetros.

## 3. Modificar atributos

Con el token de administrador, ejecutar PATCH usando el `id` creado:

```json
{
  "titulo": "Título actualizado de la noticia",
  "resumen": null,
  "destacada": true
}
```

Los campos omitidos conservan su valor. `null` permite quitar `resumen`, `imagenPrincipalUrl`, `categoria` y `ciudadId`. Título, contenido y booleanos no admiten `null`. Un objeto vacío es inválido. Consultar el detalle administrativo para verificar el resultado.

Para retirar del listado público sin eliminar:

```json
{ "publicado": false }
```

### Fechas y publicación

- `publicado` omitido al crear significa borrador.
- Publicar sin fecha asigna la hora actual del servidor. Las fechas enviadas deben usar ISO 8601 con `Z` o desplazamiento horario; se convierten a UTC.
- Una fecha futura mantiene la noticia fuera del listado hasta ese momento.
- Editar el título o contenido no cambia la fecha de publicación.
- En PATCH, `fechaPublicacion: null` borra la fecha de un borrador; si la noticia queda publicada, asigna la hora actual.
- Dos PATCH de campos distintos conservan ambos cambios. Para el mismo campo prevalece la última escritura; no hay control de versiones editoriales.

## Validaciones y límites

Título: 3–200 caracteres; resumen: hasta 1.000; contenido: 10–30.000; categoría: hasta 80; URL de imagen: hasta 2.048. También se aplica el límite general de 64 KiB al cuerpo HTTP, por lo que un texto multibyte puede alcanzar ese límite antes del máximo de caracteres.

Se rechazan campos desconocidos, claves JSON duplicadas e intentos de asignar autor, timestamps o cantidad de visualizaciones. Sin sesión las escrituras devuelven `401`; con usuario no administrador, `403`; un identificador inexistente o dado de baja devuelve `404`. Las escrituras usan el limitador existente y todas las rutas quedan sujetas a los límites generales.

La consulta utiliza proyección, `AsNoTracking` y `Take(6)` en PostgreSQL. El PATCH ejecuta una actualización atómica sin cargar toda la entidad. Se conserva el índice existente de publicación y no se agrega caché.

El contenido se guarda como texto: React debe renderizarlo como texto escapado. No se implementó sanitización de HTML ni debe insertarse directamente mediante `dangerouslySetInnerHTML`.

## Verificación realizada

Compilación Release: cero errores y cero advertencias. Suite de integración: 379 comprobaciones aprobadas sobre PostgreSQL temporal, incluyendo Usuario, Encuesta, Solicitud ciudadana, Afiliación y Noticias. Los casos de Noticias están en `scripts/verify-noticia-cases.ps1`, ejecutado por `scripts/verify-usuario.ps1`; comprueban permisos, orden y límite, exclusión de borradores/futuras/bajas, validaciones, PATCH y concurrencia de campos distintos. La suite no modifica la base de uso de la aplicación.
