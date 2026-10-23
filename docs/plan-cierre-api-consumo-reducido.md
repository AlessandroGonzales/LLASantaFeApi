# Plan de cierre de la API y consumo reducido

Fecha: 14 de octubre de 2026. Este documento ajusta el alcance a un único operador y prevalece sobre la estrategia editorial propuesta el 8 de octubre en `plan-integracion-vercel-azure.md`. Es una planificación: no modifica código ejecutable, esquema ni servicios externos.

Avance del paso 1: configuración de límites, pools, espera del correo y caché de los catálogos existentes implementada; consultar [consumo-api.md](consumo-api.md) para valores, comportamiento y verificación.

Avance del paso 2: contrato de sesión, cierre de sesión, verificación de cuentas nuevas y recuperación implementados en API; requieren SQL 007. Ver [usuario-sesion-recuperacion.md](usuario-sesion-recuperacion.md). Las pantallas React y su integración quedan para la entrega de frontend.

Avance del paso 3, 20 de octubre de 2026: GET público de departamentos y ciudades ordenadas, con UUID persistido y caché geográfica. Altas existentes protegidas, validadas e invalidación después de guardar. No requiere SQL nuevo. Contrato y pruebas en [catalogo-geografico-api.md](catalogo-geografico-api.md).

Avance del paso 4, 20 de octubre de 2026: idempotencia persistente del texto y PDF con `Idempotency-Key`, consulta de metadatos, preservación del mensaje y recuperación tras fallos. Requiere SQL 008. Se mantiene ClamAV/bytea acotado en esta etapa; Blob/Defender se implementará antes del despliegue, durante el paso 8. Ver [solicitud-ciudadana-idempotencia.md](solicitud-ciudadana-idempotencia.md).

Avance del paso 5, 21 de octubre de 2026: contrato propio/React documentado, rechazo definitivo con motivo público y auditoría, bandeja de en_revision y aprobación protegida/atómica con su cola de correo. Se elimina la lectura de contacto previa a resolver; se conserva la solicitud única por cuenta y los totales agregados. Requiere SQL 009; no añade paquetes ni servicios. Ver [afiliacion-api.md](afiliacion-api.md). Sigue el paso 6 (Encuesta) y posteriormente la integración React. La prueba general entidad por entidad se mantiene para el final de las entregas, como pidió el usuario.

**Decisión recomendada**

Frontend React/Vite en Vercel para contenido institucional público. API .NET 10 y PostgreSQL en Azure para cuentas, participación, validación, autorización y persistencia. Una sola persona publica el contenido; se propone hacerlo desde archivos del repositorio del frontend y desplegar una nueva versión. Conservar la administración mínima requerida por encuestas, solicitudes y afiliaciones.

La publicación desde archivos es una recomendación basada en la forma de trabajo indicada por el usuario. No se ha cambiado el frontend ni eliminado ningún endpoint editorial. Si más adelante se elige editar contenido desde la API, se reemplaza esa fuente por exportación explícita de contenido público; no se mantienen dos versiones editables en paralelo.

**Estado de la revisión inicial del 14 de octubre**

Se revisaron la organización de las cuatro capas, controladores, servicios, repositorios, contratos, validadores, autenticación, servicios externos, mapeos del DbContext y scripts SQL, y se contrastaron con el frontend extraído del ZIP del 8 de octubre. No se inspeccionaron secretos locales, datos personales ni cuentas externas durante esa revisión. La tabla siguiente registra ese diagnóstico inicial; los avances y pruebas posteriores se detallan arriba y en las guías de cada entrega.

| Área | Estado del código | Consecuencia para este alcance |
| --- | --- | --- |
| Usuarios | Registro/login, perfil, PATCH y baja. JWT con consulta de vigencia en DB, BCrypt y bloqueo por intentos. | Reutilizar; cerrar sesión y recuperación/verificación de identidad antes de producción. |
| Encuestas | Borrador, publicación, cierre, revisión, contrato JSON v1 y una respuesta por cuenta. | Reutilizar; frontend de encuestas pendiente. |
| Solicitudes ciudadanas | Creación autenticada, consulta, gestión y PDF con cuotas y ClamAV. | Cerrar reintentos idempotentes y almacenamiento/análisis de producción. |
| Afiliaciones | Solicitud única por cuenta, pendientes, contacto, aprobación y totales. | Reutilizar; no convertir contactos Sumate en afiliaciones. Rechazo no tiene endpoint actualmente. |
| Gmail | Cola persistente y worker; bienvenida y aprobación de afiliación. | Reducir consultas cuando está vacía y mantener procesamiento fuera de la petición. |
| Noticias | POST/PATCH administrativo y GET de últimas seis. | Conservar lo construido; la web no dependerá de ese GET para cada visita si su fuente es el repositorio. |
| Sedes/geografía | Sedes implementadas; ciudades por departamento; falta GET de departamentos y DTO de sedes sin ID. | Completar únicamente lo que necesiten los formularios y la operación elegida. |
| Representantes/propuestas/eventos | Entidades existentes; controladores y varios servicios/repositorios vacíos. | No son flujos implementados. Editorial estático no exige cerrar su CRUD para integrar la web. Asistencia a eventos queda fuera de esta primera entrega. |

