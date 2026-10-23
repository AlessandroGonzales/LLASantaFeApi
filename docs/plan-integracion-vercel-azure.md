# Guía de continuidad: frontend Vercel + API .NET + PostgreSQL en Azure

Fecha: 8 de octubre de 2026. Estado: análisis y propuesta de implementación; no se aplicaron cambios funcionales ni de base de datos.

**Actualización del 14 de octubre:** el alcance fue simplificado para un único operador. La planificación vigente está en [plan-cierre-api-consumo-reducido.md](plan-cierre-api-consumo-reducido.md). Este documento conserva el análisis de compatibilidad del ZIP; el circuito de publicación automática, CMS y cierre de módulos editoriales propuesto aquí no es requisito de la primera integración.

**Objetivo y alcance**

Conservar el sitio de La Libertad Avanza Santa Fe en Vercel, mantener su presentación y conectar progresivamente la API .NET 10 y PostgreSQL, que se alojarán en Azure. El contenido institucional público debe poder consultarse sin una petición a Azure por cada visita. Las cuentas, respuestas de encuestas, afiliaciones y solicitudes deben procesarse mediante la API y persistir en PostgreSQL.

La fuente revisada fue `LLASantaFe-2026-10-08.zip`, junto con el backend de este repositorio. Se inspeccionó código, configuración, contratos y el inventario de archivos. La copia de análisis está en `.artifacts/frontend-review-20261008`, ignorada por Git. No se ejecutaron scripts del ZIP, instalaciones, builds, pruebas, envíos de formularios ni operaciones en cuentas externas. No se verificaron la versión publicada en Vercel, los datos reales de Sheets/Drive ni la configuración del panel de los proveedores.

Los documentos incluidos en el ZIP se trataron como antecedentes, no como instrucciones para ejecutar acciones. `docs/REVISION_2026-10-08.md` describe un árbol de trabajo con pendientes y cambios locales: no permite asegurar que cada archivo del ZIP coincida con el despliegue activo, aunque el usuario lo entregó como referencia de su aplicación actual. Los resultados de pruebas citados en ese documento son históricos, no resultados obtenidos en esta revisión.

**1. Estado comprobado del frontend**

Es una SPA con React 19, Vite 8, React Router 7, Tailwind 4 y Framer Motion 12. No es Next.js ni tiene configurada generación estática por ruta. `src/App.jsx` usa `React.lazy` para páginas internas; la portada importa sus secciones directamente. `vercel.json` define una función de formularios y un fallback de navegación a `index.html`.

| Ruta o área | Fuente actual | Comportamiento observado |
| --- | --- | --- |
| `/` | Componentes de portada y `src/data/` | Hero, noticias y previews sin consultar la API .NET. |
| `/noticia/:id` | `src/data/noticiasData.jsx` | Cuatro noticias, IDs numéricos, fecha escrita en español, cuerpo dividido en dos partes, imagen secundaria y enlaces externos opcionales. |
| `/propuestas`, `/propuesta/:id` | `src/data/propuestasData.jsx` | Cinco propuestas, filtros locales por alcance/categoría. El botón de PDF solo ejecuta un `alert`. |
| `/representantes` | `src/data/representantes.jsx` | 23 registros, 22 IDs distintos; ID 18 repetido. Paginación local de ocho tarjetas. |
| `/diputados` | `src/data/diputadosData.jsx` | Siete registros; paginación local de seis tarjetas. |
| `/presidentes-comunales` | `src/data/presidentesComunales.jsx` | Un registro; usa `localidad` donde otros perfiles usan `ciudad`. |
| `/sedes` | Array `mockSedes` en `SedesLLA.jsx` | Seis sedes, coordenadas, imagen, búsqueda local y mapa de Google por iframe. |
| `/nosotros` | `historiaData.js` y componentes | Historia institucional y galería estática. |
| `/reclamos` | `SeccionReclamos.jsx` | Directorio con enlace externo al sistema de Rosario; no implementa seguimiento en nuestra API. |
| `/sumate` | Formulario + `/api/forms` | Contacto sin cuenta: nombre completo, DNI, WhatsApp, localidad y opción de fiscalización. |
| `/propone` | Formulario + `/api/forms` | Nombre, teléfono, email, profesión, motivo, mensaje y adjunto opcional; no requiere sesión. |

