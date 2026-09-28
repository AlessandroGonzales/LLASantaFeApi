# Arranque local

La solución utiliza .NET 10. Visual Studio 2022 (17.x) no puede compilar ese target;
para trabajar desde el IDE hace falta Visual Studio 2026 (18.x).

## Visual Studio 2026

El instalador oficial de Community está preparado en `.tools/vs2026/vs_community.exe`.
Su firma Microsoft se verificó al descargarlo y se vuelve a verificar al ejecutarlo:

```powershell
./scripts/install-vs2026.ps1
```

El asistente recibe la carga de trabajo ASP.NET y desarrollo web, con componentes
recomendados, y una carpeta diferente de Visual Studio 2022. Completar el asistente
y abrir `LLASantaFeApi.sln` desde Visual Studio 2026. Seleccionar Presentation como
proyecto de inicio y el perfil `http` para Swagger.

Fuente: https://learn.microsoft.com/en-us/visualstudio/install/use-command-line-parameters-to-install-visual-studio?view=visualstudio

## Base de datos

El script `database/001_integridad_base.sql` ya fue aplicado según la comprobación
local. La comprobación del 26/09/2026 encontró pendiente
`database/002_usuario_seguridad.sql`. Ejecutarlo completo una sola vez en la base
configurada antes de probar Usuario. No es necesario hacer scaffolding: las
propiedades y sus mapeos ya están incorporados.

## Swagger con el SDK local

Desde la raíz, incluso antes de instalar el IDE:

```powershell
./scripts/start-api.ps1
```

Abrir http://localhost:5275/swagger. Detener con Ctrl+C.
La conexión y clave JWT de desarrollo se leen de
`Presentation/appsettings.Local.json` (no versionado).
Los ejemplos para registro, login y Authorize están en `docs/usuario-api.md`.

La configuración se carga desde el directorio del ejecutable. Una compilación
Debug usa Development cuando no se indicó un entorno mediante variables o
argumentos; así también funciona al ejecutar sin launchSettings.json. Un entorno
explícito conserva prioridad. Release mantiene el comportamiento estándar
(Production por defecto) y exige secretos externos; no carga el archivo local.

## Verificación

- Restore con `--locked-mode`: correcto.
- Build Debug y Release: cero errores y cero advertencias.
- PostgreSQL 16 temporal: 128 comprobaciones de integración correctas.
- Solo existe `Infrastructure/Persistence/LLASantaFeDbContext.cs`; los archivos
  parciales se consolidaron en ese contexto.
- Los paquetes declaran versiones explícitas en los `.csproj`, con dependencias
  transitivas fijadas por los cuatro `packages.lock.json`.
