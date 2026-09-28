-- PostgreSQL 16+. Aplicar UNA vez sobre el esquema del backup 2026-09-26.
-- No elimina registros ni modifica el email original. Ejecutar con ON_ERROR_STOP.
-- Requiere ventana de mantenimiento: los índices y validaciones toman bloqueos.
-- Ante cualquier error, ROLLBACK: no continuar ejecutando sentencias manualmente.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';

-- Evita modificaciones concurrentes entre la comprobación y el índice único.
LOCK TABLE public.usuarios IN SHARE ROW EXCLUSIVE MODE;
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM public.usuarios
        GROUP BY lower(btrim(email)) HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Hay emails duplicados al normalizar mayúsculas y espacios. Resolverlos antes de aplicar 001; no se ha fusionado ninguna cuenta.';
    END IF;
END;
$$;

-- Conserva el email que ingresó el usuario y genera la clave de búsqueda en DB.
ALTER TABLE public.usuarios
    ADD COLUMN email_normalizado text GENERATED ALWAYS AS (lower(btrim(email))) STORED NOT NULL;
ALTER TABLE public.usuarios
    ADD CONSTRAINT usuarios_email_normalizado_key UNIQUE (email_normalizado);
-- usuarios_email_key ya cubre la misma columna y orden que este índice redundante.
DROP INDEX public.idx_usuarios_email;

-- Quitar únicamente las copias equivalentes; conservar restricciones chk_*.
ALTER TABLE public.eventos DROP CONSTRAINT eventos_cantidad_maxima_check;
ALTER TABLE public.propuestas DROP CONSTRAINT propuestas_estado_check;
ALTER TABLE public.solicitudes_afiliacion DROP CONSTRAINT solicitudes_afiliacion_estado_check;
ALTER TABLE public.usuarios DROP CONSTRAINT usuarios_genero_check;

-- Reglas estructurales, sin imponer aún flujos de publicación o afiliación.
ALTER TABLE public.encuestas ADD CONSTRAINT chk_encuestas_fechas
    CHECK (fecha_inicio IS NULL OR fecha_fin IS NULL OR fecha_fin >= fecha_inicio);
ALTER TABLE public.propuestas ADD CONSTRAINT chk_propuestas_fechas
    CHECK (fecha_inicio IS NULL OR fecha_fin IS NULL OR fecha_fin >= fecha_inicio);
ALTER TABLE public.sedes ADD CONSTRAINT chk_sedes_latitud
    CHECK (latitud IS NULL OR latitud BETWEEN -90 AND 90);
ALTER TABLE public.sedes ADD CONSTRAINT chk_sedes_longitud
    CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180);
ALTER TABLE public.noticias ADD CONSTRAINT chk_noticias_visualizaciones
    CHECK (cantidad_visualizaciones >= 0);

-- FKs utilizadas para búsquedas por localidad y rol.
CREATE INDEX idx_sedes_ciudad ON public.sedes (ciudad_id);
CREATE INDEX idx_usuarios_rol ON public.usuarios (rol_id);

-- Las otras ocho tablas con updated_at ya tienen el trigger en el backup.
CREATE TRIGGER trg_sedes_updated_at BEFORE UPDATE ON public.sedes
    FOR EACH ROW EXECUTE FUNCTION public.trigger_set_timestamp();
CREATE TRIGGER trg_solicitudes_afiliacion_updated_at BEFORE UPDATE ON public.solicitudes_afiliacion
    FOR EACH ROW EXECUTE FUNCTION public.trigger_set_timestamp();
CREATE TRIGGER trg_solicitudes_ciudadanas_updated_at BEFORE UPDATE ON public.solicitudes_ciudadanas
    FOR EACH ROW EXECUTE FUNCTION public.trigger_set_timestamp();

COMMIT;
