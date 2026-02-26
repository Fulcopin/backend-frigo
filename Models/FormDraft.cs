using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Borrador de formulario - Se guarda en BD para persistir hasta por 7 días.
    /// Permite a los usuarios continuar llenando formularios en múltiples sesiones.
    /// </summary>
    public class FormDraft
    {
        [Key]
        public int DraftID { get; set; }

        /// <summary>ID del template que se está llenando</summary>
        public int TemplateID { get; set; }

        /// <summary>Nombre del template (para mostrar en la lista)</summary>
        [StringLength(300)]
        public string? TemplateName { get; set; }

        /// <summary>Código del template</summary>
        [StringLength(50)]
        public string? TemplateCodigo { get; set; }

        /// <summary>Nombre del usuario que creó el borrador</summary>
        [StringLength(200)]
        public string? UserName { get; set; }

        /// <summary>Email del usuario</summary>
        [StringLength(200)]
        public string? UserEmail { get; set; }

        /// <summary>Rol del usuario</summary>
        [StringLength(100)]
        public string? UserRole { get; set; }

        /// <summary>Datos del encabezado (JSON)</summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderData { get; set; }

        /// <summary>Datos del body: secciones y tablas (JSON)</summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? BodyData { get; set; }

        /// <summary>Datos de firmas sin imágenes (JSON)</summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? FirmasData { get; set; }

        /// <summary>Snapshot del template al momento de crear el borrador (JSON)</summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? TemplateSnapshot { get; set; }

        /// <summary>Porcentaje estimado de avance (0-100)</summary>
        public int Progress { get; set; } = 0;

        /// <summary>Nota opcional del usuario</summary>
        [StringLength(500)]
        public string? Nota { get; set; }

        /// <summary>Fecha de creación</summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Última actualización</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>Fecha de expiración (7 días por defecto)</summary>
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(7);

        /// <summary>Si el borrador está activo</summary>
        public bool IsActive { get; set; } = true;
    }
}
