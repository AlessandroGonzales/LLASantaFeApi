# Configuración de consumo de la API

Implementación del paso 1, 14 de octubre de 2026. Configuración pública en `Presentation/appsettings.json`; las credenciales siguen en configuración privada. No requiere cambios SQL: usa los scripts 001–006 y la cola existente.

Actualización del paso 3, 20 de octubre: se añade el GET de departamentos a la misma caché acotada, con `CatalogCache:DepartmentsSeconds=300`. Consultar [catalogo-geografico-api.md](catalogo-geografico-api.md) para el contrato y resultados actuales. Los valores y resultados del paso 1 siguientes documentan esa entrega original; Usuario requiere adicionalmente SQL 007 desde el paso 2.

**Límites por instancia**

| Operación | Máximo simultáneo | Comportamiento al agotarse |
| --- | --- | --- |
| Lecturas ordinarias | 8 | HTTP 429, sin cola de espera |
| Escrituras ordinarias | 6 | HTTP 429, sin cola de espera |
| Registro y login | 2 | HTTP 429 antes de consultar DB o ejecutar BCrypt |
| Carga y descarga PDF | 2 | HTTP 429; mantiene además cuotas por cuenta y escaneo |

Son cuatro grupos independientes seleccionados por metadata del endpoint y método HTTP. Una petición PDF no consume un permiso de login o lectura ordinaria. No son límites de usuarios conectados ni una medición de capacidad. La suma es 18; el arranque rechaza una configuración que supere el presupuesto de conexiones de API.

También se mantienen 120 peticiones por minuto/IP, login 10/minuto/IP, registro 5/hora/IP y escritura 20/minuto/IP. Ciudad y Sede ahora aplican la política de escritura a sus modificaciones administrativas. Cada rechazo tiene `Retry-After`; por concurrencia es 2 segundos como indicación de reintento, no reserva de un permiso.

Claves bajo `RateLimits`: `General`, `Login`, `Registro`, `Escritura`, `ReadConcurrency`, `WriteConcurrency`, `AuthenticationConcurrency`, `PdfConcurrency`. Valores inválidos fallan al iniciar en vez de ser ajustados silenciosamente. Los límites son por proceso; las cuotas de negocio de PostgreSQL siguen compartidas. Antes de desplegar varias réplicas debe decidirse un límite agregado. Al desplegar tras un proxy se debe configurar la confianza en ese proxy; no se acepta una IP arbitraria enviada en headers por el cliente.

**PostgreSQL**

| Clave bajo `DatabaseConsumption` | Default |
| --- | --- |
| `ApiMaxPoolSize` | 20 |
| `MailMaxPoolSize` | 2 |
| `ContextPoolSize` | 32 |
| `ConnectionTimeoutSeconds` | 5 |
| `CommandTimeoutSeconds` | 15 |
| `ConnectionIdleSeconds` | 60 |
| `RetryCount` | 0 |

La API y correo tienen pools separados y nombres de aplicación distintos. Máximo de conexiones físicas en esos pools: 22 por instancia, no 22 abiertas permanentemente. Mínimo cero; se liberan conexiones ociosas según el mantenimiento del pool. El pool de contextos EF reutiliza objetos y no es un límite de conexiones. Las propiedades anteriores tienen prioridad sobre los valores equivalentes de la cadena de conexión privada; host, usuario, contraseña y TLS no se publican ni sustituyen.

Se desactivan por defecto los reintentos automáticos EF/Npgsql que antes podían repetir una escritura de resultado incierto. `RetryCount` admite 0–2; no aumentarlo sin analizar idempotencia. La cola Gmail mantiene su propia política de reintentos. Fallos Npgsql directos o envueltos en `DbUpdateException` devuelven indisponibilidad genérica 503.

Estos valores limitan espera y consumo; deben medirse antes de elegir recursos de Azure. Una conexión reservada en el pool no equivale a una solicitud facturable independiente y no hay una estimación monetaria en esta entrega.

**Worker de correo**

La consulta anterior cada 15 segundos era una consulta interna a PostgreSQL. No era una llamada a la API pública ni a Gmail cuando no había trabajo.

Señal local de capacidad uno: registro nuevo, aprobación de afiliación y encolado manual pendiente notifican después de confirmar la operación de DB. Duplicar un email de registro no despierta el worker. Las señales se agrupan y no almacenan cuerpos, direcciones ni una entrada por correo. Los trabajos permanecen en PostgreSQL.

