-- Aplicar UNA vez después de 001–004, con la API detenida.
-- Conserva solicitudes y estados existentes; no inventa autor/fecha de aprobaciones históricas.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
ALTER TABLE public.solicitudes_afiliacion
 ADD COLUMN aprobada_at timestamptz,
 ADD COLUMN aprobada_por uuid,
 ADD CONSTRAINT afiliaciones_aprobada_por_fkey FOREIGN KEY (aprobada_por) REFERENCES public.usuarios(id),
 ADD CONSTRAINT chk_afiliacion_auditoria CHECK ((aprobada_at IS NULL) = (aprobada_por IS NULL));
CREATE INDEX idx_afiliaciones_estado_pagina ON public.solicitudes_afiliacion(estado,id);
CREATE FUNCTION public.trg_afiliacion_aprobacion() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW.usuario_id,NEW.fecha_solicitud,NEW.created_at) IS DISTINCT FROM
    ROW(OLD.usuario_id,OLD.fecha_solicitud,OLD.created_at) THEN
  RAISE EXCEPTION 'No se cambia la identidad de una solicitud' USING ERRCODE='P0409';
 END IF;
 IF OLD.estado IN ('aprobada','rechazada') THEN
  RAISE EXCEPTION 'La solicitud ya fue resuelta' USING ERRCODE='P0409';
 END IF;
 IF NEW.estado='aprobada' THEN
  IF NEW.aprobada_por IS NULL THEN
   RAISE EXCEPTION 'Falta el responsable de aprobacion' USING ERRCODE='23514';
  END IF;
  NEW.aprobada_at:=now();
 ELSIF NEW.aprobada_at IS NOT NULL OR NEW.aprobada_por IS NOT NULL THEN
  RAISE EXCEPTION 'Auditoria incompatible con el estado' USING ERRCODE='23514';
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER trg_afiliacion_aprobacion BEFORE UPDATE ON public.solicitudes_afiliacion
 FOR EACH ROW EXECUTE FUNCTION public.trg_afiliacion_aprobacion();
COMMIT;
