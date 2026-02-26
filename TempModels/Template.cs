using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class Template
{
    public int TemplateId { get; set; }

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string Version { get; set; } = null!;

    public string? Objetivo { get; set; }

    public string? Proceso { get; set; }

    public string? CuandoSeUsa { get; set; }

    public string? QuienLoLlena { get; set; }

    public string? HeaderFields { get; set; }

    public string? BodyElements { get; set; }

    public string? Firmas { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? FechaVersion { get; set; }

    public string? Area { get; set; }

    public string? Frecuencia { get; set; }

    public bool IsDraft { get; set; }

    public virtual ICollection<FilledForm> FilledForms { get; set; } = new List<FilledForm>();

    public virtual ICollection<TemplateVersion> TemplateVersions { get; set; } = new List<TemplateVersion>();
}