Sin trabajo: esperas de 30, 60, 120, 240 y hasta 300 segundos. Una señal interrumpe la espera y vuelve al mínimo. Hay una separación de 2 segundos entre iteraciones normales para evitar bucles por señales repetidas. En errores: espera progresiva de 30 hasta 300 segundos, que las señales no acortan. Al volver a obtener trabajo se procesa un correo por iteración conservando el ritmo y la cuota SQL existentes.

Una instancia continuamente activa, sin señales ni trabajo y con Gmail habilitado pasa de unas 5.760 comprobaciones diarias a aproximadamente 286–300 después del arranque: alrededor de 95 % menos. Es un cálculo del intervalo, no un benchmark o ahorro de dinero medido. Cuando hay trabajo se consulta más para procesarlo. Con `Gmail:Enabled=false` no se registra el worker y no hace sondeos.

La comprobación periódica conserva recuperación tras reinicios, señales perdidas, reintentos diferidos y trabajos insertados desde otra instancia/SQL. En esos casos la detección puede tardar aproximadamente cinco minutos con los valores por defecto. No se promete entrega inmediata ni exactamente una entrega en Gmail. Los resultados inciertos siguen sin reenviarse automáticamente.

Claves adicionales `Gmail:IdleMinSeconds=30`, `Gmail:IdleMaxSeconds=300`. El scope de servicios se cierra antes de esperar. La espera se cancela al detener el host. Cada réplica tiene su señal local; PostgreSQL conserva reserva/ritmo/cuota compartidos.

**Caché**

Solo GET anónimo de `/api/Departamento`, `/api/Ciudad?departamentoId=<uuid>` y `/api/Sede`. Departamentos y ciudades: 300 segundos en servidor; sedes: 60. Cabecera pública con hasta 30 segundos de frescura en navegador/CDN. Esa ventana cliente puede mostrar una versión anterior durante hasta 30 segundos después de una modificación.

No se permite caché con `Authorization`, `Cookie`, identidad autenticada, query arbitraria, UUID inválido/vacío/duplicado, errores o `Set-Cookie`. Los datos privados y el resto de endpoints conservan `no-store`. El UUID se normaliza; se separan los orígenes CORS en la clave. El bloqueo de generación del middleware agrupa regeneraciones de una misma clave.

Memoria de entradas acotada a 4 MiB, cuerpo máximo por entrada 128 KiB. No es el total de RAM del proceso: el framework tiene estructuras y buffers adicionales. Claves `CatalogCache:SizeMiB`, `MaxBodyBytes`, `DepartmentsSeconds`, `CitiesSeconds`, `SedesSeconds` con rangos validados.

Crear un departamento o una ciudad y crear/modificar/eliminar una sede invalidan la caché correspondiente en esta instancia después de confirmar DB. Una revisión local en la clave impide que una consulta empezada antes de la modificación repueble la clave vigente con contenido anterior. Las entradas viejas siguen limitadas por expiración/tamaño.

Cambios realizados directamente en SQL u otra réplica se observan al expirar el TTL. Esta entrega no usa Redis ni hace sondeos adicionales para invalidación compartida. No afirmar invalidación instantánea entre instancias. El GET de departamentos se incorporó en el paso 3.

Las páginas públicas que usan archivos del frontend en Vercel siguen sin requerir estas llamadas a Azure. OutputCache evita consultar DB en una lectura repetida, pero la petición llega a .NET.

**Verificación reproducible**

Resultado de esta entrega: compilación Release sin errores ni advertencias, 53 comprobaciones de consumo/worker/caché/límites, 22 del transporte Gmail simulado y 428 de integración con PostgreSQL temporal. Total: 503 comprobaciones correctas. Gitleaks Worktree sin hallazgos. El servidor temporal se detuvo al finalizar; no se enviaron correos reales.

- Compilar con `scripts/dotnet.ps1 build LLASantaFeApi.sln -c Release`.
- `scripts/ConsumptionChecks`: servidor HTTP local y repositorios/transportes simulados. Comprueba señal agrupada/cancelación, espera progresiva/errores/vaciado de cola, presupuesto/validación, separación de concurrencia, 429, expiración/invalidación de caché, credenciales/cookies/errores/querys, tamaño y carrera de modificación concurrente.
- `scripts/CorreoChecks`: transporte Gmail simulado y dispatcher; ningún correo real.
- `scripts/verify-usuario.ps1 -SchemaPath <backup-sin-datos>`: suite en PostgreSQL de prueba 127.0.0.1:55432 con datos sintéticos y Gmail deshabilitado. Incluye `verify-consumo-cases.ps1` para los controladores reales y su invalidación después de escrituras.

No ejecutar la suite sobre una base de producción. Esta comprobación funcional no es una prueba de carga de Azure; la capacidad y el gasto de producción siguen pendientes de medición/cotización.
