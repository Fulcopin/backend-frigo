using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class Signature
{
    public int Id { get; set; }

    public int FilledFormId { get; set; }

    public string SignatureImage { get; set; } = null!;

    public string SignedBy { get; set; } = null!;

    public DateTime SignedDate { get; set; }

    public string? Comments { get; set; }

    public bool IsModifiedBySgi { get; set; }

    public DateTime? OriginalSignedDate { get; set; }

    public virtual FilledForm FilledForm { get; set; } = null!;
}