No aparecen rutas de login, registro de cuenta, perfil, encuestas, afiliación autenticada ni panel administrativo. Deben desarrollarse para utilizar esos módulos del backend. No confundir que exista un endpoint con que exista su pantalla.

El flujo actual de recepción tiene backend propio:

`React → función Vercel /api/forms → Turnstile + límites en Upstash Redis → mensaje firmado HMAC → Apps Script → Sheets y Drive privados`.

La función valida origen, formato, tamaño, campos y adjuntos. Tiene límites distribuidos, comprobación de Turnstile en servidor y una firma hacia Apps Script. React conserva un `requestId` para reintentos; Apps Script busca ese ID y una huella del contenido para evitar duplicados. Hay un bloqueo global del script durante la operación sobre Sheets/Drive. Este trabajo síncrono y las búsquedas sobre filas crecerán con los registros: es una razón concreta para migrar las escrituras, independiente del tráfico de lectura del sitio.

**2. Diferencias de contrato que deben resolverse antes de conectar**

| Área | Frontend actual | Backend actual | Acción |
| --- | --- | --- | --- |
| Sumate / cuenta | Sin email, contraseña ni fecha de nacimiento; nombre completo, DNI y localidad libre. | `POST /api/Usuario` requiere nombre, apellido, email, contraseña y fecha de nacimiento; ciudad por UUID. | Tratar contacto y cuenta como conceptos distintos. No fabricar cuentas ni contraseñas al importar. |
| Sumate / afiliación | Interés en participar/contacto; opción de fiscalización. | Afiliación requiere identidad autenticada y `confirmacion: true`. | No convertir automáticamente un contacto o una opción de fiscalización en afiliación. |
| Propone / identidad | Envía datos de contacto sin login. | Solicitud ciudadana requiere JWT; identidad tomada del usuario autenticado. | Mantener como base el requisito previo de usuario registrado. Diseñar login/retorno al formulario. Si se quieren conservar envíos anónimos, definir un flujo separado antes de implementarlo. |
| Propone / mensaje | Entre 10 y 10.000 caracteres. | Entre 10 y 5.000. | Para la primera integración, alinear interfaz a 5.000, sin truncar datos históricos. |
| Propone / archivos | PDF, DOC, DOCX, XLS, XLSX, ODT y TXT; 2 MiB; base64 en JSON. | Solo PDF, 2 MiB, `multipart/form-data`, carga posterior a crear la solicitud. | Mantener el alcance PDF acordado. Cambiar selección, ayuda y transporte del frontend. Otros formatos requieren una ampliación explícita. |
| Propone / respuesta | Espera `{ ok, id }`, y errores con `message`. | Creación devuelve 201; los errores utilizan diferentes contratos según el caso. | Implementar un adaptador HTTP y normalizar errores antes de reemplazar el hook. No asumir `result.ok`. |
| Noticias | `title`, `description`, dos cuerpos, imagen secundaria, enlaces sociales, fecha textual, ID entero. | `titulo`, `resumen`, un `contenido`, imagen principal, fecha UTC, UUID. | Mapear los campos; preservar imagen secundaria/enlaces con un contrato editorial definido. No perder contenido silenciosamente. |
| Detalle de noticia | URL pública de cada noticia. | Solo existe detalle administrativo; el GET público trae las últimas seis. | Añadir detalle público por ID estable, que excluya borradores, eliminadas y futuras. El exportador debe incluir noticias antiguas enlazables, no solamente seis. |
| Representantes | Nombre completo, edad fija, ciudad/localidad, teléfono, redes y fotos con variantes. | Nombre/apellido, departamento, foto única y varias redes; sin ciudad ni teléfono en el modelo scaffold actual. | Definir cargo/tipo, ámbito y ubicación; campos públicos de contacto; variantes de imagen. No deducir tipo analizando el texto de `cargo`. |
| Propuestas | `alcance`, `categoria`, `bajada`, `detalle`. | Título, descripción, estado, temática, fechas, departamento y URLs; no resumen ni ciudad. | Definir resumen/detalle, ámbito provincial/local y referencias geográficas sin confundir ciudad con departamento. |
| Sedes | `id`, ciudad legible, `defaultVisible`, dirección, imagen y coordenadas. | El DTO de lectura no incluye `id` ni nombre de ciudad. | Incorporar identidad estable y ciudad legible; resolver orden/destacadas para mantener la portada. |
| Localidades | Texto libre. | Ciudad por UUID; GET de ciudades por departamento, pero falta GET de departamentos. | Completar catálogo público acotado y cacheable, y mapeo de nombres históricos. |

