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

        // NUEVO: Tabla para indexar lotes y acelerar la trazabilidad
        public DbSet<LoteTrazabilidad> LotesTrazabilidad { get; set; }

        // INVENTARIO DE LOTES: Tabla completa para seguimiento de lotes por proceso
        public DbSet<LoteInventario> LotesInventario { get; set; }

        // ANTERIOR: Este DbSet ya no es necesario.
        // public DbSet<TableRow> TableRows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Template>()
                .HasIndex(t => t.Codigo)
                .IsUnique();
        }
    }
}