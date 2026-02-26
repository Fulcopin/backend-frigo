using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class Alert
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;

    public string Priority { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public string TargetEmail { get; set; } = null!;

    public int? FormId { get; set; }

    public string? FormCode { get; set; }

    public DateTime CreatedDate { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadDate { get; set; }

    public string Status { get; set; } = null!;

    public virtual FilledForm? Form { get; set; }
}
