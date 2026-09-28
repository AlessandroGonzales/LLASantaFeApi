# Migración a .NET 10 y revisión del esquema

Fecha: 2026-09-26. Fuente: `esquema_sin_datos.sql`, exportado con pg_dump 16.10.
Esta entrega comprende plataforma, dependencias y mejoras estructurales de DB.
Los módulos y controles de seguridad pendientes no se consideran terminados.

## Plataforma fijada

| Componente | Versión |
| --- | --- |
| SDK | 10.0.401 |
| Target de los cuatro proyectos | net10.0 |
| Runtime / ASP.NET Core / EF Core / dotnet-ef | 10.0.12 |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 |
| Swashbuckle.AspNetCore | 10.2.3 |
| Microsoft.IdentityModel.JsonWebTokens | 8.23.0 |
| BCrypt.Net-Next | 4.2.1 |
| Google.Apis.Auth | 1.76.0 |
| Google.Apis.Gmail.v1 | 1.75.0.4225 |

La compatibilidad depende de los frameworks soportados y las restricciones de
dependencias, no de que todos los paquetes se llamen 10.x. Las versiones se
declaran explícitamente en cada `.csproj`; los cuatro `packages.lock.json`
fijan también dependencias transitivas. No se usan versiones preview ni flotantes.
La restauración trata incompatibilidades por fallback, downgrades y avisos de
vulnerabilidades NU1901–NU1904 como errores. Esto no reemplaza pruebas funcionales.

Se retiró la referencia Application -> Infrastructure y las dependencias de
Polly/Http no utilizadas, junto con sus políticas de ejemplo sin consumidores.
El DbContext se registra una sola vez en Infrastructure y configura PostgreSQL 16.
Swagger conserva la UI y usa la API nueva de OpenAPI con autenticación HTTP bearer.

## Uso del SDK

En este equipo se instaló el SDK en `.tools/dotnet`, ignorado por Git.
El SDK global anterior no se modificó. Desde la raíz del repositorio:

```powershell
./scripts/dotnet.ps1 --version
./scripts/dotnet.ps1 restore LLASantaFeApi.sln --locked-mode
./scripts/dotnet.ps1 build LLASantaFeApi.sln -c Release --no-restore
./scripts/dotnet.ps1 run --project Presentation
```

En otro equipo o en CI, instalar el SDK 10.0.401 (o un parche posterior de la misma
banda 10.0.4xx admitido por `global.json`). El wrapper usa el SDK global si no existe
el local. El IDE también necesita soporte para .NET 10; el wrapper no actualiza el IDE.

Para actualizar un paquete, editar su versión en el `.csproj` correspondiente, ejecutar restore sin
`--locked-mode`, revisar los lockfiles y repetir build/pruebas/auditoría.

## Secretos

Se eliminó `OnConfiguring` con credenciales y se vaciaron conexión y clave JWT
del appsettings versionado. La configuración anterior se conservó únicamente
en `Presentation/appsettings.Local.json`, ignorado por Git, cargado en Development
y copiado al directorio de compilación para la depuración local, pero excluido de
publish. No hay contraseñas nuevas en el repositorio.

En otro entorno configurar:

- `ConnectionStrings__DefaultConnection`.
- `JwtSettings__Key`: secreto aleatorio con al menos 32 bytes.
- Emisor, audiencia, duración y FrontendUrl según el entorno.

Las variables de entorno y argumentos tienen prioridad sobre el archivo local.
La aplicación falla al iniciar si falta conexión o la clave JWT no cumple el mínimo.
Retirar una credencial del archivo no la elimina del historial de Git: las
credenciales previamente versionadas deben rotarse antes de desplegar.

## Resultado de revisar el backup

Se confirmaron 14 tablas, UUID como PK, FKs, índices parciales y GIN, validación
de email/teléfono/género, estados de propuestas/afiliación y capacidad de eventos.
Ocho tablas ya tenían triggers de `updated_at`; no faltaban en todo el esquema.
Faltaban en sedes, solicitudes_afiliacion y solicitudes_ciudadanas.

Se encontraron cuatro pares de CHECK equivalentes y un índice no único en email
redundante con `usuarios_email_key`. Se preservan las restricciones `chk_*` y la
unicidad original de email. No se eliminan índices GIN sin mediciones de consultas.

## Aplicación de la mejora SQL

**No se aplicó a tu base existente.** Se probó exclusivamente en bases temporales.
El archivo `database/001_integridad_base.sql` debe ejecutarse UNA vez, completo,
antes de arrancar esta versión de la API contra tu base.

1. Hacer un backup con datos y comprobar que pueda restaurarse.
2. Detener las escrituras de la aplicación y usar una ventana de mantenimiento.
3. Ejecutar el script sobre la base elegida. Ejemplo desde la raíz, ajustando host,
   usuario y nombre; `-W` solicita la contraseña sin escribirla en el comando:

