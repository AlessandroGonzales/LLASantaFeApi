-- Ejecutar UNA vez, después de 001 y 002, con la API detenida.
-- No elimina encuestas ni respuestas existentes.
BEGIN;
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '120s';
LOCK TABLE public.encuestas, public.encuesta_respuestas IN SHARE ROW EXCLUSIVE MODE;
ALTER TABLE public.encuestas
    ADD COLUMN publicada_at timestamptz,
    ADD COLUMN revision bigint NOT NULL DEFAULT 1,
    ALTER COLUMN activa SET DEFAULT false;
-- Conserva encuestas previas activas o con respuestas como publicadas e inmutables.
UPDATE public.encuestas e SET publicada_at=e.created_at
WHERE e.activa OR EXISTS (SELECT 1 FROM public.encuesta_respuestas r WHERE r.encuesta_id=e.id);
ALTER TABLE public.encuestas ADD CONSTRAINT chk_encuestas_publicacion CHECK (NOT activa OR publicada_at IS NOT NULL);
CREATE INDEX idx_encuestas_disponibles ON public.encuestas(id)
    WHERE activa AND publicada_at IS NOT NULL AND deleted_at IS NULL;
CREATE INDEX idx_encuesta_respuestas_pagina ON public.encuesta_respuestas(encuesta_id,id);

CREATE FUNCTION public.trigger_encuesta_edicion() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.publicada_at IS NOT NULL AND (
        ROW(NEW.titulo,NEW.descripcion,NEW.tipo,NEW.configuracion,NEW.fecha_inicio,NEW.fecha_fin,NEW.ciudad_id,NEW.departamento_id,NEW.publicada_at)
        IS DISTINCT FROM
        ROW(OLD.titulo,OLD.descripcion,OLD.tipo,OLD.configuracion,OLD.fecha_inicio,OLD.fecha_fin,OLD.ciudad_id,OLD.departamento_id,OLD.publicada_at)
        OR (NOT OLD.activa AND NEW.activa)) THEN
        RAISE EXCEPTION 'Una encuesta publicada no se edita ni se reabre' USING ERRCODE='23514';
    END IF;
    NEW.revision := OLD.revision + 1;
    RETURN NEW;
END; $$;
CREATE TRIGGER trg_encuesta_edicion BEFORE UPDATE ON public.encuestas
FOR EACH ROW EXECUTE FUNCTION public.trigger_encuesta_edicion();

CREATE FUNCTION public.trigger_encuesta_admite_respuesta() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE e public.encuestas%ROWTYPE;
DECLARE hoy date := (statement_timestamp() AT TIME ZONE 'America/Argentina/Buenos_Aires')::date;
BEGIN
    -- SHARE entra en conflicto con cierre/edición pero permite respuestas simultáneas.
    SELECT * INTO e FROM public.encuestas WHERE id=NEW.encuesta_id FOR SHARE;
    IF NOT FOUND OR e.publicada_at IS NULL OR NOT e.activa OR e.deleted_at IS NOT NULL
        OR e.fecha_inicio > hoy OR e.fecha_fin < hoy THEN
        RAISE EXCEPTION 'La encuesta no admite respuestas' USING ERRCODE='23514';
    END IF;
    IF NEW.usuario_id IS NULL OR jsonb_typeof(NEW.respuestas) <> 'object'
        OR octet_length(NEW.respuestas::text) > 65536 THEN
        RAISE EXCEPTION 'Respuesta inválida' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER trg_encuesta_admite_respuesta BEFORE INSERT ON public.encuesta_respuestas
FOR EACH ROW EXECUTE FUNCTION public.trigger_encuesta_admite_respuesta();
COMMIT;
