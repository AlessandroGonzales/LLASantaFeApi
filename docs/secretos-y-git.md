# Credenciales y revisión antes de publicar

## Qué se versiona

Código, proyectos .NET, lockfiles NuGet, migraciones de estructura, pruebas con datos sintéticos y documentación. `Presentation/appsettings.json` contiene valores públicos y campos sensibles vacíos. El envío de Gmail está desactivado por defecto.

La configuración real permanece en `Presentation/appsettings.Local.json`, ignorada por Git y excluida de `dotnet publish`. Ese archivo sí se copia al directorio local de compilación para poder desarrollar: no distribuir `bin`, `obj` ni archivos de diagnóstico. En un despliegue, configurar secretos fuera del repositorio mediante el proveedor del entorno.

No versionar .env, secretos OAuth, refresh tokens, cadenas de conexión, claves JWT, certificados privados, perfiles de publicación, backups de PostgreSQL, uploads ni datos de usuarios. Mantener los SQL de `database` como migraciones de estructura, no como dumps de datos reales.

## Controles locales en Windows

```powershell
.\scripts\install-gitleaks.ps1
git config --local core.hooksPath .githooks
.\scripts\check-secrets.ps1 -Mode Worktree
git add <archivos-revisados>
.\scripts\check-secrets.ps1 -Mode Staged
git commit -m "Descripción del cambio"
.\scripts\check-secrets.ps1 -Mode History
```

La instalación descarga Gitleaks 8.30.1 para Windows x64 desde su repositorio oficial y verifica un SHA-256 fijado. No envía archivos a servicios externos. [Gitleaks](https://github.com/gitleaks/gitleaks).

El hook `pre-commit` analiza todos los archivos del índice; bloquea nombres privados/generados, configuraciones sensibles no vacías y hallazgos de Gitleaks. El hook `pre-push` vuelve a comprobar el índice y todas las referencias del historial local. Los informes muestran ubicación y regla, con secretos redactados, y quedan en `.artifacts`, fuera de Git.

Los hooks son locales: cada clon necesita instalar la herramienta y configurar `core.hooksPath`. En otros sistemas operativos habrá que adaptar el ejecutor de los hooks. No saltarse una detección usando `--no-verify`; revisar primero. No se incluyen excepciones globales que silencien secretos conocidos.

## Si un secreto estuvo en un commit anterior

Eliminarlo del archivo actual o agregarlo a `.gitignore` no lo retira del historial. Primero revocar o rotar la credencial en el proveedor y actualizar la configuración local. Para JWT, cambiar la clave y reiniciar las instancias invalida los tokens anteriores. Para PostgreSQL, cambiar la contraseña del rol en el servidor y actualizar todos los clientes que la usan.

Después, coordinar la limpieza del historial con los colaboradores y el responsable del repositorio. Esto puede requerir reescribir commits, actualizar referencias remotas y limpiar otras copias. No hacer force-push como una operación rutinaria. El escáner de historial seguirá bloqueando mientras las referencias locales contengan secretos: no ignorar esos hallazgos para desbloquearlo.

Una revisión sin hallazgos no garantiza ausencia absoluta de secretos o datos personales; combina detección automática con revisión del contenido. La revisión local tampoco prueba el estado actual de GitHub, forks, clones o cachés.