Se mantiene un único `LLASantaFeDbContext`, .NET 10, PostgreSQL 16 configurado y las dependencias actuales. Los scripts 001–006 existen; su presencia no sustituye una auditoría de qué versiones tiene cada base. No se reejecutan a ciegas ni se usa `EnsureCreated` para reconstruir la base.

**Distribución de responsabilidades**

| Información o acción | Responsable |
| --- | --- |
| Logo, historia, hero, fotografías y estilos | Archivos públicos del frontend en Vercel. |
| Noticias, propuestas, representantes, postulantes y sedes publicados | Datos locales/JSON del frontend, editados por el operador y versionados en Git. |
| Mostrar tarjetas, buscar en un catálogo pequeño, filtrar y paginar visualmente | React; sin petición al backend por tarjeta o pulsación. |
| Registro, login, perfil y baja | API y PostgreSQL. |
| Disponibilidad real de encuestas, validación de respuestas, unicidad y cierre | API y PostgreSQL. React presenta el formulario, pero el servidor decide si acepta. |
| Crear una solicitud, verificar titularidad y gestionar su estado | API y PostgreSQL. |
| Aceptar una afiliación, auditar quién la resolvió y obtener totales | API y PostgreSQL. |
| Correo transaccional | Worker y cola persistente del backend. |
| Archivo ciudadano privado | Servicio de almacenamiento/análisis con acceso autorizado; nunca publicado en el frontend. |
| Estadísticas y exportaciones operativas | Consultas administrativas acotadas; no ejecutar análisis grandes en cada visita. |

Una visita pública de lectura debe producir cero peticiones a Azure para obtener noticias, propuestas, perfiles y sedes. Una visita a una función privada llama a la API solamente al necesitar datos o confirmar una operación. Esta es la meta verificable, no una afirmación sobre la aplicación desplegada actual.

**Publicación editorial sencilla**

El operador edita los datos, incorpora recursos optimizados, revisa la vista previa y publica una nueva versión del frontend. Identificadores estables, fechas ISO y enlaces existentes se conservan. Las seis noticias de portada se obtienen ordenando/seleccionando datos locales; el archivo de detalles mantiene accesibles las noticias anteriores.

La fuente editorial activa será el repositorio del frontend durante esta etapa. Las tablas y endpoints existentes permanecen disponibles, pero no se exige copiar manualmente cada modificación a PostgreSQL ni sincronizar una segunda fuente. Si una función futura requiere enlazar una operación privada a un evento o publicación, se registra entonces la identidad mínima correspondiente en DB.

La estrategia anterior de un worker editorial, Deploy Hooks automáticos, revisiones de exportación y seguimiento de builds pasa a ser una opción futura. Un operador que publica desde Git puede usar el circuito de despliegue de Vercel sin construir ese subsistema. La limitación es explícita: cambiar contenido exige un build/despliegue; la fecha futura no lo publica automáticamente sin una acción o programación adicional.

