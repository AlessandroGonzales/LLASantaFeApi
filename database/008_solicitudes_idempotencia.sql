-- Aplicar UNA vez después de 007, con la API detenida. No borra solicitudes ni PDF.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';

ALTER TABLE public.solicitudes_ciudadanas
 ADD COLUMN clave_idempotencia uuid,
 ADD COLUMN solicitud_hash text,
 ADD CONSTRAINT chk_solicitud_idempotencia CHECK (
  (clave_idempotencia IS NULL AND solicitud_hash IS NULL) OR
  (clave_idempotencia IS NOT NULL AND clave_idempotencia<>'00000000-0000-0000-0000-000000000000'::uuid
   AND solicitud_hash IS NOT NULL AND solicitud_hash ~ '^[0-9a-f]{64}$'));
CREATE UNIQUE INDEX ux_solicitud_idempotencia ON public.solicitudes_ciudadanas(usuario_id,clave_idempotencia)
 WHERE clave_idempotencia IS NOT NULL;
ALTER TABLE public.solicitud_pdf ADD COLUMN clave_idempotencia uuid
 CHECK(clave_idempotencia<>'00000000-0000-0000-0000-000000000000'::uuid);
ALTER TABLE public.solicitud_pdf_intentos ADD COLUMN clave_idempotencia uuid
 CHECK(clave_idempotencia<>'00000000-0000-0000-0000-000000000000'::uuid);

CREATE FUNCTION public.crear_solicitud_ciudadana(p_id uuid,p_usuario uuid,p_motivo text,p_mensaje text,p_clave uuid)
 RETURNS TABLE("Id" uuid,"Nueva" boolean) LANGUAGE plpgsql AS $$
DECLARE existente record; huella text;
BEGIN
 IF p_id IS NULL OR p_usuario IS NULL OR p_clave='00000000-0000-0000-0000-000000000000'::uuid
 OR p_motivo IS NULL OR p_motivo NOT IN ('proyecto','sugerencia','duda','voluntariado','denuncia')
 OR p_mensaje IS NULL OR length(p_mensaje) NOT BETWEEN 10 AND 5000 THEN
  RAISE EXCEPTION 'Datos invalidos' USING ERRCODE='23514';
 END IF;
 PERFORM 1 FROM public.usuarios WHERE id=p_usuario AND activo AND deleted_at IS NULL FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Cuenta no disponible' USING ERRCODE='P0401'; END IF;
 -- Mismo bloqueo que el trigger de cuota; sin llamadas externas en esta transacción.
 PERFORM pg_advisory_xact_lock(404001);
 IF p_clave IS NOT NULL THEN
  huella:=encode(sha256(convert_to('solicitud-ciudadana:v1'||E'\n'||p_motivo||E'\n'||p_mensaje,'UTF8')),'hex');
  SELECT id,solicitud_hash INTO existente FROM public.solicitudes_ciudadanas WHERE usuario_id=p_usuario AND clave_idempotencia=p_clave;
  IF FOUND THEN
   IF existente.solicitud_hash<>huella THEN
    RAISE EXCEPTION 'Clave reutilizada con otros datos' USING ERRCODE='P0412';
   END IF;
   RETURN QUERY SELECT existente.id,false;
   RETURN;
  END IF;
 END IF;
 INSERT INTO public.solicitudes_ciudadanas(id,usuario_id,motivo,mensaje,aceptacion,clave_idempotencia,solicitud_hash)
 VALUES(p_id,p_usuario,p_motivo,p_mensaje,false,p_clave,huella);
 RETURN QUERY SELECT p_id,true;
END; $$;

CREATE OR REPLACE FUNCTION public.trg_solicitud_revision() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW.usuario_id,NEW.motivo,NEW.mensaje,NEW.created_at,NEW.pdf_url,NEW.clave_idempotencia,NEW.solicitud_hash) IS DISTINCT FROM
 ROW(OLD.usuario_id,OLD.motivo,OLD.mensaje,OLD.created_at,OLD.pdf_url,OLD.clave_idempotencia,OLD.solicitud_hash)
 OR OLD.estado IN ('resuelta','rechazada') OR NEW.estado='pendiente' THEN
  RAISE EXCEPTION 'Transicion no permitida' USING ERRCODE='P0409';
 END IF;
 NEW.revision:=OLD.revision+1;
 NEW.aceptacion:=(NEW.estado='resuelta');
 RETURN NEW;
END; $$;

-- Sobrecarga: conserva reserva y cuotas originales para cargas sin clave.
CREATE FUNCTION public.reservar_solicitud_pdf(p_id uuid,p_usuario uuid,p_clave uuid)
 RETURNS TABLE("Intento" uuid,"Sha256" text,"Bytes" integer) LANGUAGE plpgsql AS $$
