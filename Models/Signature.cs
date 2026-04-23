using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    public class Signature
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int FilledFormId { get; set; }
        
        [Required]
        public string SignatureImage { get; set; } = string.Empty; // Base64 de la imagen
        
        [Required]
        public string SignedBy { get; set; } = string.Empty; // Email del firmante
        
        [Required]
        public DateTime SignedDate { get; set; }
        
        public string? Comments { get; set; }
        
        public bool IsModifiedBySGI { get; set; } = false;
        
        public DateTime? OriginalSignedDate { get; set; }
        
        // Navegación
        public virtual FilledForm? FilledForm { get; set; }
    }
    
    // DTOs para las respuestas
    public class FormSignatureStatus
    {
        public int FilledFormId { get; set; }
        public bool IsSigned { get; set; }
        public bool IsRejected { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? SignedBy { get; set; }
        public string? RejectionReason { get; set; }
    }
    
    public class SignFormRequest
    {
        public int FormId { get; set; }
        public string SignatureImage { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedDate { get; set; }
        public string? Comments { get; set; }
        public string? SignerNombre { get; set; }
    }
    
    public class SignMultipleFormsRequest
    {
        public List<int> FormIds { get; set; } = new();
        public string SignatureImage { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedDate { get; set; }
        public string? Comments { get; set; }
        public string? SignerNombre { get; set; }
    }
    
    public class RejectFormRequest
    {
        public int FormId { get; set; }
        public string RejectedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime RejectedDate { get; set; }
    }
    
    public class UpdateSignatureDateRequest
    {
        public int SignatureId { get; set; }
        public DateTime NewDate { get; set; }
    }
    
    public class SignatureStatsResponse
    {
        public int PendingCount { get; set; }
        public int SignedToday { get; set; }
        public int TotalSigned { get; set; }
        public int RejectedCount { get; set; }
    }

    /// <summary>
    /// Modelo para registrar rechazos de firma con motivo opcional
    /// </summary>
    public class SignatureRejection
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int FilledFormId { get; set; }

        [Required]
        public string RejectedBy { get; set; } = string.Empty;

        public DateTime RejectedDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Motivo del rechazo (OPCIONAL)
        /// </summary>
        [StringLength(1000)]
        public string? Reason { get; set; }

        /// <summary>
        /// Estado: rejected, returned, etc.
        /// </summary>
        [StringLength(50)]
        public string Status { get; set; } = "rejected";

        public virtual FilledForm? FilledForm { get; set; }
    }

    /// <summary>
    /// DTO de respuesta para reporte de tiempos de firma
    /// </summary>
    public class SignatureTimingReport
    {
        public int FilledFormId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public string FormCode { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? SignedDate { get; set; }
        public string? SignedBy { get; set; }
        public double? HoursToSign { get; set; }
        public string? TimingLabel { get; set; }
        public string Status { get; set; } = "pending"; // pending, signed, rejected
        public string? RejectionReason { get; set; }
        public string? RejectedBy { get; set; }
        public DateTime? RejectedDate { get; set; }
    }

    /// <summary>
    /// DTO de resumen de tiempos
    /// </summary>
    public class SignatureTimingSummary
    {
        public double AverageHoursToSign { get; set; }
        public double FastestHours { get; set; }
        public double SlowestHours { get; set; }
        public int TotalSigned { get; set; }
        public int TotalPending { get; set; }
        public int TotalRejected { get; set; }
        public int SignedWithin24h { get; set; }
        public int SignedAfter24h { get; set; }
        public int SignedAfter72h { get; set; }
        public List<SignatureTimingReport> Details { get; set; } = new();
    }
}
