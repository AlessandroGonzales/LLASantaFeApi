-- Aplicar UNA vez después de 006, con la API detenida. No confirma emails históricos.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
ALTER TABLE public.usuarios
 ADD COLUMN email_verificado_at timestamptz,
 ADD COLUMN requiere_verificar_email boolean NOT NULL DEFAULT false;
-- Las cuentas anteriores conservan acceso durante la transición; nuevas requieren prueba de email.
ALTER TABLE public.usuarios ALTER COLUMN requiere_verificar_email SET DEFAULT true;

CREATE TABLE public.usuario_acciones (
 id uuid PRIMARY KEY, usuario_id uuid NOT NULL REFERENCES public.usuarios(id) ON DELETE CASCADE,
 tipo text NOT NULL CHECK(tipo IN ('email_verificacion','password_recuperacion')),
 codigo_hash text NOT NULL CHECK(codigo_hash ~ '^[a-f0-9]{64}$'),
 version_acceso uuid NOT NULL, vence_at timestamptz NOT NULL, usado_at timestamptz,
 emitido_at timestamptz NOT NULL DEFAULT now(), dia date NOT NULL,
 emisiones integer NOT NULL CHECK(emisiones BETWEEN 1 AND 3),
 UNIQUE(usuario_id,tipo)
);
ALTER TABLE public.correos_transaccionales DROP CONSTRAINT correos_transaccionales_tipo_check;
ALTER TABLE public.correos_transaccionales ADD CONSTRAINT correos_transaccionales_tipo_check
 CHECK(tipo IN ('bienvenida','afiliacion_aprobada','email_verificacion','password_recuperacion','password_modificada'));

CREATE FUNCTION public.solicitar_accion_usuario(p_email text,p_tipo text,p_id uuid,p_hash text,p_minutes integer)
RETURNS boolean LANGUAGE plpgsql AS $$
DECLARE u public.usuarios; a public.usuario_acciones; v_dia date:=(now() AT TIME ZONE 'UTC')::date; v_emisiones integer;
BEGIN
 IF p_tipo NOT IN ('email_verificacion','password_recuperacion') OR p_minutes NOT BETWEEN 5 AND 120
    OR p_hash !~ '^[a-f0-9]{64}$' THEN RAISE EXCEPTION 'Accion invalida'; END IF;
 SELECT * INTO u FROM public.usuarios WHERE email_normalizado=lower(btrim(p_email)) AND activo AND deleted_at IS NULL FOR UPDATE;
 IF NOT FOUND OR (p_tipo='email_verificacion' AND u.email_verificado_at IS NOT NULL) THEN RETURN false; END IF;
 SELECT * INTO a FROM public.usuario_acciones WHERE usuario_id=u.id AND tipo=p_tipo FOR UPDATE;
 -- No invalidar un enlace vigente ante solicitudes repetidas, ni bombardear el buzón.
 IF FOUND AND a.usado_at IS NULL AND a.vence_at>now() AND a.version_acceso=u.version_acceso THEN RETURN false; END IF;
 IF a.emitido_at>now()-interval '1 minute' OR (a.dia=v_dia AND a.emisiones>=3) THEN RETURN false; END IF;
 v_emisiones:=CASE WHEN a.dia=v_dia THEN a.emisiones+1 ELSE 1 END;
 UPDATE public.correos_transaccionales SET estado='cancelado',error_codigo='codigo_sustituido'
  WHERE referencia_id=a.id AND tipo=p_tipo AND estado='pendiente';
 INSERT INTO public.usuario_acciones(id,usuario_id,tipo,codigo_hash,version_acceso,vence_at,dia,emisiones)
 VALUES(p_id,u.id,p_tipo,p_hash,u.version_acceso,now()+make_interval(mins=>p_minutes),v_dia,v_emisiones)
 ON CONFLICT(usuario_id,tipo) DO UPDATE SET id=EXCLUDED.id,codigo_hash=EXCLUDED.codigo_hash,
  version_acceso=EXCLUDED.version_acceso,vence_at=EXCLUDED.vence_at,usado_at=NULL,
  emitido_at=now(),dia=EXCLUDED.dia,emisiones=EXCLUDED.emisiones;
 INSERT INTO public.correos_transaccionales(tipo,referencia_id,usuario_id) VALUES(p_tipo,p_id,u.id);
 RETURN true;
END; $$;

