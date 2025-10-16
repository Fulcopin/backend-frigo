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
        public DbSet<TableRow> TableRows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Template>()
                .HasIndex(t => t.Codigo)
                .IsUnique();
        }
    }
}