-- Aplicar UNA vez después de 003, con la API detenida. No borra solicitudes previas.
BEGIN;
SET LOCAL lock_timeout='5s';
SET LOCAL statement_timeout='120s';
ALTER TABLE public.solicitudes_ciudadanas
 ADD COLUMN estado text NOT NULL DEFAULT 'pendiente',
 ADD COLUMN respuesta text,
 ADD COLUMN revision bigint NOT NULL DEFAULT 1,
 ADD COLUMN gestionado_por uuid REFERENCES public.usuarios(id);
UPDATE public.solicitudes_ciudadanas SET estado='resuelta' WHERE aceptacion;
ALTER TABLE public.solicitudes_ciudadanas ADD CONSTRAINT chk_solicitud_estado
 CHECK (estado IN ('pendiente','en_revision','resuelta','rechazada'));
CREATE INDEX idx_solicitudes_usuario_fecha ON public.solicitudes_ciudadanas(usuario_id,created_at);
CREATE INDEX idx_solicitudes_fecha ON public.solicitudes_ciudadanas(created_at);
CREATE INDEX idx_solicitudes_usuario_pagina ON public.solicitudes_ciudadanas(usuario_id,id);
CREATE INDEX idx_solicitudes_estado_pagina ON public.solicitudes_ciudadanas(estado,id);
CREATE FUNCTION public.trg_solicitud_cuota() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(404001);
 IF (SELECT count(*) FROM public.solicitudes_ciudadanas WHERE usuario_id=NEW.usuario_id AND created_at>now()-interval '24 hours')>=3
 OR (SELECT count(*) FROM public.solicitudes_ciudadanas WHERE created_at>now()-interval '24 hours')>=1000 THEN
  RAISE EXCEPTION 'Cuota de solicitudes agotada' USING ERRCODE='P0429';
 END IF;
 NEW.created_at:=now();
 RETURN NEW;
END; $$;
CREATE TRIGGER trg_solicitud_cuota BEFORE INSERT ON public.solicitudes_ciudadanas
 FOR EACH ROW EXECUTE FUNCTION public.trg_solicitud_cuota();
CREATE FUNCTION public.trg_solicitud_revision() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW.usuario_id,NEW.motivo,NEW.mensaje,NEW.created_at,NEW.pdf_url) IS DISTINCT FROM
 ROW(OLD.usuario_id,OLD.motivo,OLD.mensaje,OLD.created_at,OLD.pdf_url)
 OR OLD.estado IN ('resuelta','rechazada') OR NEW.estado='pendiente' THEN
  RAISE EXCEPTION 'Transicion no permitida' USING ERRCODE='P0409';
 END IF;
 NEW.revision:=OLD.revision+1;
 NEW.aceptacion:=(NEW.estado='resuelta');
 RETURN NEW;
END; $$;
CREATE TRIGGER trg_solicitud_revision BEFORE UPDATE ON public.solicitudes_ciudadanas
 FOR EACH ROW EXECUTE FUNCTION public.trg_solicitud_revision();

-- Tabla separada: los listados nunca materializan bytes del PDF.
CREATE TABLE public.solicitud_pdf (
 solicitud_id uuid PRIMARY KEY REFERENCES public.solicitudes_ciudadanas(id) ON DELETE CASCADE,
 usuario_id uuid NOT NULL REFERENCES public.usuarios(id),
 contenido bytea NOT NULL CHECK(octet_length(contenido) BETWEEN 20 AND 2097152),
 sha256 text NOT NULL CHECK(length(sha256)=64),
 bytes integer NOT NULL CHECK(bytes BETWEEN 20 AND 2097152),
 created_at timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT solicitud_pdf_hash_usuario UNIQUE(usuario_id,sha256),
 CONSTRAINT solicitud_pdf_bytes CHECK(bytes=octet_length(contenido))
);
-- Reservas consumidas antes de leer multipart. Incluso archivos inválidos consumen intento.
CREATE TABLE public.solicitud_pdf_intentos (
 id uuid PRIMARY KEY DEFAULT public.uuid_generate_v4(),
 usuario_id uuid NOT NULL REFERENCES public.usuarios(id),
 solicitud_id uuid NOT NULL REFERENCES public.solicitudes_ciudadanas(id) ON DELETE CASCADE,
 created_at timestamptz NOT NULL DEFAULT now(),
 usado boolean NOT NULL DEFAULT false
);
CREATE INDEX idx_pdf_intentos_usuario_fecha ON public.solicitud_pdf_intentos(usuario_id,created_at);
CREATE INDEX idx_pdf_intentos_fecha ON public.solicitud_pdf_intentos(created_at);
CREATE FUNCTION public.reservar_solicitud_pdf(p_id uuid,p_usuario uuid) RETURNS uuid LANGUAGE plpgsql AS $$
DECLARE intento uuid;
BEGIN
 PERFORM pg_advisory_xact_lock(404002);
 IF NOT EXISTS(SELECT 1 FROM public.solicitudes_ciudadanas WHERE id=p_id AND usuario_id=p_usuario) THEN
  RAISE EXCEPTION 'No disponible' USING ERRCODE='P0404';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM public.solicitudes_ciudadanas WHERE id=p_id AND estado='pendiente' AND created_at>now()-interval '24 hours')
 OR EXISTS(SELECT 1 FROM public.solicitud_pdf WHERE solicitud_id=p_id) THEN
  RAISE EXCEPTION 'No admite adjunto' USING ERRCODE='P0409';
 END IF;
 DELETE FROM public.solicitud_pdf_intentos WHERE created_at<now()-interval '24 hours';
 IF (SELECT count(*) FROM public.solicitud_pdf_intentos WHERE usuario_id=p_usuario)>=3
 OR (SELECT count(*) FROM public.solicitud_pdf_intentos)>=100 THEN
  RAISE EXCEPTION 'Cuota de adjuntos agotada' USING ERRCODE='P0429';
 END IF;
 INSERT INTO public.solicitud_pdf_intentos(usuario_id,solicitud_id) VALUES(p_usuario,p_id) RETURNING id INTO intento;
 RETURN intento;
END; $$;
CREATE FUNCTION public.guardar_solicitud_pdf(p_id uuid,p_usuario uuid,p_intento uuid,p_contenido bytea,p_hash text)
 RETURNS integer LANGUAGE plpgsql AS $$
BEGIN
 PERFORM pg_advisory_xact_lock(404003);
 PERFORM 1 FROM public.solicitudes_ciudadanas WHERE id=p_id AND usuario_id=p_usuario
  AND estado='pendiente' AND created_at>now()-interval '24 hours' FOR SHARE;
 IF NOT FOUND THEN RAISE EXCEPTION 'No admite adjunto' USING ERRCODE='P0409'; END IF;
 UPDATE public.solicitud_pdf_intentos SET usado=true WHERE id=p_intento AND usuario_id=p_usuario
  AND solicitud_id=p_id AND NOT usado AND created_at>now()-interval '5 minutes';
 IF NOT FOUND THEN RAISE EXCEPTION 'Reserva no disponible' USING ERRCODE='P0409'; END IF;
 IF (SELECT coalesce(sum(bytes),0) FROM public.solicitud_pdf)+octet_length(p_contenido)>104857600 THEN
  RAISE EXCEPTION 'Almacenamiento completo' USING ERRCODE='P0503';
 END IF;
 INSERT INTO public.solicitud_pdf(solicitud_id,usuario_id,contenido,sha256,bytes)
 VALUES(p_id,p_usuario,p_contenido,p_hash,octet_length(p_contenido));
 RETURN 1;
END; $$;
COMMIT;
