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

        // ✅ AUDITORÍA: Información del usuario que llenó el formulario
        [StringLength(200)]
        public string? FilledBy { get; set; }  // Nombre del usuario
        
        [StringLength(200)]
        public string? FilledByEmail { get; set; }  // Email del usuario
        
        [StringLength(100)]
        public string? FilledByRole { get; set; }  // Rol del usuario

        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderData { get; set; }
        
        // NUEVO: Este campo almacenará el JSON con los datos de las secciones y tablas.
        [Column(TypeName = "nvarchar(max)")]
        public string? BodyData { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? FirmasData { get; set; }

        // 🦐🐟 NUEVO: Tipo de producto (Camarón o Pescado)
        [StringLength(50)]
        public string? TipoProducto { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Observaciones { get; set; }
        
        // ✅ Hora local del servidor (NO UTC)
        /// <summary>
        /// Fecha del REGISTRO: la que el operario escribió en el encabezado.
        ///
        /// Es distinta de CreatedAt, que es cuándo se apretó Guardar. Un
        /// formulario del día 9 puede guardarse el 10, y todos los filtros y
        /// listados tienen que usar el 9, que es lo que dice el papel.
        ///
        /// Vive como columna y no solo dentro del JSON de HeaderData para que
        /// SQL pueda filtrarla, ordenarla e indexarla: leer el JSON en cada
        /// consulta obligaba a traer todo a memoria.
        ///
        /// La calcula el backend al guardar, con FechaDelHeader().
        /// </summary>
        public DateTime? FechaRegistro { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}