CREATE FUNCTION public.consumir_accion_usuario(p_id uuid,p_tipo text,p_hash text,p_usuario uuid,p_password_hash text)
RETURNS boolean LANGUAGE plpgsql AS $$
DECLARE a public.usuario_acciones; u public.usuarios; v_usuario uuid;
BEGIN
 -- Bloquear siempre usuario antes de acción, igual que emisión, para evitar interbloqueos.
 SELECT usuario_id INTO v_usuario FROM public.usuario_acciones WHERE id=p_id AND tipo=p_tipo;
 IF NOT FOUND THEN RETURN false; END IF;
 SELECT * INTO u FROM public.usuarios WHERE id=v_usuario FOR UPDATE;
 SELECT * INTO a FROM public.usuario_acciones WHERE id=p_id AND tipo=p_tipo FOR UPDATE;
 IF NOT FOUND OR NOT u.activo OR u.deleted_at IS NOT NULL OR a.codigo_hash<>p_hash OR a.usado_at IS NOT NULL
    OR a.vence_at<=now() OR a.version_acceso<>u.version_acceso THEN RETURN false; END IF;
 IF p_tipo='email_verificacion' THEN
  IF p_usuario IS DISTINCT FROM u.id OR u.email_verificado_at IS NOT NULL THEN RETURN false; END IF;
  UPDATE public.usuarios SET email_verificado_at=now() WHERE id=u.id;
 ELSIF p_tipo='password_recuperacion' THEN
  IF p_password_hash IS NULL OR length(p_password_hash)<>60 OR p_password_hash !~ '^\$2[aby]\$' THEN RETURN false; END IF;
  -- Quien tiene el código del buzón puede recuperar también una cuenta aún no confirmada.
  UPDATE public.usuarios SET password_hash=p_password_hash,email_verificado_at=coalesce(email_verificado_at,now()),
   intentos_fallidos=0,bloqueado_hasta=NULL WHERE id=u.id;
  INSERT INTO public.correos_transaccionales(tipo,referencia_id,usuario_id) VALUES('password_modificada',a.id,u.id);
 ELSE RETURN false;
 END IF;
 UPDATE public.usuario_acciones SET usado_at=now() WHERE id=a.id;
 UPDATE public.correos_transaccionales SET estado='cancelado',error_codigo='codigo_consumido'
  WHERE referencia_id=a.id AND tipo=p_tipo AND estado='pendiente';
 RETURN true;
END; $$;

CREATE OR REPLACE FUNCTION public.trigger_usuario_version_acceso() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.email IS DISTINCT FROM OLD.email THEN
  NEW.email_verificado_at:=NULL; NEW.requiere_verificar_email:=true;
 END IF;
 IF ROW(NEW.password_hash,NEW.rol_id,NEW.activo,NEW.deleted_at,NEW.email,NEW.email_verificado_at,NEW.requiere_verificar_email)
   IS DISTINCT FROM ROW(OLD.password_hash,OLD.rol_id,OLD.activo,OLD.deleted_at,OLD.email,OLD.email_verificado_at,OLD.requiere_verificar_email) THEN
  NEW.version_acceso:=public.uuid_generate_v4();
 END IF;
 RETURN NEW;
END; $$;

CREATE OR REPLACE FUNCTION public.trg_correo_transaccional() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_TABLE_NAME='usuarios' THEN
  IF (TG_OP='INSERT' AND NOT NEW.requiere_verificar_email) OR
     (TG_OP='UPDATE' AND NEW.email_verificado_at IS NOT NULL AND OLD.email_verificado_at IS NULL) THEN
   PERFORM public.encolar_correo('bienvenida',NEW.id);
  END IF;
 ELSIF NEW.estado='aprobada' AND OLD.estado IS DISTINCT FROM NEW.estado THEN
  PERFORM public.encolar_correo('afiliacion_aprobada',NEW.id);
 END IF;
 RETURN NEW;
END; $$;
CREATE TRIGGER trg_correo_email_confirmado AFTER UPDATE OF email_verificado_at ON public.usuarios
 FOR EACH ROW EXECUTE FUNCTION public.trg_correo_transaccional();

