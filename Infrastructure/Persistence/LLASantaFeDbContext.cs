using System;
using System.Collections.Generic;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public partial class LLASantaFeDbContext : DbContext
{
    public LLASantaFeDbContext()
    {
    }

    public LLASantaFeDbContext(DbContextOptions<LLASantaFeDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Ciudade> Ciudades { get; set; }

    public virtual DbSet<Departamento> Departamentos { get; set; }

    public virtual DbSet<Encuesta> Encuestas { get; set; }

    public virtual DbSet<EncuestaRespuesta> EncuestaRespuestas { get; set; }

    public virtual DbSet<Evento> Eventos { get; set; }

    public virtual DbSet<Noticia> Noticias { get; set; }

    public virtual DbSet<Notificacione> Notificaciones { get; set; }

    public virtual DbSet<Propuesta> Propuestas { get; set; }

    public virtual DbSet<Representante> Representantes { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Sede> Sedes { get; set; }

    public virtual DbSet<SolicitudesAfiliacion> SolicitudesAfiliacions { get; set; }

    public virtual DbSet<SolicitudesCiudadana> SolicitudesCiudadanas { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Ciudade>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ciudades_pkey");

            entity.ToTable("ciudades");

            entity.HasIndex(e => new { e.DepartamentoId, e.Nombre }, "ciudades_departamento_id_nombre_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Ciudades)
                .HasForeignKey(d => d.DepartamentoId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ciudades_departamento_id_fkey");
        });

        modelBuilder.Entity<Departamento>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("departamentos_pkey");

            entity.ToTable("departamentos");

            entity.HasIndex(e => e.Nombre, "departamentos_nombre_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Encuesta>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("encuestas_pkey");

            entity.ToTable("encuestas");

            entity.HasIndex(e => e.Configuracion, "idx_encuestas_configuracion_gin").HasMethod("gin");

            entity.HasIndex(e => new { e.FechaInicio, e.FechaFin }, "idx_encuestas_fecha");

            entity.HasIndex(e => new { e.Tipo, e.Activa }, "idx_encuestas_tipo_activa").HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Activa)
                .HasDefaultValue(true)
                .HasColumnName("activa");
            entity.Property(e => e.CiudadId).HasColumnName("ciudad_id");
            entity.Property(e => e.Configuracion)
                .HasColumnType("jsonb")
                .HasColumnName("configuracion");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.Tipo).HasColumnName("tipo");
            entity.Property(e => e.Titulo).HasColumnName("titulo");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Ciudad).WithMany(p => p.Encuesta)
                .HasForeignKey(d => d.CiudadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("encuestas_ciudad_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Encuesta)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("encuestas_created_by_fkey");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Encuesta)
                .HasForeignKey(d => d.DepartamentoId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("encuestas_departamento_id_fkey");
        });

        modelBuilder.Entity<EncuestaRespuesta>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("encuesta_respuestas_pkey");

            entity.ToTable("encuesta_respuestas");

            entity.HasIndex(e => e.CreatedAt, "idx_encuesta_respuestas_created").IsDescending();

            entity.HasIndex(e => e.EncuestaId, "idx_encuesta_respuestas_encuesta");

            entity.HasIndex(e => e.Respuestas, "idx_encuesta_respuestas_respuestas_gin").HasMethod("gin");

            entity.HasIndex(e => e.UsuarioId, "idx_encuesta_respuestas_usuario");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EncuestaId).HasColumnName("encuesta_id");
            entity.Property(e => e.IpAddress).HasColumnName("ip_address");
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
            entity.Property(e => e.Respuestas)
                .HasColumnType("jsonb")
                .HasColumnName("respuestas");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");

            entity.HasOne(d => d.Encuesta).WithMany(p => p.EncuestaRespuesta)
                .HasForeignKey(d => d.EncuestaId)
                .HasConstraintName("encuesta_respuestas_encuesta_id_fkey");

            entity.HasOne(d => d.Usuario).WithMany(p => p.EncuestaRespuesta)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("encuesta_respuestas_usuario_id_fkey");
        });

        modelBuilder.Entity<Evento>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("eventos_pkey");

            entity.ToTable("eventos");

            entity.HasIndex(e => e.DepartamentoId, "idx_eventos_departamento");

            entity.HasIndex(e => e.Fecha, "idx_eventos_fecha").IsDescending();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CantidadMaxima).HasColumnName("cantidad_maxima");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Direccion).HasColumnName("direccion");
            entity.Property(e => e.Fecha).HasColumnName("fecha");
            entity.Property(e => e.Hora).HasColumnName("hora");
            entity.Property(e => e.ImagenUrl).HasColumnName("imagen_url");
            entity.Property(e => e.Titulo).HasColumnName("titulo");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Eventos)
                .HasForeignKey(d => d.DepartamentoId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("eventos_departamento_id_fkey");
        });

        modelBuilder.Entity<Noticia>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("noticias_pkey");

            entity.ToTable("noticias", tb => tb.HasComment("Noticias oficiales - optimizado para feeds por fecha y ciudad"));

            entity.HasIndex(e => e.CiudadId, "idx_noticias_ciudad").HasFilter("(publicado = true)");

            entity.HasIndex(e => e.Destacada, "idx_noticias_destacada").HasFilter("(publicado = true)");

            entity.HasIndex(e => new { e.Publicado, e.FechaPublicacion }, "idx_noticias_publicado_fecha")
                .IsDescending(false, true)
                .HasFilter("(deleted_at IS NULL)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CantidadVisualizaciones)
                .HasDefaultValue(0)
                .HasColumnName("cantidad_visualizaciones");
            entity.Property(e => e.Categoria).HasColumnName("categoria");
            entity.Property(e => e.CiudadId).HasColumnName("ciudad_id");
            entity.Property(e => e.Contenido).HasColumnName("contenido");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Destacada)
                .HasDefaultValue(false)
                .HasColumnName("destacada");
            entity.Property(e => e.FechaPublicacion).HasColumnName("fecha_publicacion");
            entity.Property(e => e.ImagenPrincipalUrl).HasColumnName("imagen_principal_url");
            entity.Property(e => e.Publicado)
                .HasDefaultValue(false)
                .HasColumnName("publicado");
            entity.Property(e => e.Resumen).HasColumnName("resumen");
            entity.Property(e => e.Titulo).HasColumnName("titulo");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Ciudad).WithMany(p => p.Noticia)
                .HasForeignKey(d => d.CiudadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("noticias_ciudad_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Noticia)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("noticias_created_by_fkey");
        });

        modelBuilder.Entity<Notificacione>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notificaciones_pkey");

            entity.ToTable("notificaciones");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Mensaje).HasColumnName("mensaje");
            entity.Property(e => e.Tipo).HasColumnName("tipo");
            entity.Property(e => e.Titulo).HasColumnName("titulo");
            entity.Property(e => e.Url).HasColumnName("url");
        });

        modelBuilder.Entity<Propuesta>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("propuestas_pkey");

            entity.ToTable("propuestas");

            entity.HasIndex(e => e.DepartamentoId, "idx_propuestas_departamento");

            entity.HasIndex(e => e.Estado, "idx_propuestas_estado");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.DocumentoPdfUrl).HasColumnName("documento_pdf_url");
            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'borrador'::text")
                .HasColumnName("estado");
            entity.Property(e => e.FechaFin).HasColumnName("fecha_fin");
            entity.Property(e => e.FechaInicio).HasColumnName("fecha_inicio");
            entity.Property(e => e.ImagenUrl).HasColumnName("imagen_url");
            entity.Property(e => e.Tematica).HasColumnName("tematica");
            entity.Property(e => e.Titulo).HasColumnName("titulo");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Propuesta)
                .HasForeignKey(d => d.DepartamentoId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("propuestas_departamento_id_fkey");
        });

        modelBuilder.Entity<Representante>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("representantes_pkey");

            entity.ToTable("representantes");

            entity.HasIndex(e => e.DepartamentoId, "idx_representantes_departamento").HasFilter("(activo = true)");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Apellido).HasColumnName("apellido");
            entity.Property(e => e.Biografia).HasColumnName("biografia");
            entity.Property(e => e.Cargo).HasColumnName("cargo");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DepartamentoId).HasColumnName("departamento_id");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Facebook).HasColumnName("facebook");
            entity.Property(e => e.FotoUrl).HasColumnName("foto_url");
            entity.Property(e => e.Instagram).HasColumnName("instagram");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Profesion).HasColumnName("profesion");
            entity.Property(e => e.Proyectos)
                .HasColumnType("jsonb")
                .HasColumnName("proyectos");
            entity.Property(e => e.Twitter).HasColumnName("twitter");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Departamento).WithMany(p => p.Representantes)
                .HasForeignKey(d => d.DepartamentoId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("representantes_departamento_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Nombre, "roles_nombre_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
        });

        modelBuilder.Entity<Sede>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("sedes_pkey");

            entity.ToTable("sedes");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CiudadId).HasColumnName("ciudad_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Direccion).HasColumnName("direccion");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Horario).HasColumnName("horario");
            entity.Property(e => e.ImagenUrl).HasColumnName("imagen_url");
            entity.Property(e => e.Latitud)
                .HasPrecision(10, 8)
                .HasColumnName("latitud");
            entity.Property(e => e.Longitud)
                .HasPrecision(11, 8)
                .HasColumnName("longitud");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.Telefono)
                .HasMaxLength(20)
                .HasColumnName("telefono");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Ciudad).WithMany(p => p.Sedes)
                .HasForeignKey(d => d.CiudadId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("sedes_ciudad_id_fkey");
        });

        modelBuilder.Entity<SolicitudesAfiliacion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("solicitudes_afiliacion_pkey");

            entity.ToTable("solicitudes_afiliacion");

            entity.HasIndex(e => new { e.UsuarioId, e.Estado }, "idx_solicitudes_afiliacion_usuario");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Estado)
                .HasDefaultValueSql("'pendiente'::text")
                .HasColumnName("estado");
            entity.Property(e => e.FechaSolicitud)
                .HasDefaultValueSql("now()")
                .HasColumnName("fecha_solicitud");
            entity.Property(e => e.Observacion).HasColumnName("observacion");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");

            entity.HasOne(d => d.Usuario).WithMany(p => p.SolicitudesAfiliacions)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("solicitudes_afiliacion_usuario_id_fkey");
        });

        modelBuilder.Entity<SolicitudesCiudadana>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("solicitudes_ciudadanas_pkey");

            entity.ToTable("solicitudes_ciudadanas");

            entity.HasIndex(e => e.UsuarioId, "idx_solicitudes_ciudadanas_usuario");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Aceptacion)
                .HasDefaultValue(false)
                .HasColumnName("aceptacion");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Mensaje).HasColumnName("mensaje");
            entity.Property(e => e.Motivo).HasColumnName("motivo");
            entity.Property(e => e.PdfUrl).HasColumnName("pdf_url");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");

            entity.HasOne(d => d.Usuario).WithMany(p => p.SolicitudesCiudadanas)
                .HasForeignKey(d => d.UsuarioId)
                .HasConstraintName("solicitudes_ciudadanas_usuario_id_fkey");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("usuarios_pkey");

            entity.ToTable("usuarios", tb => tb.HasComment("Usuarios registrados en la aplicación"));

            entity.HasIndex(e => e.Activo, "idx_usuarios_activo").HasFilter("(deleted_at IS NULL)");

            entity.HasIndex(e => e.CiudadId, "idx_usuarios_ciudad").HasFilter("(deleted_at IS NULL)");


            entity.HasIndex(e => e.Email, "usuarios_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Activo)
                .HasDefaultValue(true)
                .HasColumnName("activo");
            entity.Property(e => e.Apellido).HasColumnName("apellido");
            entity.Property(e => e.CiudadId).HasColumnName("ciudad_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.FechaNacimiento).HasColumnName("fecha_nacimiento");
            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("now()")
                .HasColumnName("fecha_registro");
            entity.Property(e => e.FotoPerfilUrl).HasColumnName("foto_perfil_url");
            entity.Property(e => e.Genero).HasColumnName("genero");
            entity.Property(e => e.Nombre).HasColumnName("nombre");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Profesion).HasColumnName("profesion");
            entity.Property(e => e.RolId).HasColumnName("rol_id");
            entity.Property(e => e.Telefono)
                .HasMaxLength(20)
                .HasColumnName("telefono");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Ciudad).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.CiudadId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("usuarios_ciudad_id_fkey");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InverseCreatedByNavigation)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("usuarios_created_by_fkey");

            entity.HasOne(d => d.Rol).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.RolId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("usuarios_rol_id_fkey");
        });

        // Reglas adicionales de los scripts SQL 001 y 002.


        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.Property(e => e.EmailNormalizado)
                .HasColumnName("email_normalizado")
                .HasComputedColumnSql("lower(btrim(email))", stored: true)
                .IsRequired();
            entity.HasIndex(e => e.EmailNormalizado, "usuarios_email_normalizado_key").IsUnique();
            entity.HasIndex(e => e.RolId, "idx_usuarios_rol");
            entity.ToTable("usuarios", table =>
            {
                table.HasCheckConstraint("chk_usuarios_email", "email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$'");
                table.HasCheckConstraint("chk_usuarios_genero", "genero IS NULL OR genero IN ('masculino', 'femenino', 'otro', 'prefiero_no_decir')");
                table.HasCheckConstraint("chk_usuarios_telefono", "telefono IS NULL OR telefono ~ '^\\+?[0-9\\s\\-\\(\\)]{8,20}$'");
            });
        });
        modelBuilder.Entity<Encuesta>().ToTable("encuestas", table =>
            table.HasCheckConstraint("chk_encuestas_fechas", "fecha_inicio IS NULL OR fecha_fin IS NULL OR fecha_fin >= fecha_inicio"));
        modelBuilder.Entity<Propuesta>().ToTable("propuestas", table =>
        {
            table.HasCheckConstraint("chk_propuestas_fechas", "fecha_inicio IS NULL OR fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
            table.HasCheckConstraint("chk_propuestas_estado", "estado IN ('borrador', 'activa', 'en_progreso', 'completada', 'rechazada')");
        });
        modelBuilder.Entity<Evento>().ToTable("eventos", table =>
            table.HasCheckConstraint("chk_eventos_cantidad_maxima", "cantidad_maxima IS NULL OR cantidad_maxima > 0"));
        modelBuilder.Entity<SolicitudesAfiliacion>().ToTable("solicitudes_afiliacion", table =>
            table.HasCheckConstraint("chk_solicitudes_afiliacion_estado", "estado IN ('pendiente', 'aprobada', 'rechazada', 'en_revision')"));
        modelBuilder.Entity<Sede>(entity =>
        {
            entity.HasIndex(e => e.CiudadId, "idx_sedes_ciudad");
            entity.ToTable("sedes", table =>
            {
                table.HasCheckConstraint("chk_sedes_latitud", "latitud IS NULL OR latitud BETWEEN -90 AND 90");
                table.HasCheckConstraint("chk_sedes_longitud", "longitud IS NULL OR longitud BETWEEN -180 AND 180");
            });
        });
        modelBuilder.Entity<Noticia>().ToTable("noticias", table =>
            table.HasCheckConstraint("chk_noticias_visualizaciones", "cantidad_visualizaciones >= 0"));

        modelBuilder.Entity<Usuario>(e =>
        {
            e.Property(u => u.VersionAcceso).HasColumnName("version_acceso").HasDefaultValueSql("uuid_generate_v4()");
            e.Property(u => u.IntentosFallidos).HasColumnName("intentos_fallidos").HasDefaultValue(0);
            e.Property(u => u.BloqueadoHasta).HasColumnName("bloqueado_hasta");
            e.ToTable("usuarios", t => t.HasCheckConstraint("chk_usuarios_intentos", "intentos_fallidos BETWEEN 0 AND 5"));
        });
        modelBuilder.Entity<EncuestaRespuesta>().HasIndex(e => new { e.EncuestaId, e.UsuarioId }, "ux_encuesta_respuestas_usuario")
            .IsUnique().HasFilter("(usuario_id IS NOT NULL)");
        modelBuilder.Entity<SolicitudesAfiliacion>().HasIndex(e => e.UsuarioId, "solicitudes_afiliacion_usuario_key").IsUnique();

        modelBuilder.Entity<Encuesta>(entity =>
        {
            entity.Property(e => e.PublicadaAt).HasColumnName("publicada_at");
            entity.Property(e => e.Revision).HasColumnName("revision").HasDefaultValue(1L).ValueGeneratedOnAddOrUpdate();
            entity.Property(e => e.Activa).HasDefaultValue(false);
            entity.HasIndex(e => new { e.Id }, "idx_encuestas_disponibles").HasFilter("activa AND publicada_at IS NOT NULL AND deleted_at IS NULL");
        });
        modelBuilder.Entity<EncuestaRespuesta>().HasIndex(e => new { e.EncuestaId, e.Id }, "idx_encuesta_respuestas_pagina");
        modelBuilder.Entity<SolicitudesCiudadana>(entity =>
        {
            entity.Property(e => e.Estado).HasColumnName("estado").HasDefaultValue("pendiente");
            entity.Property(e => e.Respuesta).HasColumnName("respuesta");
            entity.Property(e => e.Revision).HasColumnName("revision").HasDefaultValue(1L).ValueGeneratedOnAddOrUpdate();
            entity.Property(e => e.GestionadoPor).HasColumnName("gestionado_por");
        });
        modelBuilder.Entity<SolicitudesAfiliacion>(entity =>
        {
            entity.Property(e => e.AprobadaAt).HasColumnName("aprobada_at");
            entity.Property(e => e.AprobadaPor).HasColumnName("aprobada_por");
            entity.HasIndex(e => new { e.Estado, e.Id }, "idx_afiliaciones_estado_pagina");
            entity.HasOne<Usuario>().WithMany().HasForeignKey(e => e.AprobadaPor)
                .OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("afiliaciones_aprobada_por_fkey");
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