No resolver nombres compuestos dividiendo automáticamente por el primer espacio. No importar `desconocido` como email o edad. Corregir claves inconsistentes como `telefon` y conservar un informe de filas que requieren revisión. Los IDs de diputados, concejales y presidentes se repiten entre archivos; una clave de importación debe incluir la colección, y el duplicado interno de concejales necesita una clave adicional revisada.

Las fichas de la web son datos de entrada para la migración; esta revisión no verificó la exactitud factual de biografías, cargos o noticias. Su publicación definitiva debe conservar el control editorial del cliente.

**3. Arquitectura recomendada para la primera integración**

Conservar una API modular con las cuatro capas actuales y el DbContext existente. No hacen falta microservicios para este alcance.

| Capa de entrega | Decisión propuesta |
| --- | --- |
| Sitio público | React/Vite en Vercel, con contenido público generado durante el build. |
| Fuente editable | PostgreSQL, administrada mediante endpoints protegidos de .NET. |
| Recursos actuales | Logo, hero, fotografías y documentos públicos editoriales siguen en Vercel. |
| Datos privados | API .NET y PostgreSQL en Azure. No se exportan al sitio. |
| Escrituras desde React | HTTPS directo a la API Azure; evitar otro salto por Vercel para cada operación nueva. |
| Función `/api/forms` | Se conserva mientras tenga formularios activos. Se retira por flujo después de una transición comprobada. |
| PDFs ciudadanos | Privados, con autorización y análisis; nunca dentro del despliegue público de Vercel. |

Una visita que solo lee noticias, propuestas o perfiles debe obtener los archivos desde Vercel, sin llamadas a Azure para ese contenido. Esta meta es verificable en la pestaña Network. Las operaciones de cuenta sí dependen de Azure.

El contenido en base de datos y su copia pública tienen propósitos distintos: una es la fuente editable; la otra es un artefacto de publicación. La copia se regenera automáticamente, no se mantiene manualmente en dos lugares.