DECLARE adjunto record; intento uuid;
BEGIN
 IF p_clave='00000000-0000-0000-0000-000000000000'::uuid THEN
  RAISE EXCEPTION 'Clave invalida' USING ERRCODE='23514';
 END IF;
 PERFORM 1 FROM public.usuarios WHERE id=p_usuario AND activo AND deleted_at IS NULL FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Cuenta no disponible' USING ERRCODE='P0401'; END IF;
 PERFORM pg_advisory_xact_lock(404002);
 PERFORM 1 FROM public.solicitudes_ciudadanas WHERE id=p_id AND usuario_id=p_usuario FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'No disponible' USING ERRCODE='P0404'; END IF;
 SELECT clave_idempotencia,sha256,bytes INTO adjunto FROM public.solicitud_pdf WHERE solicitud_id=p_id;
 IF FOUND THEN
  IF p_clave IS NOT NULL AND adjunto.clave_idempotencia=p_clave THEN
   RETURN QUERY SELECT '00000000-0000-0000-0000-000000000000'::uuid,adjunto.sha256,adjunto.bytes;
   RETURN;
  END IF;
  RAISE EXCEPTION 'No admite reemplazo' USING ERRCODE='P0409';
 END IF;
 IF p_clave IS NOT NULL AND EXISTS(SELECT 1 FROM public.solicitud_pdf_intentos
   WHERE solicitud_id=p_id AND usuario_id=p_usuario AND clave_idempotencia=p_clave AND NOT usado
    AND created_at>now()-interval '5 minutes') THEN
  RAISE EXCEPTION 'Carga en curso' USING ERRCODE='P0425';
 END IF;
 intento:=public.reservar_solicitud_pdf(p_id,p_usuario);
 UPDATE public.solicitud_pdf_intentos SET clave_idempotencia=p_clave WHERE id=intento;
 RETURN QUERY SELECT intento,NULL::text,NULL::integer;
END; $$;

CREATE OR REPLACE FUNCTION public.guardar_solicitud_pdf(p_id uuid,p_usuario uuid,p_intento uuid,p_contenido bytea,p_hash text)
 RETURNS integer LANGUAGE plpgsql AS $$
DECLARE clave uuid; adjunto record;
BEGIN
 IF p_contenido IS NULL OR octet_length(p_contenido) NOT BETWEEN 20 AND 2097152
 OR p_hash IS NULL OR p_hash<>encode(sha256(p_contenido),'hex') THEN
  RAISE EXCEPTION 'Contenido invalido' USING ERRCODE='23514';
 END IF;
 PERFORM 1 FROM public.usuarios WHERE id=p_usuario AND activo AND deleted_at IS NULL FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'Cuenta no disponible' USING ERRCODE='P0401'; END IF;
 PERFORM pg_advisory_xact_lock(404003);
 SELECT clave_idempotencia INTO clave FROM public.solicitud_pdf_intentos WHERE id=p_intento AND usuario_id=p_usuario AND solicitud_id=p_id;
 SELECT clave_idempotencia,sha256,bytes INTO adjunto FROM public.solicitud_pdf WHERE solicitud_id=p_id AND usuario_id=p_usuario;
 IF FOUND THEN
  IF clave IS NOT NULL AND adjunto.clave_idempotencia=clave AND adjunto.sha256=p_hash AND adjunto.bytes=octet_length(p_contenido) THEN
   RETURN 0;
  END IF;
  RAISE EXCEPTION 'No admite reemplazo' USING ERRCODE='P0409';
 END IF;
 PERFORM 1 FROM public.solicitudes_ciudadanas WHERE id=p_id AND usuario_id=p_usuario
  AND estado='pendiente' AND created_at>now()-interval '24 hours' FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'No admite adjunto' USING ERRCODE='P0409'; END IF;
 UPDATE public.solicitud_pdf_intentos SET usado=true WHERE id=p_intento AND usuario_id=p_usuario
  AND solicitud_id=p_id AND NOT usado AND created_at>now()-interval '5 minutes';
 IF NOT FOUND THEN RAISE EXCEPTION 'Reserva no disponible' USING ERRCODE='P0409'; END IF;
 IF (SELECT coalesce(sum(bytes),0) FROM public.solicitud_pdf)+octet_length(p_contenido)>104857600 THEN
  RAISE EXCEPTION 'Almacenamiento completo' USING ERRCODE='P0503';
 END IF;
 INSERT INTO public.solicitud_pdf(solicitud_id,usuario_id,contenido,sha256,bytes,clave_idempotencia)
 VALUES(p_id,p_usuario,p_contenido,p_hash,octet_length(p_contenido),clave);
 RETURN 1;
END; $$;
COMMIT;
