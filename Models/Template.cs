// Contenido para: Models/Template.cs

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class Template
    {
        [Key]
        public int TemplateID { get; set; }

        [Required]
        [StringLength(50)]
        public string Codigo { get; set; }

        [Required]
        [StringLength(255)]
        public string Nombre { get; set; }

        [StringLength(20)]
        public string Version { get; set; } = "1";

        // Fecha en que entra en vigor esta versión
        public DateTime? FechaVersion { get; set; }

        public string? Supervisa { get; set; }
        public string? Proceso { get; set; }
        public string? CuandoSeUsa { get; set; }
        public string? QuienLoLlena { get; set; }

        // Campos adicionales para módulos de Firmas, Alertas y Consumos
        [StringLength(100)]
        public string? Area { get; set; }
        
        [StringLength(50)]
        public string? Frecuencia { get; set; } // "Diaria", "Semanal", "Mensual", etc.

        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderFields { get; set; }

        // ANTERIOR:
        // [Column(TypeName = "nvarchar(max)")]
        // public string? TableColumns { get; set; }

        // NUEVO: Renombrado para almacenar la nueva estructura dinámica del cuerpo.
        [Column(TypeName = "nvarchar(max)")]
        public string? BodyElements { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Firmas { get; set; }

        // ✅ NUEVO: Indica si la plantilla es un borrador (no publicada)
        public bool IsDraft { get; set; } = false;

        // ✅ Indica si la plantilla tiene auto-suma de FILAS activada (para tablas con columnas PESO/TOTAL)
        public bool IsMasterForm { get; set; } = false;

        // ✅ NUEVO: Indica si la plantilla muestra totales automáticos por COLUMNA (suma al pie de tabla)
        public bool AutoSumColumns { get; set; } = false;

        // ✅ NUEVO: Indica si la plantilla necesita datos de la API externa (ERP)
        public bool UsaApi { get; set; } = false;

        // ✅ NUEVO: Indica si la plantilla está obsoleta (no aparece en listado para llenar pero se conservan registros)
        public bool IsObsolete { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}