Vercel no cachea automáticamente una llamada del navegador a un dominio de Azure. Hay que generar el contenido en Vercel o configurar explícitamente una ruta cacheada. Para esta SPA y un catálogo editorial pequeño, se recomienda comenzar por generación durante el build, sin introducir un proxy dinámico obligatorio. [Vite en Vercel](https://vercel.com/docs/frameworks/frontend/vite).

**4. Publicación de contenido: flujo concreto**

1. Un administrador guarda/publica/modifica/retira contenido mediante la API. PostgreSQL confirma el cambio.
2. La misma transacción registra que existe una revisión pendiente de publicación. Un worker procesa esa marca con reintentos acotados. No usar una tarea en memoria lanzada sin seguimiento.
3. Se agrupan cambios cercanos para evitar un build por cada PATCH. Guardar borradores no debe provocar despliegues públicos. Separar revisión deseada y revisión efectivamente publicada.
4. El worker solicita un build del frontend mediante un Deploy Hook privado. La URL se trata como secreto y nunca se entrega al navegador.
5. El build obtiene de la API una instantánea coherente de contenido exclusivamente público, con versión de contrato y revisión. No conecta directamente a PostgreSQL ni usa un JWT de administrador.
6. Se generan JSON de portada, listados y detalles separados. El build valida referencias, IDs, enlaces, tipos, límites de tamaño y ausencia de campos privados. No empaquetar todas las biografías y noticias completas dentro del JavaScript inicial.
7. Solo una compilación válida puede reemplazar la publicación anterior. Registrar éxito/fallo y revisión publicada; pedir un despliegue no demuestra que haya terminado. Usar comprobación autenticada del estado del despliegue o un callback autenticado; ignorar notificaciones antiguas.
8. Ante fallo, conservar la publicación anterior y mostrar al administrador que la nueva versión sigue pendiente. Evitar que un build viejo reemplace una revisión más nueva.

Para una primera versión, un exportador público específico puede entregar la instantánea en una respuesta con tamaño máximo explícito, dado el catálogo pequeño. Al crecer, paginar por revisión consistente. Si se supera el límite, fallar con un diagnóstico y ampliar el diseño; nunca truncar silenciosamente. Las seis noticias de portada son un subconjunto, no todo el archivo editorial.

Este exportador y el worker aún no existen. Deben compartir reglas de publicación con los endpoints públicos y excluir borradores, futuras y eliminadas. DTOs explícitos, no serialización de entidades EF completas.

Las fechas futuras necesitan un trabajo programado que solicite publicación al alcanzar la fecha. Retirar o corregir contenido necesita el mismo circuito y una vía de publicación urgente. La actualización pública tardará lo que tarde el build más la política de caché: se propone una meta inicial de cinco minutos en operación normal, pendiente de medir; no una garantía actual. Una retirada urgente no puede depender de una caché de larga duración sin invalidación.

Un Deploy Hook reconstruye el sitio desde su repositorio/rama; no modifica por sí mismo la base de datos ni reemplaza un archivo del despliegue existente. Requiere proyecto conectado a Git. Hay que comprobarlo en Vercel cuando configuremos la integración. [Deploy Hooks](https://vercel.com/docs/deploy-hooks).

La primera etapa puede generar JSON y conservar el renderizado cliente. Para mejorar el HTML inicial de noticias/propuestas y las vistas al compartir, añadir prerenderizado por ruta en una entrega posterior. El código actual usa `sessionStorage` directamente durante algunos renderizados y metadata en `useEffect`; hay que adaptarlo antes de renderizar en servidor. Vite por sí solo no convierte esta SPA en un sitio con ISR. No se propone migrar a Next.js como requisito.

**5. Caché, recursos y consultas**

| Recurso | Política inicial propuesta |
| --- | --- |
| JS/CSS/imágenes con nombre que incorpora hash | Caché larga e inmutable; nombre distinto al cambiar contenido. |
| HTML y referencias a la revisión actual | Revalidación o TTL corto. No marcar inmutable el `index.html`. |
| JSON público versionado | Cacheable; referencias y detalles de una misma revisión. |
| GET público en .NET | OutputCache acotada, por ejemplo 60 segundos, con invalidación por cambios; ajustar expiración a publicaciones programadas. |
| Perfil, encuestas personalizadas, afiliación, solicitudes, PDF y administración | `no-store`; sin caché compartida. |

El backend actualmente impone `Cache-Control: no-store` globalmente y en Noticias. Debe sustituirse selectivamente en los GET públicos, manteniéndose en todos los privados. `OutputCache` debe tener límites de memoria y claves controladas: no variar por cualquier query string arbitraria. Las entradas paginadas y búsquedas necesitan validación y cardinalidad acotada.

La caché de salida evita repetir la consulta, pero la petición todavía llega a .NET. El contenido estático de Vercel elimina esa petición de las visitas públicas. Con varias instancias, definir invalidación entre ellas o tolerar explícitamente un TTL máximo corto; una invalidación de memoria local no alcanza otras instancias. [Output caching en .NET 10](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-10.0).

Se conservarán las optimizaciones existentes: rutas lazy, imágenes WebP con variantes, `loading="lazy"`, tamaño de tarjetas acotado y reproducción del hero sensible a movimiento reducido/ahorro de datos. La paginación local actual evita montar todas las tarjetas; no evita descargar los datos que ya vienen importados en JavaScript. Para 31 perfiles, filtrar un JSON pequeño localmente puede ser más simple que consultar la API en cada filtro. Si el catálogo crece, se divide por categoría/página sin cargar detalles completos en la portada.

El video móvil actualmente importado pesa 4.463.789 bytes (aproximadamente 4,46 MB decimales). Diez mil descargas completas equivaldrían a unos 44,6 GB de transferencia, solo de ese archivo. Es una estimación, no una medición de visitas, reproducción o facturación. El tamaño total del ZIP, unos 63,24 MiB sin compresión, incluye originales y evidencias; no representa el peso de una página ni el tamaño final del despliegue.

Mantener el video y las imágenes fuera de los endpoints de negocio. El ahorro principal se complementa comprimiendo medios y evitando descargas innecesarias; una CDN sigue transfiriendo bytes. Para imágenes nuevas administradas por usuarios internos, hará falta almacenamiento persistente de archivos: no prometer que un POST a .NET pueda escribir en `src/assets` de un despliegue de Vercel. Ese flujo queda para una ampliación posterior; los recursos actuales pueden seguir en el repositorio del frontend.

Al migrar imágenes, no guardar rutas temporales del equipo ni tratar imports de Vite como URLs permanentes. Proponer un manifiesto de recursos y rutas públicas estables/versionadas, como `/media/...`, resueltas contra el dominio del sitio. El build comprueba que los recursos referenciados existan. No hacer un proxy que descargue URLs arbitrarias suministradas por usuarios.

**6. Seguridad y reintentos que deben conservarse al migrar formularios**

La protección actual en Vercel no acompaña automáticamente a un formulario cuando empieza a llamar directamente a Azure. Antes del corte deben existir controles equivalentes en el destino: límites antes de operaciones costosas, validación del cuerpo, autorización, cuotas y protección contra automatización en los endpoints públicos que la necesiten.

Turnstile debe verificarse en servidor, incluyendo hostname y acción esperados. El widget del navegador no basta. Si lo verifica .NET, el token se consume allí; no volver a validarlo también en Vercel. Mantener el secreto fuera de React. [Validación de Turnstile](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/).

La API actual tiene un límite global de ocho peticiones simultáneas por instancia, sin cola, y políticas por IP. Es una configuración inicial, no la capacidad real del servicio. Separar transferencias/escaneo de PDF, autenticación y lecturas; calibrar límites mediante carga. El middleware de rate limiting está antes de autenticación, lo que evita hacer la consulta de validación JWT para solicitudes ya rechazadas. Las cuotas guardadas en PostgreSQL y los límites en memoria tienen alcances diferentes; con varias instancias, los límites en memoria no son globales.

Configurar proxies confiables de Azure antes de usar la IP del visitante. No confiar en un `X-Forwarded-For` arbitrario ni copiar sin adaptación la cabecera específica de Vercel. CORS debe permitir únicamente orígenes necesarios; no protege frente a clientes que no son navegadores. El endpoint Azure seguirá siendo accesible directamente y necesita sus propias defensas aunque exista un frontend en Vercel.

Añadir idempotencia a la creación de solicitudes antes de permitir reintentos automáticos: clave por usuario y operación, huella del cuerpo, respuesta persistida y restricción única transaccional. Misma clave/cuerpo devuelve el resultado anterior; misma clave con otro cuerpo produce conflicto. Las solicitudes concurrentes deben crear una sola fila. No consumir otra cuota de negocio por repetir un envío ya confirmado. Definir retención y límite de tamaño del registro de idempotencia. Esto es un trabajo pendiente; las restricciones de respuesta única/afiliación existentes no sustituyen este mecanismo general.

Para `/propone`, el nuevo frontend hará dos pasos: crear solicitud JSON y adjuntar PDF con `FormData` usando `archivo`. Si la carga falla, conservar el ID y reintentar únicamente el archivo dentro de los límites existentes; no recrear la solicitud. Un timeout es resultado incierto, no prueba de que no se guardó. Verificar estado antes de insistir. Mostrar claramente solicitud recibida con adjunto pendiente. No fijar manualmente el boundary de `multipart/form-data`.

Los límites actuales son un PDF opcional de 2 MiB, tres solicitudes por usuario cada 24 horas y cuotas adicionales de intentos/almacenamiento. No aumentarlos como efecto secundario de adaptar el frontend. La inspección estructural de documentos en el ZIP no equivale al antivirus obligatorio de la API. ClamAV existe en el backend; el reemplazo por análisis nativo de Azure no está implementado. Definir y comprobar ese servicio antes de habilitar adjuntos en producción. Si se elige análisis asíncrono, habrá que añadir cuarentena y estado pendiente; no marcar un archivo disponible antes del resultado.

Diseñar sesión antes de añadir pantallas privadas: el JWT actual es corto y no hay refresh implementado. No prometer sesión persistente ni introducir tokens en URLs. Decidir explícitamente entre token en memoria con nuevo login al expirar o una ampliación de sesión; cookies requieren resolver CSRF, dominios y credenciales. No guardar credenciales de DB, Gmail, Turnstile secreto o Deploy Hooks en variables `VITE_*`, porque Vite las incorpora al cliente. [Variables de Vite](https://vite.dev/guide/env-and-mode).

**7. Hallazgos del ZIP que conviene corregir antes de la integración**

- `Hero.jsx` pasa solo la fuente móvil al selector; la rama de escritorio puede recibir una fuente ausente. El controlador llama `onPauseChange`, pero el componente no proporciona ese callback. Son discrepancias comprobables en código; no se reprodujeron en navegador en esta revisión.
- Hay un ID 18 duplicado en concejales. Normalizar claves antes de crear UUIDs y conservar un mapa de importación; no eliminar una persona para resolver el duplicado.
- El manifiesto de imágenes optimizadas debe cubrir los perfiles añadidos; el fallback a la foto original conserva funcionalidad, pero puede aumentar transferencia. Comprobar el manifiesto completo en la entrega de imágenes.
- `NoticiasDetalle.jsx` usa `dangerouslySetInnerHTML` sin sanitización en ese punto. Antes de recibir contenido editable por API, definir HTML con lista permitida y sanitización mediante biblioteca mantenida, o bloques estructurados renderizados por React. Una expresión regular para quitar etiquetas no es un sanitizador. La autorización del editor no elimina esta necesidad.
- Unificar fechas ISO y UUIDs. Preservar enlaces existentes `/noticia/1` y `/propuesta/1` mediante un mapa de rutas antiguas y redirecciones; no romper URLs compartidas. No usar `parseInt` para los nuevos IDs.
- El botón de descarga de propuesta debe usar un documento real validado o no ofrecer descarga cuando falte. Un PDF público editorial no es el PDF privado de una solicitud.
- Preparar 404 propia, sitemap completo y metadata inicial por página cuando se implemente prerenderizado. El sitemap actual contiene tres rutas; la metadata de noticias se aplica en cliente.

No es necesario corregir todos los detalles de presentación para comenzar el contrato de la API. Sí deben cerrarse las incompatibilidades de identidad, contenido, archivos y URLs antes de sustituir fuentes de datos en producción.

**8. Entregas propuestas, entidad por entidad**

| Orden | Entrega | Trabajo principal | Criterio de terminación |
| --- | --- | --- | --- |
| 0 | Contrato de integración | Resolver contacto Sumate frente a cuenta; ratificar sesión/PDF; documentar DTOs y mapa de URLs. | Ningún campo del frontend queda descartado sin una decisión explícita. |
| 1 | Noticias | DTO tarjeta/detalle, detalle público, contenido seguro, caché selectiva, importación de las cuatro noticias y sus recursos. | Una noticia anterior a las últimas seis sigue accesible; no se filtran borradores/futuras; imagen secundaria y fuentes se conservan. |
| 2 | Publicación Vercel mínima | Exportación pública versionada, generación JSON en build y actualización manual controlada inicialmente; después worker + hook + seguimiento. | Navegación de noticias sin llamadas de lectura a Azure; falla de build conserva versión anterior; revisión visible. |
| 3 | Representantes | Unificar 31 perfiles, identificar cargo/ámbito/ciudad, contactos públicos y fotos; administración estrictamente necesaria. | Tres secciones actuales funcionan con el mismo catálogo, sin IDs repetidos ni pérdida de filtros. |
| 4 | Propuestas | Resumen/detalle, alcance geográfico, temática, publicación y documento opcional real. | Se mantienen filtros y URLs; una descarga existe o no se ofrece. |
| 5 | Sedes y geografía | ID/ciudad en DTO, orden o destacadas, GET departamentos y catálogo de ciudades. | Mapa/búsqueda actuales y selección de localidad funcionan sin peticiones por tarjeta. |
| 6 | Cuentas y formularios | Pantallas de cuenta/sesión; conectar solicitud + PDF y afiliación; idempotencia, antiautomatización y errores. | Reintentos no duplican; aislamiento entre usuarios; fallos de archivo no pierden el envío. |
| 7 | Encuestas | UI por contrato JSON v1 ya implementado, validación y estados. | Una sola respuesta por cuenta/encuesta, controles por tipo y cierre efectivo. |
| 8 | Migración histórica y cambio de destino | Importador, conciliación, prueba de corte y retiro gradual de Sheets por flujo. | Cantidades/adjuntos conciliados, registros no mapeables conservados y destino de cada formulario inequívoco. |
| 9 | Preparación Azure y producción | Permisos mínimos, red, secretos, backups, antivirus, monitoreo y carga representativa. | Integración completa probada en un entorno separado y procedimiento de recuperación documentado. |

El entorno de integración Azure puede prepararse antes para probar entregas; no implica cambiar la web productiva. La entrega 2 también puede probarse localmente con una exportación pública de prueba antes de configurar proveedores. Cada cambio de esquema se entrega como SQL revisable, se aplica siguiendo el procedimiento acordado y se actualizan los mapeos necesarios; no ejecutar scaffolding destructivo ni generar un segundo DbContext.

La captura pública de contactos de Sumate, si se conserva en el sistema nuevo, necesita un caso de uso y persistencia propios, separados de Usuarios y Afiliación. Su ubicación en la secuencia depende de esa decisión de producto. Eventos/asistencia quedan para una entrega posterior: el ZIP no contiene una pantalla de asistencia y el backend no tiene ese flujo completo.

**9. Migración de Sheets y Drive sin pérdida ni duplicación**

Mantener operativa la versión actual hasta que el destino nuevo esté probado. No importar datos personales desde el ZIP: no contiene el dataset de Sheets/Drive.

Preparar un export privado con IDs de origen, fechas, estados, campos y referencias a adjuntos. Inventariar filas y archivos, validar en modo de simulación y producir un reporte de coincidencias/errores sin datos personales en logs públicos. La importación debe poder repetirse con una clave única por sistema/colección/ID de origen.

Sumate no tiene los campos necesarios para crear cuentas. Propone tiene email pero no demuestra titularidad ni proporciona credenciales. Los históricos se conservan como registros de origen o solicitudes históricas con identidad no vinculada hasta que exista un procedimiento válido de asociación. No inventar usuarios, fechas de nacimiento, UUIDs de ciudad ni consentimientos.

Los mensajes de más de 5.000 caracteres y los adjuntos históricos que no son PDF requieren preservación y tratamiento explícito; no truncar ni descartar para ajustarlos al contrato nuevo. Mantener Drive privado durante esa transición. Registrar qué archivos se migraron, cuáles siguen en el archivo histórico y cuáles requieren revisión.

Una importación no debe disparar los correos de bienvenida/aprobación como si fuesen acciones nuevas. El backend tiene triggers de encolado: el procedimiento debe contemplarlos expresamente y verificarse en una copia, sin deshabilitar controles globales en producción por conveniencia.

En el corte, fijar destino por formulario y registrar el momento/identificador de transición. Contemplar pestañas antiguas abiertas que todavía envían a `/api/forms`, formularios en vuelo y un lote final de conciliación. No implementar doble escritura indefinida. Para volver atrás, no reenviar silenciosamente operaciones de resultado incierto a Sheets: podrían existir ya en PostgreSQL.

**10. Verificación y operación antes de producción**

No se estima capacidad a partir de 10.000 visitas semanales. Separar sesiones, solicitudes, transferencia y picos. Un escenario ilustrativo de 1.000 visitas en cinco minutos con diez llamadas cada una promedia unas 33 solicitudes/segundo; no es una prueba ejecutada ni una meta acordada. Con publicación estática, las lecturas editoriales de ese escenario no deberían alcanzar Azure.

Comprobar en las entregas correspondientes:

- Contratos, permisos, sanitización de contenido y no exposición de campos privados en artefactos públicos.
- Navegación pública con Azure temporalmente inaccesible: debe seguir mostrando la última publicación estática. Los formularios deben informar indisponibilidad sin confirmar envíos falsamente.
- Publicación, edición, retiro, fecha futura, build fallido, reintentos y carreras entre revisiones.
- Registro/login/perfil, solicitudes con y sin PDF, reintentos, autorización de descargas, afiliación y encuestas.
- Restauración de backups y reconciliación de migración en un entorno separado.
- Latencia p95/p99, errores 5xx, 429 legítimos, CPU/memoria, conexiones/esperas DB, tiempos de consulta, tamaño/transferencia y cantidad/duración de builds.
- Picos de login y PDFs por separado de lecturas. Correo y escaneo simulado para cargas de prueba; no enviar correos reales en masa ni usar datos reales.

Alertas mínimas: API indisponible, errores sostenidos, saturación de conexiones, publicación pendiente/fallida, cola de correo retrasada y almacenamiento cercano a cuota. Revisar presupuestos de Vercel y Azure por separado; las alertas de gasto no equivalen a un bloqueo universal del consumo.

No contratar Redis, Front Door o microservicios por anticipación como requisito de esta guía. Upstash ya existe en el flujo legado; conservarlo hasta reemplazar sus controles. Si escalamos .NET a varias instancias, habrá que resolver expresamente límites e invalidación compartidos. Ajustar consultas e índices con evidencia y planes de ejecución; no sumar índices sin un patrón de consulta que los justifique.

**11. Decisiones abiertas y siguiente acción**

Decisiones confirmadas: Vercel permanece; API .NET 10 y PostgreSQL van a Azure; trabajamos entidad por entidad; el alcance actual de solicitudes conserva un PDF opcional de 2 MiB y tres solicitudes por usuario cada 24 horas.

Propuestas de esta guía, todavía no implementadas: contenido público generado en build, exportador versionado, seguimiento de publicaciones, DTOs públicos ajustados, caché selectiva e idempotencia de solicitudes.

Decisiones que debe resolver el producto antes de sus entregas: si Sumate seguirá aceptando contactos sin cuenta; si Propone pasará a requerir login como el backend actual; si el plazo de actualización pública tras un build satisface la operación editorial. También faltan la frecuencia real de publicación y el plan/configuración de Vercel, que influyen en builds y costos.

Siguiente entrega técnica recomendada: **Noticias**. Es el módulo que ya tiene administración en la API y pantallas públicas en el frontend. Primero cerrar tarjeta/detalle, seguridad del contenido, referencias de imágenes y compatibilidad de URLs; luego usarlo para probar el circuito de publicación estática. No comenzar implementando todos los módulos ni cambiando el registro productivo.
