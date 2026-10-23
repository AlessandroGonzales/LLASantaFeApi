-- Operación manual de administración; no es una migración ni se ejecuta al arrancar.
-- Requiere 001–009 y permisos de operador sobre la base.
-- Sustituir únicamente usuario_objetivo por usuarios.id / GET /api/Usuario/me.idUsuario.
-- No es el UUID de roles.id ni el valor de usuarios.rol_id.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '15s';

DO $admin$
DECLARE
    usuario_objetivo uuid := '00000000-0000-0000-0000-000000000000';
    cuenta public.usuarios%ROWTYPE;
    rol_admin_id uuid;
BEGIN
    IF usuario_objetivo = '00000000-0000-0000-0000-000000000000'::uuid THEN
        RAISE EXCEPTION 'Reemplazá usuario_objetivo por el UUID de la cuenta que administrará la aplicación.';
    END IF;

    SELECT * INTO cuenta FROM public.usuarios WHERE id = usuario_objetivo FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'La cuenta indicada no existe. Usá usuarios.id, no rol_id.';
    END IF;
    IF NOT cuenta.activo OR cuenta.deleted_at IS NOT NULL THEN
        RAISE EXCEPTION 'La cuenta debe estar activa y no dada de baja.';
    END IF;
    IF cuenta.email_verificado_at IS NULL THEN
        RAISE EXCEPTION 'Verificá el email de la cuenta antes de asignarle Admin.';
    END IF;

    INSERT INTO public.roles (nombre, descripcion)
    VALUES ('Admin', 'Administra encuestas, solicitudes, afiliaciones y las operaciones autorizadas por AdminPolicy.')
    ON CONFLICT (nombre) DO NOTHING;

    SELECT id INTO STRICT rol_admin_id FROM public.roles WHERE nombre = 'Admin';
    IF cuenta.rol_id = rol_admin_id THEN
        RAISE NOTICE 'La cuenta ya tiene el rol Admin; no se modifica su acceso.';
    ELSE
        UPDATE public.usuarios SET rol_id = rol_admin_id WHERE id = usuario_objetivo;
        -- El trigger existente rota version_acceso: los JWT previos dejan de ser válidos.
        RAISE NOTICE 'Rol Admin asignado a la cuenta indicada. Iniciá sesión nuevamente.';
    END IF;
END;
$admin$;

COMMIT;

SELECT id, nombre, descripcion FROM public.roles WHERE nombre IN ('Usuario', 'Admin') ORDER BY nombre;