Vite produce recursos estáticos y puede alojarse en Vercel. No es necesario migrar a Next.js para esta etapa. [Vite en Vercel](https://vercel.com/docs/frameworks/frontend/vite).

**Administración mínima**

Mantener endpoints protegidos para crear/publicar/cerrar encuestas, consultar resultados paginados, revisar/resolver solicitudes y consultar/aprobar afiliaciones. Usar un cliente HTTP local autenticado contra la API; Swagger se conserva para desarrollo, según el comportamiento actual. No abrir Swagger públicamente en producción como sustituto de un flujo de acceso seguro.

No hace falta construir ahora un panel editorial, un sistema de aprobaciones entre varias personas ni administración pública de roles. Que exista un solo administrador no elimina los permisos: las cuentas registradas no pueden aprobar solicitudes ni acceder a contactos o respuestas ajenas.

Los endpoints editoriales ya escritos no se eliminan en esta entrega. Los módulos vacíos se consideran pendientes fuera del alcance, no funcionalidad completada. Actualizar DI o limpiar esqueletos solo si mejora el mantenimiento sin romper contratos usados.

**Hallazgos de la revisión inicial y resolución del paso 1**

1. `Presentation/Program.cs` aplica `no-store` global. Es correcto para datos privados. Incorporar caché selectiva únicamente al catálogo geográfico u otros GET públicos que se utilicen; no agregar infraestructura de caché para contenido que nunca consulta la API.
2. `ApiRateLimits.cs` fija ocho peticiones simultáneas por instancia, sin cola. Es una protección inicial, no una medida de capacidad. Configurar y separar límites para lecturas, autenticación y transferencias antes de calibrarlos con pruebas de carga. Mantener el filtro temprano antes de validar JWT/consultar DB.
3. Cada JWT validado consulta vigencia/rol en PostgreSQL. Conserva revocación inmediata. La primera reducción es evitar enviar JWT o consultar `/me` en páginas públicas y reutilizar el perfil en la sesión cliente. No quitar el control de vigencia para ahorrar una consulta ni cachearlo sin definir el retraso de revocación.
4. `CorreoWorker.cs` sondeaba la cola cada 15 segundos: unas 5.760 comprobaciones diarias en reposo. El paso 1 implementó señal local después de confirmar trabajos y espera progresiva hasta 300 segundos, manteniendo recuperación periódica y la cola durable en PostgreSQL. Los intervalos y límites vigentes están en `consumo-api.md`.
5. `solicitud_pdf` guarda el archivo completo como `bytea`. Los listados evitan leerlo, pero ocupa almacenamiento, backups y transferencia de DB. El tope actual de 100 MiB limita el contenido agregado: equivaldría a unas 50 cargas de 2 MiB. Es capacidad acotada, no almacenamiento ilimitado ni tamaño total de la base.
6. Las cuotas de solicitudes/archivos usan bloqueos globales transaccionales para asegurar contadores. Es correcto para el alcance actual, pero serializa esas escrituras. Mantener transacciones breves y no introducir llamadas a servicios dentro del bloqueo; optimizar esos contadores solo si la medición muestra espera relevante.
7. El pool del DbContext y el pool de conexiones son distintos. El paso 1 fija 20 conexiones para API y 2 para correo, con mínimo cero y timeouts explícitos; el pool de objetos EF es de 32. No multiplicar réplicas sin contemplar ese presupuesto agregado en PostgreSQL.
8. El frontend legado tiene Turnstile verificado en Vercel y límites distribuidos en Upstash. Un formulario que cambia su destino a Azure deja de pasar por esos controles. Implementar validación del token y límites necesarios en el nuevo destino antes del corte; CORS no reemplaza esa protección.

**Orden de trabajo para culminar esta API**

| Paso | Trabajo concreto | Condición para avanzar |
| --- | --- | --- |
| 1 | Ajustar configuración de consumo: concurrencia por operación, límites/pools explícitos, espera del correo y caché geográfica limitada. | Operaciones privadas mantienen autorización, cuotas y revocación; no se reduce seguridad como optimización. |
| 2 | Cerrar integración de Usuario: contrato de registro, sesión y expiración; recuperación de cuenta y comprobación de email; acceso administrativo definido. | Registro/login/perfil/baja funcionan desde un cliente real con errores claros y sin credenciales en el frontend. |
| 3 | Completar catálogo geográfico: GET de departamentos, ciudades con IDs estables, respuesta pequeña y cacheable. | Formularios no inventan UUIDs; el servidor valida las referencias aunque el cliente reutilice datos cacheados. |
| 4 | Cerrar SolicitudCiudadana: idempotencia y flujo JSON → PDF; transporte binario, estados y producción de archivos. | Un reintento no crea otra solicitud ni consume una nueva cuota de negocio para un resultado ya confirmado; archivo fallido no pierde el mensaje. |
| 5 | Cerrar Afiliación: contrato con React, estado propio, contactos administrativos y decisión final. | Backend implementado. Se añade rechazo para cerrar solicitudes que no deben aprobarse, con motivo público separado de notas internas y auditoría. Solicitud única, aprobación y correo atómicos. |
| 6 | Integrar Encuesta: renderizado de tipos v1, respuesta única y consulta bajo demanda. | Backend y contrato v1 cerrados; guía para los cuatro controles React, comprobante propio de envío, validación de fechas/estado/configuración y listas/respuestas paginadas. Pantallas conectadas en el paso 7. |
| 7 | Preparar pruebas de integración y recursos con frontend local/staging. | Páginas públicas no llaman a Azure por contenido; picos de operaciones privadas no mezclan PDF con login sin límites. |
| 8 | Migración y preparación de Azure. | Datos históricos conciliados, permisos mínimos, backups/restauración, secretos, archivo seguro, métricas y gasto estimado para el despliegue elegido. |

Los pasos se implementan en entregas acotadas. Primero se estabiliza la base transversal; después se cierra un módulo antes de pasar al siguiente. No se renueva la arquitectura de capas ni se completa CRUD de todas las tablas por su sola existencia.

**Reglas del cliente React**

Crear un cliente HTTP común con URL de API configurable y normalización de errores. `VITE_API_BASE_URL` puede ser pública; secretos de Gmail/DB/antibot permanecen en Azure. Las peticiones a contenido local no necesitan token.

Cargar `/me` una vez al iniciar la sección privada y actualizar la copia cliente después de un PATCH. Consultar encuestas al abrir su sección, solicitudes al abrir el historial y detalle al seleccionarlo. No montar consultas privadas en la portada, ejecutar polling continuo o repetir el mismo fetch desde varios componentes. Cancelar solicitudes obsoletas y compartir resultados dentro de la sesión. Refrescar tras una operación que cambie datos, al expirar la sesión o cuando el usuario lo solicite.

Usar paginación y límites existentes. Para errores, distinguir 400, 401, 403, 409, 413, 429 y 503; conservar campos del formulario. Un timeout de una escritura no demuestra que el servidor no la haya guardado. Solo reintentar de manera automática cuando exista idempotencia y aplicar espera, no bucles agresivos.

Los archivos nuevos de Propone usan `FormData` con campo `archivo`, no base64 dentro de JSON. El navegador calcula el boundary. Si falla el PDF, conservar el ID de solicitud y consultar estado/reintentar el adjunto conforme a su cuota.

**PDF y Azure**

La API local actual necesita ClamAV disponible. No se instala automáticamente al desplegar .NET. El usuario propuso seguridad nativa de Azure; la ruta recomendada para producción, si se adopta, es Blob Storage privado más Defender for Storage, con metadata/propietario/estado en PostgreSQL.

Flujo objetivo: autenticar y reservar cuota, aceptar un PDF de hasta 2 MiB en un endpoint acotado, guardarlo en privado como pendiente, recibir/verificar el resultado de análisis y habilitar descarga únicamente si está limpio. Rechazado, sin resultado o error conserva acceso bloqueado. La descarga valida titularidad/admin y usa streaming o acceso temporal restringido; no expone una URL pública permanente.

Para la primera versión, conservar la carga pasando por el endpoint limitado y enviarla al almacenamiento sin materializar bytes en DB. Una subida directa con SAS puede reducir tráfico de API, pero requiere controlar permisos, expiración, objetos abandonados y tamaño real; el tamaño anunciado por React no basta. No añadirla únicamente por rendimiento sin cerrar esas reglas.

Blob/Defender tienen costos y operación propios. Se debe estimar el volumen y el análisis antes de contratar. El análisis al cargar es un flujo por eventos y su resultado debe vincularse a la versión exacta del archivo; la existencia del blob no significa que sea descargable. Esta integración requiere cambios de contrato/SQL y todavía no está implementada. [Defender for Storage](https://learn.microsoft.com/en-us/azure/defender-for-cloud/on-upload-malware-scanning).

Si inicialmente se conserva el almacenamiento `bytea`, solo puede habilitarse en producción con servicio antivirus real operativo, cuotas y capacidad acordadas. Esta alternativa evita migrar archivos de inmediato, pero no permite usar Defender for Storage para bytes que siguen únicamente en PostgreSQL.

**Compatibilidad con el sitio existente**

`/sumate` es un contacto público con DNI/WhatsApp/localidad, no una cuenta ni una afiliación. No convertir históricos ni cambiar este formulario sin definir su caso de uso. Puede conservar su flujo legado durante esta primera integración. Si debe migrar, requiere una entrega separada con identidad/consentimiento/antibot y persistencia propios.

`/propone` actualmente no exige login, admite mensaje de 10.000 caracteres y varios formatos. Para integrar el backend existente, la propuesta base mantiene el requisito original de cuenta registrada y alinea el formulario a 5.000 caracteres y PDF de 2 MiB. Esto implica nuevas pantallas y retorno al formulario después de iniciar sesión; no basta cambiar la URL del fetch. Si se necesita conservar recepción pública, debe definirse antes de esa entrega.

Conservar los motivos: `proyecto`, `sugerencia`, `duda`, `voluntariado`, `denuncia`. Importar históricos sin truncar mensajes ni descartar adjuntos de otros formatos. Preservar el archivo privado legado para los datos que no correspondan al contrato nuevo, con conciliación por ID de origen. No disparar bienvenidas/aprobaciones durante la importación.

El ZIP tiene cuatro noticias, cinco propuestas, 31 perfiles y seis sedes. Corregir el ID 18 duplicado de concejales, claves inconsistentes y referencias de fotos antes de nuevas publicaciones. Revisar Hero (fuente desktop/callback), cobertura de imágenes optimizadas y HTML insertado en NoticiasDetalle. Si se mantiene HTML editorial, sanitizar mediante una biblioteca adecuada; no usar expresiones regulares como protección.

Optimizar transferencia también en Vercel: imágenes por tamaño, detalle cargado al abrirlo, video comprimido con fallback, y separación entre resúmenes y cuerpos completos. No descargar todos los detalles por importarlos en la portada. El video móvil actual ocupa unos 4,46 MB; 10.000 descargas completas serían unos 44,6 GB decimales. Es estimación de bytes, no factura ni medición del sitio.

**Operación y control de gasto**

El objetivo es una API pequeña con límites conocidos. Elegir alojamiento y DB según mediciones de operaciones privadas, no por número total de visitantes. Un plan aprovisionado puede cobrar compute aun con pocas consultas; reducir tráfico ayuda a mantener un tamaño pequeño, pero no elimina ese costo fijo. Comparar opciones y región cuando se prepare el despliegue, sin prometer una tarifa antes de cotizar.

No asumir que una instancia de PostgreSQL Burstable soporta picos sostenidos: utiliza créditos y puede degradarse cuando se agotan. Su documentación actual desaconseja ese tier para producción; la elección debe considerar también disponibilidad y soporte. [Opciones de cómputo PostgreSQL](https://learn.microsoft.com/en-us/azure/postgresql/compute-storage/concepts-compute).

Monitoreo mínimo: latencia p95, errores, 429 legítimos, CPU/memoria, conexiones/esperas DB, ocupación de archivos, cola de correo y gasto. Acotar retención de logs y evitar registrar cuerpos, tokens o datos personales. No lanzar consultas estadísticas pesadas durante las escrituras interactivas ni calcular totales por cada tarjeta del sitio.

Usar rol PostgreSQL dedicado de permisos mínimos, secretos externos al repositorio, red restringida para DB, proxies de confianza, HTTPS, backup y restauración comprobada. Configurar capacidad máxima/autoscaling solo después de medir; las cuotas de negocio deben seguir funcionando entre réplicas.

Los presupuestos de Azure envían alertas: no detienen automáticamente recursos ni consumo. Por eso deben acompañarse de límites de solicitudes, uploads, escalado, almacenamiento y retención. [Presupuestos de Azure](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets).

**Criterio de API terminada para esta etapa**

- Cuentas, encuestas, solicitudes, afiliaciones y correos integrados y comprobados de extremo a extremo en un entorno separado.
- Administración mínima autenticada; cuentas comunes sin acceso a operaciones administrativas.
- Reintentos seguros, límites efectivos y respuestas claras para indisponibilidad/cuota.
- Navegación editorial pública disponible desde Vercel aunque Azure esté temporalmente inaccesible.
- Catálogos y contratos documentados, migración conciliable y almacenamiento/análisis de PDF preparado para el despliegue elegido.
- Medición de picos privados y estimación de gasto basada en recursos concretos.

No se exige implementar Representante/Propuesta como CRUD para marcar terminada esta etapa si se publican desde React. Tampoco se declara completada asistencia a eventos ni se asume que existen estadísticas avanzadas o un CMS. Los pasos 1–6 tienen implementación de backend; el siguiente es la integración y prueba con React (paso 7). El contrato, consumo y prueba manual de Encuesta están en `encuesta-api.md`, con ejemplo completo en `ejemplos/encuesta-v1.json`. Migraciones pendientes en cada base e integración de producción deben completarse antes de declarar terminada la aplicación.
