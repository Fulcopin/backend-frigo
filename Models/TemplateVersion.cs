using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Representa un snapshot/versión histórica de una plantilla
    /// Cada vez que se edita una plantilla, se guarda un registro aquí
    /// </summary>
    [Table("TemplateVersions")]
    public class TemplateVersion
    {
        [Key]
        public int VersionID { get; set; }

        // FK al template original
        public int TemplateID { get; set; }

        // Versión de la plantilla (ej: "02-08", "03-00")
        [Required]
        [MaxLength(50)]
        public string Version { get; set; } = string.Empty;

        // Fecha en que entra en vigor esta versión
        public DateTime? FechaVersion { get; set; }

        // Snapshot completo del template en esta versión
        [Required]
        [MaxLength(100)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        public string? Supervisa { get; set; }
        public string? Proceso { get; set; }
        public string? CuandoSeUsa { get; set; }
        public string? QuienLoLlena { get; set; }

        // JSON strings de la estructura
        public string? HeaderFields { get; set; }
        public string? BodyElements { get; set; }
        public string? Firmas { get; set; }

        // Metadata de la versión
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(500)]
        public string? ChangeDescription { get; set; } // Descripción de qué cambió

        // Quién hizo el cambio (opcional, para futura implementación)
        [MaxLength(100)]
        public string? ModifiedBy { get; set; }

        // Relación con el template original
        [ForeignKey("TemplateID")]
        public virtual Template? Template { get; set; }
    }
}
