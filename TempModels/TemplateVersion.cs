using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class TemplateVersion
{
    public int VersionId { get; set; }

    public int TemplateId { get; set; }

    public string Version { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Supervisa { get; set; }

    public string? Proceso { get; set; }

    public string? CuandoSeUsa { get; set; }

    public string? QuienLoLlena { get; set; }

    public string? HeaderFields { get; set; }

    public string? BodyElements { get; set; }

    public string? Firmas { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? ChangeDescription { get; set; }

    public string? ModifiedBy { get; set; }

    public DateTime? FechaVersion { get; set; }

    public virtual Template Template { get; set; } = null!;
}
