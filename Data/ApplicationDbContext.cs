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