CREATE OR REPLACE FUNCTION public.encolar_correo(p_tipo text,p_referencia uuid) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE v_usuario uuid; v_id uuid;
BEGIN
 IF p_tipo='bienvenida' THEN
  SELECT id INTO v_usuario FROM public.usuarios WHERE id=p_referencia AND activo AND deleted_at IS NULL
   AND (NOT requiere_verificar_email OR email_verificado_at IS NOT NULL);
 ELSIF p_tipo='afiliacion_aprobada' THEN
  SELECT s.usuario_id INTO v_usuario FROM public.solicitudes_afiliacion s JOIN public.usuarios u ON u.id=s.usuario_id
   WHERE s.id=p_referencia AND s.estado='aprobada' AND u.activo AND u.deleted_at IS NULL
    AND (NOT u.requiere_verificar_email OR u.email_verificado_at IS NOT NULL);
 END IF;
 IF v_usuario IS NULL THEN RETURN NULL; END IF;
 INSERT INTO public.correos_transaccionales(tipo,referencia_id,usuario_id) VALUES(p_tipo,p_referencia,v_usuario)
 ON CONFLICT(tipo,referencia_id) DO NOTHING RETURNING id INTO v_id;
 IF v_id IS NULL THEN SELECT id INTO v_id FROM public.correos_transaccionales WHERE tipo=p_tipo AND referencia_id=p_referencia; END IF;
 RETURN v_id;
END; $$;

-- Se amplía el resultado de reserva con metadata mínima; no se almacena el código en claro.
DROP FUNCTION public.reservar_correo(integer);
CREATE FUNCTION public.reservar_correo(p_limite integer)
RETURNS TABLE(id uuid,tipo text,email text,intentos integer,accion_id uuid,codigo_hash text,vence_at timestamptz)
LANGUAGE plpgsql AS $$
DECLARE v_id uuid; v_dia date:=(now() AT TIME ZONE 'UTC')::date; v_pres public.correo_presupuesto;
BEGIN
 IF p_limite NOT BETWEEN 1 AND 500 THEN RAISE EXCEPTION 'Limite invalido'; END IF;
 SELECT * INTO v_pres FROM public.correo_presupuesto WHERE correo_presupuesto.id=true FOR UPDATE;
 UPDATE public.correos_transaccionales c SET estado='incierto',error_codigo='reserva_vencida'
  WHERE c.estado='procesando' AND c.reservado_at<now()-interval '5 minutes';
 IF v_pres.siguiente_at>now() OR (v_pres.dia=v_dia AND v_pres.intentos>=p_limite) THEN RETURN; END IF;
 SELECT c.id INTO v_id FROM public.correos_transaccionales c WHERE c.estado='pendiente' AND c.disponible_at<=now()
  ORDER BY c.disponible_at,c.id LIMIT 1 FOR UPDATE SKIP LOCKED;
 IF v_id IS NULL THEN RETURN; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id
    WHERE c.id=v_id AND u.activo AND u.deleted_at IS NULL) OR
    EXISTS(SELECT 1 FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id
     WHERE c.id=v_id AND c.tipo NOT IN ('email_verificacion','password_recuperacion')
      AND u.requiere_verificar_email AND u.email_verificado_at IS NULL) OR
    EXISTS(SELECT 1 FROM public.correos_transaccionales c WHERE c.id=v_id AND c.created_at<now()-interval '14 days') OR
    EXISTS(SELECT 1 FROM public.correos_transaccionales c JOIN public.usuarios u ON u.id=c.usuario_id
     LEFT JOIN public.usuario_acciones a ON a.id=c.referencia_id AND a.tipo=c.tipo
     WHERE c.id=v_id AND c.tipo IN ('email_verificacion','password_recuperacion') AND
      (a.id IS NULL OR a.usado_at IS NOT NULL OR a.vence_at<=now() OR a.version_acceso<>u.version_acceso)) THEN
  UPDATE public.correos_transaccionales c SET estado='cancelado',error_codigo='evento_no_vigente' WHERE c.id=v_id;
  RETURN;
 END IF;
 UPDATE public.correo_presupuesto SET dia=v_dia,intentos=CASE WHEN dia=v_dia THEN correo_presupuesto.intentos+1 ELSE 1 END,
  siguiente_at=now()+interval '2 seconds' WHERE correo_presupuesto.id=true;
 UPDATE public.correos_transaccionales c SET estado='procesando',intentos=c.intentos+1,reservado_at=now() WHERE c.id=v_id;
 RETURN QUERY SELECT c.id,c.tipo,u.email,c.intentos,a.id,a.codigo_hash,a.vence_at FROM public.correos_transaccionales c
  JOIN public.usuarios u ON u.id=c.usuario_id LEFT JOIN public.usuario_acciones a ON a.id=c.referencia_id AND a.tipo=c.tipo WHERE c.id=v_id;
END; $$;
COMMIT;
