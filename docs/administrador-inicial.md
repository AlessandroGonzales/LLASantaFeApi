# Configurar la primera cuenta administradora

El rol operativo de esta aplicación es **Admin**, respetando exactamente ese nombre.
`Presentation/Program.cs` define `AdminPolicy` con `RequireRole("Admin", "SuperAdmin")`.
Para el operador actual basta crear/asignar Admin. SuperAdmin pasa hoy la misma
política; no existe una jerarquía adicional que haya que configurar para estas pruebas.

## Qué permite Admin

| Módulo | Operaciones administrativas actuales |
| --- | --- |
| Encuestas | Crear/editar borradores, publicar/cerrar y consultar resultados paginados. |
| Afiliaciones | Consultar pendientes y contactos, aprobar/rechazar y consultar totales. |
| Solicitudes ciudadanas | Consultar administración, leer expedientes/adjuntos y gestionar estado/respuesta. |
| Noticias | Crear, consultar detalle administrativo y actualizar atributos. |
| Catálogos | Operaciones administrativas existentes de ciudades, departamentos y sedes. |
| Correo transaccional | Endpoint administrativo de encolado de los eventos permitidos; no envío libre a destinatarios arbitrarios. |

Estas capacidades las imponen las políticas/controladores del backend. La columna
`roles.descripcion` las documenta, no es un listado de permisos ejecutable. No es
necesario añadir tablas de permisos ni cambiar controladores/DTO para usar este rol.

## Preparación de la cuenta

1. Usar una cuenta dedicada a administrar. Para el caso de prueba CU-U01, conservar
   A/B como usuarios comunes y usar esta cuenta como ADM.
2. Si todavía no existe, registrarla por `POST /api/Usuario` con sus datos reales de
   prueba y confirmar su email mediante el flujo normal. El registro crea Usuario.
3. Iniciar sesión como esa cuenta y consultar `GET /api/Usuario/me`.
4. Copiar **idUsuario**. También puede obtenerse de **public.usuarios.id** en pgAdmin.
   `usuarios.rol_id` y `roles.id` identifican un rol, no la cuenta que se elevará.

## Crear el rol y asignarlo desde pgAdmin

Abrir [crear_admin.sql](../database/operaciones/crear_admin.sql) en Query Tool,
conectado a la base correcta y con un operador de DB autorizado. Sustituir solo:

```sql
usuario_objetivo uuid := '00000000-0000-0000-0000-000000000000';
```

por el idUsuario de la cuenta dedicada. Ejecutar el archivo completo.

La operación usa una transacción: comprueba cuenta existente, activa y con email
verificado; crea la fila Admin si falta y actualiza únicamente el rol de esa cuenta.
El rol Usuario y las demás cuentas se conservan. No se cambian contraseña, email,
datos personales ni se concede acceso por un email predeterminado.

Sin sustituir el UUID, o con una cuenta inexistente/inactiva/no verificada, falla
sin asignar permisos. Ante error, ejecutar `ROLLBACK;`, corregir la causa y volver
a ejecutar el archivo completo. Repetirlo para una cuenta que ya tiene Admin no
duplica el rol ni rota otra vez su versión de acceso. Es una operación manual, no
la migración 010 ni una tarea automática de la API.

## Obtener el JWT administrativo y probarlo

1. El cambio de rol invalida los JWT anteriores mediante el trigger de
   `version_acceso`. Quitar el token viejo de Authorize en Swagger.
2. Ejecutar `POST /api/Usuario/login` con el mismo email/contraseña. **200**:
   guardar el nuevo `token` y su vencimiento.
3. Autorizar Swagger con ese token. `GET /api/Usuario/me`: **200** y
   `roleNombre: "Admin"`.
4. `GET /api/SolicitudAfiliacion/pendientes?limite=20`: **200**, aunque la lista esté
   vacía. `GET /api/Encuesta/administracion?limite=20`: **200**.
5. Cambiar al JWT de A/B: ambos GET administrativos deben dar **403**. Volver al
   JWT de ADM solo al realizar las intervenciones administrativas del caso de uso.

El JWT incluye `role: "Admin"`, pero la API también comprueba en PostgreSQL que
cuenta, rol y versión sigan vigentes en cada petición autenticada. Una cadena
editada a mano en el token no concede permisos. El registro público/PATCH de perfil
no aceptan rol y no existe un endpoint público para elevar cuentas.

## Verificación de la operación

Se ejecutaron 21 comprobaciones SQL en PostgreSQL 16 temporal con 001–009:
rechazo del marcador UUID cero, ID inexistente, cuenta sin verificar, inactiva o
dada de baja; creación/asignación correcta; conservación del resto de usuarios;
rotación de versión de acceso; repetición sin duplicar ni revocar otra vez; y
rechazo de un UUID de rol utilizado como si fuera el de una cuenta. El cluster se
detuvo al finalizar. Esta prueba no asignó privilegios en la base configurada.
