// Contenido para: Data/ApplicationDbContext.cs

using FormBuilder.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Template> Templates { get; set; }
        public DbSet<FilledForm> FilledForms { get; set; }
        public DbSet<FilledFormChange> FilledFormChanges { get; set; } // ✅ NUEVO: qué valor cambió, quién y cuándo
        public DbSet<AlertConfigTemplate> AlertConfigTemplates { get; set; }
        public DbSet<DocumentoManual> DocumentosManuales { get; set; } // ✅ NUEVO: lista maestra cargada a mano por SGI
        public DbSet<SourceForm> SourceForms { get; set; }
        public DbSet<TemplateVersion> TemplateVersions { get; set; } // ✅ NUEVO: Historial de versiones
        
        // Nuevos módulos: Firmas, Alertas y Consumos
        public DbSet<Signature> Signatures { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<AlertConfiguration> AlertConfigurations { get; set; }
        public DbSet<CatalogoFirma> CatalogoFirmas { get; set; } // ✅ NUEVO: Catálogo de firmas
        public DbSet<FormDraft> FormDrafts { get; set; } // ✅ NUEVO: Borradores de formularios
        public DbSet<SignatureRejection> SignatureRejections { get; set; } // ✅ NUEVO: Rechazos de firma con motivo
        public DbSet<TemplateChangeLog> TemplateChangeLogs { get; set; } // ✅ NUEVO: Historial manual de cambios
        public DbSet<Ticket> Tickets { get; set; } // ✅ NUEVO: Mesa de ayuda
        public DbSet<TicketViewer> TicketViewers { get; set; } // ✅ NUEVO: Usuarios con acceso a ver todos los tickets
        public DbSet<Indicador> Indicadores { get; set; } // ✅ NUEVO: Tablero de indicadores
        public DbSet<Tablero> Tableros { get; set; } // ✅ NUEVO: Pestañas del tablero de indicadores

        // NUEVO: Tabla para indexar lotes y acelerar la trazabilidad
        public DbSet<LoteTrazabilidad> LotesTrazabilidad { get; set; }

        // INVENTARIO DE LOTES: Tabla completa para seguimiento de lotes por proceso
        public DbSet<LoteInventario> LotesInventario { get; set; }

        // Libro de movimientos (entradas/salidas) del inventario de lotes
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; }

        // Costo unitario por producto, cargado a mano: es lo que valoriza el kardex
        public DbSet<CostoProducto> CostosProducto { get; set; }

        // Cómo se arma el reporte de "Descargar Datos" de cada formulario
        public DbSet<ConfiguracionReporte> ConfiguracionesReporte { get; set; }

        // NUEVO: Planificador dinámico de producción
        public DbSet<ProductionPlan> ProductionPlans { get; set; }

        // Consultas fijas del Comparativo Plan (antes en localStorage de cada
        // navegador): actividad x bloque x columna -> receta.
        public DbSet<ConsultaPlan> ConsultasPlan { get; set; }

        // MÓDULO DE PERSONAL: registros estructurados de personal por proceso
        // (planta/externo, rango de horario) y sus estándares de tiempo.
        public DbSet<PersonalRegistro> PersonalRegistros { get; set; }
        public DbSet<ProcesoEstandar> ProcesoEstandares { get; set; }

        // ANTERIOR: Este DbSet ya no es necesario.
        // public DbSet<TableRow> TableRows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Template>()
                .HasIndex(t => t.Codigo)
                .IsUnique();

            // Una sola configuración de reporte por formulario: al guardar se
            // pisa la anterior en vez de acumular copias.
            modelBuilder.Entity<ConfiguracionReporte>()
                .HasIndex(c => c.TemplateID)
                .IsUnique();

            // Una sola consulta por actividad + bloque + columna: al guardar se
            // pisa la anterior en vez de acumular copias.
            modelBuilder.Entity<ConsultaPlan>()
                .HasIndex(c => new { c.Actividad, c.Grupo, c.Clave })
                .IsUnique();
        }
    }
}