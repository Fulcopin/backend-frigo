using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Formulario Fuente/Maestro
    /// Almacena datos básicos que pueden ser reutilizados en otros formularios
    /// Ejemplo: Registro de Producción Básico (Hora, Tina, Peso Neto)
    /// </summary>
    [Table("SourceForms")]
    public class SourceForm
    {
        [Key]
        public int SourceFormID { get; set; }

        /// <summary>
        /// Nombre/Tipo del formulario fuente
        /// Ejemplo: "Registro Producción Básico", "Control de Tinas", etc.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string FormType { get; set; } = string.Empty;

        /// <summary>
        /// Código único del registro (opcional)
        /// Ejemplo: "PROD-2025-001"
        /// </summary>
        [MaxLength(100)]
        public string? RecordCode { get; set; }

        /// <summary>
        /// Fecha del registro
        /// </summary>
        [Required]
        public DateTime RecordDate { get; set; }

        /// <summary>
        /// Datos del formulario en formato JSON
        /// Estructura flexible para diferentes tipos de formularios
        /// Ejemplo:
        /// {
        ///   "rows": [
        ///     { "hora": "08:00", "tina": "T1", "pesoNeto": 120.5 },
        ///     { "hora": "09:00", "tina": "T2", "pesoNeto": 150.3 }
        ///   ]
        /// }
        /// </summary>
        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string DataJson { get; set; } = string.Empty;

        /// <summary>
        /// Metadatos adicionales (opcional)
        /// Ejemplo: { "turno": "Mañana", "responsable": "Juan Pérez" }
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? Metadata { get; set; }

        /// <summary>
        /// Indica si este registro está activo y disponible para consulta
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Usuario que creó el registro
        /// </summary>
        [MaxLength(100)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Fecha de última actualización
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Notas o comentarios adicionales
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO para crear un formulario fuente
    /// </summary>
    public class CreateSourceFormDto
    {
        [Required(ErrorMessage = "El tipo de formulario es requerido")]
        public string FormType { get; set; } = string.Empty;

        public string? RecordCode { get; set; }

        [Required(ErrorMessage = "La fecha del registro es requerida")]
        public DateTime RecordDate { get; set; }

        [Required(ErrorMessage = "Los datos son requeridos")]
        public object Data { get; set; } = new { };

        public object? Metadata { get; set; }

        public string? CreatedBy { get; set; }

        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO para consultar datos de formularios fuente
    /// </summary>
    public class SourceFormQueryDto
    {
        public int SourceFormID { get; set; }
        public string FormType { get; set; } = string.Empty;
        public string? RecordCode { get; set; }
        public DateTime RecordDate { get; set; }
        public object Data { get; set; } = new { };
        public object? Metadata { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }

    /// <summary>
    /// DTO para filtrar consultas
    /// </summary>
    public class SourceFormFilterDto
    {
        public string? FormType { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? RecordCode { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// DTO para seleccionar filas específicas de un formulario fuente
    /// </summary>
    public class SourceFormRowSelectionDto
    {
        public int SourceFormID { get; set; }
        public List<int> RowIndices { get; set; } = new List<int>();
    }
}
