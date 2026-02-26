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
    }
    
    public class SignMultipleFormsRequest
    {
        public List<int> FormIds { get; set; } = new();
        public string SignatureImage { get; set; } = string.Empty;
        public string SignedBy { get; set; } = string.Empty;
        public DateTime SignedDate { get; set; }
        public string? Comments { get; set; }
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
}
