-- Aplicar UNA vez después de 008, con la API detenida.
-- Conserva estados y auditorías anteriores; no inventa motivos/autores históricos.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';

ALTER TABLE public.solicitudes_afiliacion
 ADD COLUMN rechazada_at timestamptz,
 ADD COLUMN rechazada_por uuid REFERENCES public.usuarios(id),
 ADD COLUMN motivo_rechazo text,
 ADD CONSTRAINT chk_afiliacion_rechazo CHECK (
  (rechazada_at IS NULL AND rechazada_por IS NULL AND motivo_rechazo IS NULL) OR
  (estado='rechazada' AND rechazada_at IS NOT NULL AND rechazada_por IS NOT NULL
   AND motivo_rechazo IS NOT NULL AND length(btrim(motivo_rechazo)) BETWEEN 10 AND 2000
   AND aprobada_at IS NULL AND aprobada_por IS NULL));

-- Una resolución comprueba su autor vigente y queda confirmada en la misma
-- transacción que el correo de aprobación existente. No hay llamadas externas.
CREATE OR REPLACE FUNCTION public.trg_afiliacion_aprobacion() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE responsable uuid;
BEGIN
 IF ROW(NEW.usuario_id,NEW.fecha_solicitud,NEW.created_at) IS DISTINCT FROM
    ROW(OLD.usuario_id,OLD.fecha_solicitud,OLD.created_at) THEN
  RAISE EXCEPTION 'No se cambia la identidad de una solicitud' USING ERRCODE='P0409';
 END IF;
 IF OLD.estado IN ('aprobada','rechazada') THEN
  RAISE EXCEPTION 'La solicitud ya fue resuelta' USING ERRCODE='P0409';
 END IF;
 IF NEW.estado='aprobada' THEN
  IF NEW.aprobada_por IS NULL OR NEW.rechazada_at IS NOT NULL OR NEW.rechazada_por IS NOT NULL OR NEW.motivo_rechazo IS NOT NULL THEN
   RAISE EXCEPTION 'Auditoria de aprobacion invalida' USING ERRCODE='23514';
  END IF;
  responsable:=NEW.aprobada_por;
 ELSIF NEW.estado='rechazada' THEN
  IF NEW.rechazada_por IS NULL OR NEW.motivo_rechazo IS NULL
   OR length(btrim(NEW.motivo_rechazo)) NOT BETWEEN 10 AND 2000
   OR NEW.aprobada_at IS NOT NULL OR NEW.aprobada_por IS NOT NULL THEN
   RAISE EXCEPTION 'Auditoria de rechazo invalida' USING ERRCODE='23514';
  END IF;
  responsable:=NEW.rechazada_por;
 ELSIF NEW.aprobada_at IS NOT NULL OR NEW.aprobada_por IS NOT NULL
    OR NEW.rechazada_at IS NOT NULL OR NEW.rechazada_por IS NOT NULL OR NEW.motivo_rechazo IS NOT NULL THEN
  RAISE EXCEPTION 'Auditoria incompatible con el estado' USING ERRCODE='23514';
 END IF;
 IF responsable IS NOT NULL THEN
  PERFORM 1 FROM public.usuarios a JOIN public.roles r ON r.id=a.rol_id
   WHERE a.id=responsable AND a.activo AND a.deleted_at IS NULL AND r.nombre IN ('Admin','SuperAdmin')
    AND (NOT a.requiere_verificar_email OR a.email_verificado_at IS NOT NULL) FOR SHARE OF a;
  IF NOT FOUND THEN RAISE EXCEPTION 'Responsable no disponible' USING ERRCODE='P0409'; END IF;
 END IF;
 IF NEW.estado='aprobada' THEN
  PERFORM 1 FROM public.usuarios u WHERE u.id=NEW.usuario_id AND u.activo AND u.deleted_at IS NULL
   AND (NOT u.requiere_verificar_email OR u.email_verificado_at IS NOT NULL) FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Cuenta no disponible' USING ERRCODE='P0409'; END IF;
  NEW.aprobada_at:=now();
 ELSIF NEW.estado='rechazada' THEN
  NEW.motivo_rechazo:=btrim(NEW.motivo_rechazo);
  NEW.rechazada_at:=now();
 END IF;
 RETURN NEW;
END; $$;
COMMIT;
