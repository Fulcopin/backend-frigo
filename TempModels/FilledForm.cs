using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class FilledForm
{
    public int FormId { get; set; }

    public int TemplateId { get; set; }

    public string? HeaderData { get; set; }

    public string? FirmasData { get; set; }

    public string? Observaciones { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? BodyData { get; set; }

    public string? TemplateSnapshot { get; set; }

    public string? TemplateVersion { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? FechaVersion { get; set; }

    public string? FilledBy { get; set; }

    public string? FilledByEmail { get; set; }

    public string? FilledByRole { get; set; }

    public string? TipoProducto { get; set; }

    public virtual ICollection<Alert> Alerts { get; set; } = new List<Alert>();

    public virtual ICollection<Signature> Signatures { get; set; } = new List<Signature>();

    public virtual Template Template { get; set; } = null!;
}
