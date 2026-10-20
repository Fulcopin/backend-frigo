using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    public class Alert
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Type { get; set; } = string.Empty;
        [Required]
        public string Priority { get; set; } = string.Empty;
        [Required]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Message { get; set; } = string.Empty;
        [Required]
        public string TargetEmail { get; set; } = string.Empty;
        public int? FormId { get; set; }
        public string? FormCode { get; set; }
        [Required]
        public DateTime CreatedDate { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime? ReadDate { get; set; }
        [Required]
        public string Status { get; set; } = "pending";
        public virtual FilledForm? Form { get; set; }
    }

    public class AlertConfiguration
    {
        [Key]
        public int Id { get; set; }
        public bool EnableMissingFormAlerts { get; set; } = true;
        public string DailyCheckTime { get; set; } = "18:00";
        public string MissingFormRecipients { get; set; } = "[]";
        public bool EnableSignatureAlerts { get; set; } = true;
        public int SignatureAlertDelay { get; set; } = 24;
        public string SignatureRecipients { get; set; } = "[]";
        public int SummaryFrequencyDays { get; set; } = 7;
        public bool EnableTemplateChangeAlerts { get; set; } = false;
        public string TemplateChangeRecipients { get; set; } = "[]";
        public int LockThresholdHours { get; set; } = 36;
        // 📧 Resumen de cumplimiento (qué registros faltan y cuáles no están
        //    firmados). Se manda cada ResumenCadaHoras a los destinatarios
        //    fijos y, si se pide, a cada responsable con lo suyo.
        public bool EnableResumenCumplimiento { get; set; } = false;
        /// <summary>Cada cuántas horas se envía (10 por defecto).</summary>
        public int ResumenCadaHoras { get; set; } = 10;
        /// <summary>Cuántos días hacia atrás se revisan.</summary>
        public int ResumenDiasAtras { get; set; } = 7;
        /// <summary>¿El sábado y el domingo también se exigen?</summary>
        public bool ResumenIncluirFinDeSemana { get; set; } = true;
        /// <summary>Además de los destinatarios fijos, avisar a cada responsable y sus reemplazos.</summary>
        public bool ResumenAResponsables { get; set; } = true;
        public string ResumenRecipients { get; set; } = "[]";
        /// <summary>Última vez que se envió; evita repetir al reiniciar la API.</summary>
        public DateTime? UltimoResumenCumplimiento { get; set; }

        public string SenderEmail { get; set; } = "alertas@frigolab.com";
        public string SenderName { get; set; } = "Frigolab Alertas";
    }

    public class SendTestEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class CreateManualAlertRequest
    {
        public string Type { get; set; } = "system";
        public string Priority { get; set; } = "medium";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TargetEmail { get; set; } = string.Empty;
    }

    public class AlertStatsResponse
    {
        public int ActiveCount { get; set; }
        public int SentToday { get; set; }
        public int TotalSent { get; set; }
        public int FailedCount { get; set; }
    }

    public class SendFormEmailRequest
    {
        public string Email { get; set; } = string.Empty;
        public string FormName { get; set; } = string.Empty;
        public string FormCode { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
        public Dictionary<string, object>? HeaderData { get; set; }
        public List<FormBodySection>? BodySections { get; set; }
        public List<FormFirmaInfo>? Firmas { get; set; }
    }

    public class FormBodySection
    {
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = "section";
        public List<string>? Columns { get; set; }
        public List<Dictionary<string, object>>? Rows { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }

    public class FormFirmaInfo
    {
        public string Puesto { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string? Fecha { get; set; }
        public bool TieneFirma { get; set; }
    }
}