```powershell
& 'C:/Program Files/PostgreSQL/16/bin/psql.exe' `
  -h localhost -p 5432 -U postgres -d LLASantaFe -W `
  -v ON_ERROR_STOP=1 -f ./database/001_integridad_base.sql
```

En pgAdmin también puede ejecutarse el archivo entero. Si falla, ejecutar
`ROLLBACK;` antes de cualquier nuevo intento. El script usa una transacción,
lock_timeout de 5 segundos y statement_timeout de 120 segundos. No es idempotente:
un segundo intento sobre una base ya migrada debe fallar, no volver a modificarla.
En bases grandes hay que revisar bloqueos y tiempos en staging; no aumentar
timeouts a ciegas. No usar `EnsureCreated` ni `database update` para aplicar este SQL.

Cambios:

- Columna generada `usuarios.email_normalizado = lower(btrim(email))` e índice único.
  Se conserva el email original. El repositorio de login consulta esta columna.
  Si existen cuentas que colisionan, se cancela toda la transacción sin fusionarlas.
- Eliminación de cuatro CHECK duplicados y `idx_usuarios_email`.
- Fechas inicio/fin coherentes en encuestas y propuestas.
- Latitud entre -90 y 90, longitud entre -180 y 180.
- Contador de visualizaciones no negativo.
- Índices `sedes(ciudad_id)` y `usuarios(rol_id)`.
- Los tres triggers de timestamp faltantes.

Para diagnosticar colisiones antes de ejecutar, usar localmente (contiene emails):

```sql
SELECT lower(btrim(email)) AS email_normalizado, count(*) AS cuentas
FROM public.usuarios
GROUP BY lower(btrim(email))
HAVING count(*) > 1;
```

Resolver cada conflicto revisando las cuentas reales. El script no decide qué
cuenta conservar. Si falla cualquier CHECK nuevo, revisar el registro conflictivo
antes de volver a ejecutar. No se alteraron las reglas de anonimato, cantidad de
respuestas por encuesta ni estados de afiliación: requieren definir casos de uso.

## Modelo y scaffolding

El modelo actual ya refleja 001: propiedad generada, índices, restricciones y
consulta normalizada. No es obligatorio regenerarlo para usar esta entrega.
Toda la configuración está en `Infrastructure/Persistence/LLASantaFeDbContext.cs`. El scaffolding no reproduce todos los CHECK ni los triggers;
el SQL versionado sigue siendo la fuente de esos objetos.

Si cambia nuevamente PostgreSQL, revisar y guardar cambios antes de regenerar:

```powershell
./scripts/scaffold.ps1 -Overwrite
```

La herramienta EF está fijada a 10.0.12. Se utiliza conexión por nombre y
`--no-onconfiguring`, evitando insertar credenciales en el contexto.
Revisar el diff después de regenerar y actualizar la configuración del contexto si cambian
las restricciones. No se agregaron migraciones EF sobre una base preexistente.

## Verificación realizada

- Restauración del backup en PostgreSQL 16.10 temporal, escuchando en loopback.
- Aplicación completa de 001 y pruebas de `database/verify_integridad.sql`:
  correos duplicados, fechas invertidas, coordenadas, visualizaciones, CHECK
  conservados, valores opcionales y funcionamiento de los tres triggers.
- Caso negativo en otra base: dos correos que solo difieren por mayúsculas impiden
  la migración; se conservan las dos cuentas y el esquema original.
- Scaffolding real con EF 10 contra el esquema modificado, generado en `.artifacts`
  para contrastarlo sin sobrescribir modificaciones previas del proyecto.
- Build Release y publish local; secretos locales ausentes de la publicación.
- Restore `--locked-mode` y auditoría de paquetes directos/transitivos sin
  vulnerabilidades conocidas reportadas por NuGet en la fecha de verificación.
- HTTP: documento Swagger, 401 en perfil anónimo, registro, login usando otro
  casing y espacios del correo, emisión/validación JWT y perfil autenticado.

`scripts/verify-http.ps1` reproduce la prueba HTTP contra una base `net10_*` con
el esquema actualizado y un rol `Usuario`; deja una cuenta sintética en esa base.
`database/verify_integridad.sql` revierte sus registros al terminar.

La compilación actual de los cuatro proyectos termina sin errores ni advertencias.
No se probó envío real de Gmail, login Google, frontend ni despliegue en Azure.

## Siguiente etapa

Antes de exponer la API: cerrar escrituras anónimas de sedes/geografía, agregar
validación y manejo de errores, corregir mapeos de usuario y respuestas de sedes,
definir suspensión/reactivación y revocación, y limitar solicitudes.
Después completar los módulos pendientes según permisos y acciones necesarias.
El modelo editorial, candidaturas, destinatarios de notificaciones y políticas
de privacidad requieren decisiones funcionales y una migración posterior.

Referencias consultadas:

- https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json
- https://www.npgsql.org/efcore/release-notes/10.0.html
- https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md
- https://api.nuget.org/v3/index.json


