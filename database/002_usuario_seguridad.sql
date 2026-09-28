-- Aplicar UNA vez después de 001. No ejecutar con la API atendiendo escrituras.
-- psql -v ON_ERROR_STOP=1 -f database/002_usuario_seguridad.sql
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
LOCK TABLE public.usuarios, public.encuesta_respuestas, public.solicitudes_afiliacion IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM public.encuesta_respuestas WHERE usuario_id IS NOT NULL
               GROUP BY encuesta_id, usuario_id HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'Hay respuestas duplicadas por encuesta/usuario. Resolver antes de aplicar 002.';
    END IF;
    IF EXISTS (SELECT 1 FROM public.solicitudes_afiliacion GROUP BY usuario_id HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'Hay varias solicitudes de afiliacion por usuario. Resolver antes de aplicar 002.';
    END IF;
END;
$$;
ALTER TABLE public.usuarios
    ADD COLUMN version_acceso uuid NOT NULL DEFAULT public.uuid_generate_v4(),
    ADD COLUMN intentos_fallidos integer NOT NULL DEFAULT 0,
    ADD COLUMN bloqueado_hasta timestamp with time zone,
    ADD CONSTRAINT chk_usuarios_intentos CHECK (intentos_fallidos BETWEEN 0 AND 5);
CREATE UNIQUE INDEX ux_encuesta_respuestas_usuario
    ON public.encuesta_respuestas (encuesta_id, usuario_id) WHERE usuario_id IS NOT NULL;
ALTER TABLE public.solicitudes_afiliacion
    ADD CONSTRAINT solicitudes_afiliacion_usuario_key UNIQUE (usuario_id);
-- Revoca incluso cuando un administrador modifica el estado directamente en PostgreSQL.
CREATE FUNCTION public.trigger_usuario_version_acceso() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.password_hash IS DISTINCT FROM OLD.password_hash
       OR NEW.rol_id IS DISTINCT FROM OLD.rol_id
       OR NEW.activo IS DISTINCT FROM OLD.activo
       OR NEW.deleted_at IS DISTINCT FROM OLD.deleted_at THEN
        NEW.version_acceso := public.uuid_generate_v4();
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_usuario_version_acceso BEFORE UPDATE ON public.usuarios
    FOR EACH ROW EXECUTE FUNCTION public.trigger_usuario_version_acceso();
-- Rol de menor privilegio requerido por el registro; no asigna privilegios a cuentas.
INSERT INTO public.roles (nombre) VALUES ('Usuario') ON CONFLICT (nombre) DO NOTHING;
COMMIT;

