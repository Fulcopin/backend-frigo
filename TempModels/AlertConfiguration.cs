using System;
using System.Collections.Generic;

namespace FormBuilder.API.TempModels;

public partial class AlertConfiguration
{
    public int Id { get; set; }

    public bool EnableMissingFormAlerts { get; set; }

    public string DailyCheckTime { get; set; } = null!;

    public string MissingFormRecipients { get; set; } = null!;

    public bool EnableSignatureAlerts { get; set; }

    public int SignatureAlertDelay { get; set; }

    public string SignatureRecipients { get; set; } = null!;

    public string SenderEmail { get; set; } = null!;

    public string SenderName { get; set; } = null!;
}
