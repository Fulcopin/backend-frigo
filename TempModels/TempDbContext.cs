using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.TempModels;

public partial class TempDbContext : DbContext
{
    public TempDbContext()
    {
    }

    public TempDbContext(DbContextOptions<TempDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Alert> Alerts { get; set; }

    public virtual DbSet<AlertConfiguration> AlertConfigurations { get; set; }

    public virtual DbSet<CatalogoFirma> CatalogoFirmas { get; set; }

    public virtual DbSet<FilledForm> FilledForms { get; set; }

    public virtual DbSet<Signature> Signatures { get; set; }

    public virtual DbSet<SourceForm> SourceForms { get; set; }

    public virtual DbSet<Template> Templates { get; set; }

    public virtual DbSet<TemplateVersion> TemplateVersions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=ConnectionStrings:DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasIndex(e => e.FormId, "IX_Alerts_FormId");

            entity.HasOne(d => d.Form).WithMany(p => p.Alerts).HasForeignKey(d => d.FormId);
        });

        modelBuilder.Entity<CatalogoFirma>(entity =>
        {
            entity.Property(e => e.CatalogoFirmaId).HasColumnName("CatalogoFirmaID");
            entity.Property(e => e.Area).HasMaxLength(100);
            entity.Property(e => e.Correo).HasMaxLength(150);
            entity.Property(e => e.NombreCompleto).HasMaxLength(200);
            entity.Property(e => e.Puesto).HasMaxLength(100);
        });

        modelBuilder.Entity<FilledForm>(entity =>
        {
            entity.HasKey(e => e.FormId);

            entity.HasIndex(e => e.TemplateId, "IX_FilledForms_TemplateID");

            entity.Property(e => e.FormId).HasColumnName("FormID");
            entity.Property(e => e.FilledBy).HasMaxLength(200);
            entity.Property(e => e.FilledByEmail).HasMaxLength(200);
            entity.Property(e => e.FilledByRole).HasMaxLength(100);
            entity.Property(e => e.TemplateId).HasColumnName("TemplateID");
            entity.Property(e => e.TemplateVersion).HasMaxLength(20);
            entity.Property(e => e.TipoProducto).HasMaxLength(50);

            entity.HasOne(d => d.Template).WithMany(p => p.FilledForms).HasForeignKey(d => d.TemplateId);
        });

        modelBuilder.Entity<Signature>(entity =>
        {
            entity.HasIndex(e => e.FilledFormId, "IX_Signatures_FilledFormId");

            entity.Property(e => e.IsModifiedBySgi).HasColumnName("IsModifiedBySGI");

            entity.HasOne(d => d.FilledForm).WithMany(p => p.Signatures).HasForeignKey(d => d.FilledFormId);
        });

        modelBuilder.Entity<SourceForm>(entity =>
        {
            entity.Property(e => e.SourceFormId).HasColumnName("SourceFormID");
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.FormType).HasMaxLength(200);
            entity.Property(e => e.RecordCode).HasMaxLength(100);
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasIndex(e => e.Codigo, "IX_Templates_Codigo").IsUnique();

            entity.Property(e => e.TemplateId).HasColumnName("TemplateID");
            entity.Property(e => e.Area).HasMaxLength(100);
            entity.Property(e => e.Codigo).HasMaxLength(50);
            entity.Property(e => e.Frecuencia).HasMaxLength(50);
            entity.Property(e => e.Nombre).HasMaxLength(255);
            entity.Property(e => e.Version).HasMaxLength(20);
        });

        modelBuilder.Entity<TemplateVersion>(entity =>
        {
            entity.HasKey(e => e.VersionId).HasName("PK__Template__16C6402F7F9F85DC");

            entity.HasIndex(e => e.CreatedAt, "IX_TemplateVersions_CreatedAt").IsDescending();

            entity.HasIndex(e => e.TemplateId, "IX_TemplateVersions_TemplateID");

            entity.HasIndex(e => e.Version, "IX_TemplateVersions_Version");

            entity.Property(e => e.VersionId).HasColumnName("VersionID");
            entity.Property(e => e.ChangeDescription).HasMaxLength(500);
            entity.Property(e => e.Codigo).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.Nombre).HasMaxLength(200);
            entity.Property(e => e.TemplateId).HasColumnName("TemplateID");
            entity.Property(e => e.Version).HasMaxLength(50);

            entity.HasOne(d => d.Template).WithMany(p => p.TemplateVersions)
                .HasForeignKey(d => d.TemplateId)
                .HasConstraintName("FK_TemplateVersions_Templates");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
