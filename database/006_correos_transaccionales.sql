-- Aplicar UNA vez después de 001–005, con la API detenida. No envía correos históricos.
BEGIN;
SET LOCAL lock_timeout='5s';
CREATE TABLE public.correos_transaccionales (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 tipo text NOT NULL CHECK(tipo IN ('bienvenida','afiliacion_aprobada')),
 referencia_id uuid NOT NULL,
 usuario_id uuid NOT NULL REFERENCES public.usuarios(id) ON DELETE CASCADE,
 estado text NOT NULL DEFAULT 'pendiente' CHECK(estado IN ('pendiente','procesando','enviado','fallido','incierto','cancelado')),
 intentos integer NOT NULL DEFAULT 0 CHECK(intentos BETWEEN 0 AND 5),
 created_at timestamptz NOT NULL DEFAULT now(), disponible_at timestamptz NOT NULL DEFAULT now(),
 reservado_at timestamptz, enviado_at timestamptz, proveedor_id text, error_codigo text,
 UNIQUE(tipo,referencia_id)
);
CREATE INDEX idx_correos_pendientes ON public.correos_transaccionales(disponible_at,id) WHERE estado='pendiente';
CREATE INDEX idx_correos_reservados ON public.correos_transaccionales(reservado_at) WHERE estado='procesando';
CREATE TABLE public.correo_presupuesto (
 id boolean PRIMARY KEY DEFAULT true CHECK(id), dia date NOT NULL,
 intentos integer NOT NULL DEFAULT 0, siguiente_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO public.correo_presupuesto(id,dia) VALUES(true,(now() AT TIME ZONE 'UTC')::date);
CREATE FUNCTION public.encolar_correo(p_tipo text,p_referencia uuid) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_usuario uuid; v_id uuid;
BEGIN
 IF p_tipo='bienvenida' THEN
  SELECT id INTO v_usuario FROM public.usuarios WHERE id=p_referencia AND activo AND deleted_at IS NULL;
 ELSIF p_tipo='afiliacion_aprobada' THEN
  SELECT s.usuario_id INTO v_usuario FROM public.solicitudes_afiliacion s JOIN public.usuarios u ON u.id=s.usuario_id
   WHERE s.id=p_referencia AND s.estado='aprobada' AND u.activo AND u.deleted_at IS NULL;
 END IF;
 IF v_usuario IS NULL THEN RETURN NULL; END IF;
 INSERT INTO public.correos_transaccionales(tipo,referencia_id,usuario_id) VALUES(p_tipo,p_referencia,v_usuario)
 ON CONFLICT(tipo,referencia_id) DO NOTHING RETURNING id INTO v_id;
 IF v_id IS NULL THEN SELECT id INTO v_id FROM public.correos_transaccionales WHERE tipo=p_tipo AND referencia_id=p_referencia; END IF;
 RETURN v_id;
END; $$;
CREATE FUNCTION public.trg_correo_transaccional() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_TABLE_NAME='usuarios' THEN
  PERFORM public.encolar_correo('bienvenida',NEW.id);
 ELSIF NEW.estado='aprobada' AND OLD.estado IS DISTINCT FROM NEW.estado THEN
  PERFORM public.encolar_correo('afiliacion_aprobada',NEW.id);
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER trg_correo_bienvenida AFTER INSERT ON public.usuarios FOR EACH ROW EXECUTE FUNCTION public.trg_correo_transaccional();
CREATE TRIGGER trg_correo_afiliacion AFTER UPDATE OF estado ON public.solicitudes_afiliacion FOR EACH ROW EXECUTE FUNCTION public.trg_correo_transaccional();

-- Cuota y ritmo compartidos entre réplicas. Una reserva cada dos segundos.
CREATE FUNCTION public.reservar_correo(p_limite integer)
RETURNS TABLE(id uuid,tipo text,email text,intentos integer) LANGUAGE plpgsql AS $$
DECLARE v_id uuid; v_dia date := (now() AT TIME ZONE 'UTC')::date; v_pres public.correo_presupuesto;
BEGIN
 IF p_limite NOT BETWEEN 1 AND 500 THEN RAISE EXCEPTION 'Limite invalido'; END IF;
 SELECT * INTO v_pres FROM public.correo_presupuesto WHERE correo_presupuesto.id=true FOR UPDATE;
 -- Gmail puede haber aceptado un envío antes de una caída. No reenviar a ciegas.
 UPDATE public.correos_transaccionales c SET estado='incierto',error_codigo='reserva_vencida'
  WHERE c.estado='procesando' AND c.reservado_at<now()-interval '5 minutes';
 IF v_pres.siguiente_at>now() OR (v_pres.dia=v_dia AND v_pres.intentos>=p_limite) THEN RETURN; END IF;
 SELECT c.id INTO v_id FROM public.correos_transaccionales c WHERE c.estado='pendiente' AND c.disponible_at<=now()
  ORDER BY c.disponible_at,c.id LIMIT 1 FOR UPDATE SKIP LOCKED;
 IF v_id IS NULL THEN RETURN; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id
    WHERE c.id=v_id AND u.activo AND u.deleted_at IS NULL) THEN
  UPDATE public.correos_transaccionales c SET estado='cancelado',error_codigo='cuenta_inactiva' WHERE c.id=v_id;
  RETURN;
 END IF;
 IF EXISTS(SELECT 1 FROM public.correos_transaccionales c WHERE c.id=v_id AND c.created_at<now()-interval '14 days') THEN
  UPDATE public.correos_transaccionales c SET estado='cancelado',error_codigo='aviso_vencido' WHERE c.id=v_id;
  RETURN;
 END IF;
 UPDATE public.correo_presupuesto SET dia=v_dia,intentos=CASE WHEN dia=v_dia THEN correo_presupuesto.intentos+1 ELSE 1 END,
  siguiente_at=now()+interval '2 seconds' WHERE correo_presupuesto.id=true;
 UPDATE public.correos_transaccionales c SET estado='procesando',intentos=c.intentos+1,reservado_at=now() WHERE c.id=v_id;
 RETURN QUERY SELECT c.id,c.tipo,u.email,c.intentos FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id WHERE c.id=v_id;
END; $$;
COMMIT;
