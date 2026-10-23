# Catálogo geográfico: departamentos y ciudades

Paso 3, 20 de octubre de 2026. Backend .NET 10; la conexión con React se hará en la entrega del frontend.

Este paso no requiere SQL nuevo, scaffolding ni paquetes NuGet. El catálogo utiliza el esquema existente; para la API completa, SolicitudCiudadana requiere además SQL 008 desde el paso 4. Se conserva el único `LLASantaFeDbContext` y se consultan los UUID existentes; los GET no crean ni reemplazan identificadores o registros.

## Contrato público

Los dos GET son anónimos. Para aprovechar la caché, consultarlos sin `Authorization` ni cookies de sesión.

| Método y ruta | Respuesta |
| --- | --- |
| GET `/api/Departamento` | 200 con todos los departamentos, ordenados por nombre e ID. |
| GET `/api/Ciudad?departamentoId=<UUID>` | 200 con las ciudades de ese departamento, ordenadas por nombre e ID. |

Departamento:

```json
[
  { "idDepartamento": "UUID_REAL_DEL_DEPARTAMENTO", "nombre": "Nombre del departamento" }
]
```

Ciudad, conservando el contrato anterior:

```json
[
  {
    "idCiudad": "UUID_REAL_DE_LA_CIUDAD",
    "nombre": "Nombre de la localidad",
    "departamentoId": "UUID_REAL_DEL_DEPARTAMENTO"
  }
]
```

Los marcadores de los ejemplos se sustituyen por UUID recibidos desde la API. No se devuelven fechas internas, relaciones, personas ni datos de otras entidades. Las ciudades incluyen las localidades que estén cargadas en esa tabla; este paso no importa un padrón geográfico ni supone una cantidad fija de municipios/comunas.

Filtro ausente, vacío, mal formado o UUID de ceros: **400**. UUID válido sin coincidencias, incluso si no corresponde a un departamento existente: **200 con `[]`**. Es una consulta filtrada; no se ejecuta una segunda consulta de existencia para distinguir esos dos casos. La creación de ciudades y los formularios de usuario sí validan sus referencias en el servidor; la FK mantiene la integridad final.

## Consumo y caché

- Una consulta SQL por GET que llega al repositorio, con `AsNoTracking` y selección de las columnas necesarias. Sin consultas por cada fila ni carga de relaciones.
- Se reutilizan los índices existentes: nombre único de departamento y `(departamento_id, nombre)` de ciudades. No se añaden índices redundantes. El plan de ejecución concreto lo elige PostgreSQL según los datos.
- Caché del servidor de **300 segundos** para ambos catálogos, configurable con `CatalogCache:DepartmentsSeconds` y `CitiesSeconds` (1–900).
- HTTP `Cache-Control: public, max-age=30` por defecto; `Vary: Origin`. El middleware agrupa la regeneración concurrente de una misma clave. El almacenamiento sigue acotado a 4 MiB, con 128 KiB por cuerpo cacheado.
- Peticiones con JWT/cookies o query adicional salen de la caché compartida. Los errores, respuestas con cookies y datos privados conservan `no-store`.
- Las altas administrativas de departamentos y ciudades invalidan la caché geográfica de la instancia después de guardar. Una consulta iniciada antes de la modificación no repuebla la revisión vigente con datos antiguos.
- Cambios directos en SQL o en otra instancia se observan al vencer el TTL. Un navegador que ya tenga respuesta fresca puede mostrar la versión anterior durante su ventana HTTP. No hay polling ni servicio nuevo para sincronizar estos catálogos.

La caché del servidor reduce consultas a PostgreSQL; la petición todavía llega a .NET. Reutilizar los resultados en React evita además repetir esas llamadas. Los límites generales por IP y concurrencia del paso 1 siguen aplicándose también a estos GET.

## Prueba en Swagger

1. Detener la API anterior, recompilar `Presentation` y ejecutar el proyecto. Abrir `/swagger` en su URL local.
2. En **Authorize**, quitar el token para probar las lecturas anónimas y su caché.
3. Ejecutar GET `/api/Departamento`. Esperar 200 y una lista con `idDepartamento` y `nombre`. Si la tabla no tiene datos, la lista será vacía.
4. Copiar un `idDepartamento` de esa respuesta y usarlo en GET `/api/Ciudad`. Esperar 200 y únicamente las ciudades de ese departamento.
5. Repetir los GET: los IDs se mantienen y las respuestas anónimas válidas incluyen `Cache-Control: public, max-age=30`. Esto comprueba el contrato HTTP; por sí solo no mide consultas de DB. La prueba automatizada con contador de ejecuciones comprueba que se reutiliza la respuesta.
6. En GET de ciudades, probar `invalid`, el UUID `00000000-0000-0000-0000-000000000000` y omitir el filtro: esperar 400 y `no-store`.
7. Elegir un departamento sin ciudades: esperar 200 y `[]`. No confundirlo con 503, que indica indisponibilidad del servicio.
8. Autorizar con un usuario registrado y llamar GET `/api/Usuario/me`. En PATCH `/api/Usuario/me`, enviar un `idCiudad` obtenido del catálogo usando la propiedad `ciudadId`:

```json
{ "ciudadId": "UUID_REAL_DE_LA_CIUDAD" }
```

Esperar 204; GET del perfil debe reflejar la ciudad. Un UUID inexistente debe devolver 400 sin modificar el perfil. El registro utiliza la misma propiedad. Este ensayo modifica la ciudad del perfil de prueba; al finalizar se puede restaurar su valor anterior con PATCH.

## Altas administrativas existentes

Se mantienen únicamente las operaciones ya presentes. Exigen `Admin` o `SuperAdmin`, aplican la política `escritura` y responden 200 al guardar. Sin token: 401. Usuario común: 403. Límite agotado: 429 con `Retry-After`.

POST `/api/Departamento`, contenido `application/json`, conserva un **string JSON**, no un objeto:

```json
"Nombre del departamento"
```

POST `/api/Ciudad` recibe `nombre` y `departamentoId` como parámetros query. Copiar el ID real del GET de departamentos. Normalización: se recortan espacios exteriores; el nombre resultante admite 1–100 caracteres y rechaza caracteres de control. Campos inválidos o departamento inexistente: 400. Nombre exacto repetido en el mismo ámbito: 409 por restricción única de PostgreSQL. Las comparaciones de unicidad siguen las reglas actuales de DB; no se introduce normalización de mayúsculas o acentos.

Para probar invalidación, usar una base de prueba: consultar el catálogo, crear un departamento con un administrador y volver a consultar; debe aparecer. Consultar sus ciudades vacías, crear una ciudad y volver a consultar; debe aparecer inmediatamente en esa instancia, salvo caché HTTP todavía fresca del cliente. La prueba automatizada consulta directamente la API y verifica ambos casos. Este paso no añade eliminación geográfica ni una segunda interfaz administrativa.

## Uso posterior desde React

Cargar departamentos al abrir el formulario que los necesita. Cargar las ciudades al seleccionar departamento y reutilizar ese resultado por UUID durante la sesión del formulario. Si cambia el departamento, borrar la selección anterior de ciudad y cancelar/descartar la respuesta anterior para no mostrar localidades del departamento equivocado.

Guardar `idDepartamento` e `idCiudad` como valores de los selectores; las etiquetas son `nombre`. El formulario envía `ciudadId` al registro/PATCH, no el nombre ni un UUID inventado. No solicitar estos catálogos desde cada tarjeta ni desde la portada institucional. Las lecturas públicas no deben heredar automáticamente el JWT del cliente HTTP privado.

Conservar los valores del formulario ante un error. Si la API rechaza una ciudad que ya no existe, volver a cargar el catálogo y pedir una selección válida. Un 429 o 503 no debe iniciar un bucle de reintentos; respetar `Retry-After` cuando esté presente.

## Verificación de esta entrega

La suite `scripts/verify-usuario.ps1` incluye `verify-geografia-cases.ps1`, utiliza PostgreSQL temporal en 127.0.0.1:55432 y datos sintéticos. Comprueba contratos, orden, UUIDs, Swagger, filtros, duplicados, permisos, invalidación tras altas, límites de escritura y uso del catálogo en el perfil. Las suites existentes de cuentas y participación siguen ejecutándose. Gmail permanece deshabilitado/simulado.

`scripts/ConsumptionChecks` comprueba también reutilización, expiración e invalidación del catálogo de departamentos y exclusión de JWT, cookies, queries extra y separación por origen. Estas comprobaciones no equivalen a una prueba de carga ni a una medición de costos en Azure.

Resultado: compilación Release con **0 errores y 0 advertencias**; **577 comprobaciones HTTP/PostgreSQL**, incluidas 72 del catálogo geográfico y dos adicionales de límites de escritura; **62 de consumo/worker/caché** y **45 de correo/dispatcher**. Total: **684 comprobaciones correctas**. Se mantuvieron los flujos existentes de cuentas, encuestas, solicitudes, afiliación, noticias y notificaciones. El PostgreSQL temporal se detuvo al terminar y no se enviaron correos reales.

Referencias técnicas: [Output caching en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-10.0) y [consultas eficientes en EF Core](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
