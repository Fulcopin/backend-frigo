// Contenido para: Models/FilledForm.cs

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class FilledForm
    {
        [Key]
        public int FormID { get; set; }

        public int TemplateID { get; set; }
        [ForeignKey("TemplateID")]
        public virtual Template? Template { get; set; }

        // NUEVO: Versionamiento - Snapshot del template al momento de creación
        [Column(TypeName = "nvarchar(max)")]
        public string? TemplateSnapshot { get; set; }

        // NUEVO: Versión específica del template utilizada
        [StringLength(20)]
        public string? TemplateVersion { get; set; }

        // NUEVO: Fecha de la versión del template
        public DateTime? FechaVersion { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderData { get; set; }
        
        // NUEVO: Este campo almacenará el JSON con los datos de las secciones y tablas.
        [Column(TypeName = "nvarchar(max)")]
        public string? BodyData { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? FirmasData { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Observaciones { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}