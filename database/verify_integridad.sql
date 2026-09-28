-- Regresión de 001. Ejecutar SOLO en una copia de prueba. No conserva los registros.
BEGIN;
DO $$
DECLARE
    usuario uuid;
    normalizado text;
    trigger_count integer;
BEGIN
    INSERT INTO public.usuarios (nombre, apellido, email, password_hash)
    VALUES ('Prueba', 'Migracion', 'Net10.Check@Example.invalid', 'not-a-real-hash')
    RETURNING id, email_normalizado INTO usuario, normalizado;
    IF normalizado <> 'net10.check@example.invalid' THEN
        RAISE EXCEPTION 'Normalización incorrecta';
    END IF;
    BEGIN
        INSERT INTO public.usuarios (nombre, apellido, email, password_hash)
        VALUES ('Duplicado', 'Migracion', 'net10.check@example.invalid', 'not-a-real-hash');
        RAISE EXCEPTION 'Se aceptó un email duplicado';
    EXCEPTION WHEN unique_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.sedes (nombre, direccion, latitud) VALUES ('Prueba', 'Prueba', 91);
        RAISE EXCEPTION 'Se aceptó una latitud inválida';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.sedes (nombre, direccion, longitud) VALUES ('Prueba', 'Prueba', 181);
        RAISE EXCEPTION 'Se aceptó una longitud inválida';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.encuestas (titulo, tipo, configuracion, fecha_inicio, fecha_fin)
        VALUES ('Prueba', 'prueba', '{}', DATE '2026-10-02', DATE '2026-10-01');
        RAISE EXCEPTION 'Se aceptaron fechas invertidas en encuesta';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.propuestas (titulo, descripcion, fecha_inicio, fecha_fin)
        VALUES ('Prueba', 'Prueba', DATE '2026-10-02', DATE '2026-10-01');
        RAISE EXCEPTION 'Se aceptaron fechas invertidas en propuesta';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.noticias (titulo, contenido, cantidad_visualizaciones)
        VALUES ('Prueba', 'Prueba', -1);
        RAISE EXCEPTION 'Se aceptaron visualizaciones negativas';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    -- Comprobar que siguen vigentes las restricciones cuya copia fue eliminada.
    BEGIN
        INSERT INTO public.eventos (titulo, fecha, cantidad_maxima) VALUES ('Prueba', CURRENT_DATE, 0);
        RAISE EXCEPTION 'Se aceptó capacidad cero';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.propuestas (titulo, descripcion, estado) VALUES ('Prueba', 'Prueba', 'invalido');
        RAISE EXCEPTION 'Se aceptó estado de propuesta inválido';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        INSERT INTO public.solicitudes_afiliacion (usuario_id, estado) VALUES (usuario, 'invalido');
        RAISE EXCEPTION 'Se aceptó estado de afiliación inválido';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    BEGIN
        UPDATE public.usuarios SET genero = 'invalido' WHERE id = usuario;
        RAISE EXCEPTION 'Se aceptó género inválido';
    EXCEPTION WHEN check_violation THEN NULL;
    END;
    -- Fechas abiertas, coordenadas extremas y valores opcionales siguen admitidos.
    INSERT INTO public.sedes (nombre, direccion, latitud, longitud, updated_at)
    VALUES ('Net10 check', 'Prueba', -90, 180, TIMESTAMPTZ '2000-01-01 00:00:00Z');
    UPDATE public.sedes SET direccion = 'Actualizada' WHERE nombre = 'Net10 check';
    IF EXISTS (SELECT 1 FROM public.sedes WHERE nombre = 'Net10 check' AND updated_at <> CURRENT_TIMESTAMP) THEN
        RAISE EXCEPTION 'No se actualizó updated_at en sedes';
    END IF;
    INSERT INTO public.solicitudes_afiliacion (usuario_id, updated_at)
    VALUES (usuario, TIMESTAMPTZ '2000-01-01 00:00:00Z');
    UPDATE public.solicitudes_afiliacion SET observacion = 'Prueba' WHERE usuario_id = usuario;
    IF EXISTS (SELECT 1 FROM public.solicitudes_afiliacion WHERE usuario_id = usuario AND updated_at <> CURRENT_TIMESTAMP) THEN
        RAISE EXCEPTION 'No se actualizó updated_at en afiliaciones';
    END IF;
    INSERT INTO public.solicitudes_ciudadanas (usuario_id, motivo, mensaje, updated_at)
    VALUES (usuario, 'Prueba', 'Prueba', TIMESTAMPTZ '2000-01-01 00:00:00Z');
    UPDATE public.solicitudes_ciudadanas SET mensaje = 'Actualizado' WHERE usuario_id = usuario;
    IF EXISTS (SELECT 1 FROM public.solicitudes_ciudadanas WHERE usuario_id = usuario AND updated_at <> CURRENT_TIMESTAMP) THEN
        RAISE EXCEPTION 'No se actualizó updated_at en solicitudes ciudadanas';
    END IF;
    INSERT INTO public.propuestas (titulo, descripcion) VALUES ('Prueba', 'Prueba');
    INSERT INTO public.eventos (titulo, fecha) VALUES ('Prueba', CURRENT_DATE);
    SELECT count(*) INTO trigger_count FROM pg_trigger
    WHERE tgname IN ('trg_sedes_updated_at', 'trg_solicitudes_afiliacion_updated_at', 'trg_solicitudes_ciudadanas_updated_at')
        AND NOT tgisinternal;
    IF trigger_count <> 3 THEN RAISE EXCEPTION 'Faltan triggers'; END IF;
    RAISE NOTICE 'OK: unicidad, límites, fechas, restricciones conservadas y triggers.';
END;
$$;
ROLLBACK;
