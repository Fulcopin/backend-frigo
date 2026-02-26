using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class SourceForm
{
    public int SourceFormId { get; set; }

    public string FormType { get; set; } = null!;

    public string? RecordCode { get; set; }

    public DateTime RecordDate { get; set; }

    public string DataJson { get; set; } = null!;

    public string? Metadata { get; set; }

    public bool IsActive { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Notes { get; set; }
}
