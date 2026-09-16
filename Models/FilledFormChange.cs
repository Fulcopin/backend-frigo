using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// AUDITORÍA DE VALORES DE UN FORMULARIO YA GUARDADO.
    ///
    /// Cada vez que se vuelve a guardar un formulario se compara lo que había
    /// contra lo que llega y se registra QUÉ celda cambió, de qué valor a qué
    /// valor, quién lo hizo y cuándo. Sirve para responder la pregunta
    /// "¿este reporte ya revisado fue modificado después?".
    ///
    /// Los cambios de una misma persona seguidos en el tiempo se agrupan en una
    /// sola fila (ver VENTANA_AGRUPADO_MIN en FilledFormsController) para que un
    /// autoguardado no genere cien registros.
    /// </summary>
    public class FilledFormChange
    {
        [Key]
        public int Id { get; set; }

        public int FormID { get; set; }
        [ForeignKey("FormID")]
        public virtual FilledForm? FilledForm { get; set; }

        /// <summary>Quién hizo la modificación (nombre visible).</summary>
        [StringLength(200)]
        public string? ChangedBy { get; set; }

        [StringLength(200)]
        public string? ChangedByEmail { get; set; }

        [StringLength(100)]
        public string? ChangedByRole { get; set; }

        /// <summary>Cuándo empezó esta tanda de cambios (hora local del servidor).</summary>
        public DateTime ChangedAt { get; set; } = DateTime.Now;

        /// <summary>Último guardado de esta misma tanda.</summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Lista de cambios en JSON. Cada entrada:
        /// { clave, ambito, elemento, fila, campo, seccion, antes, despues }
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? Cambios { get; set; }

        /// <summary>Cuántos valores cambiaron (para no tener que parsear el JSON).</summary>
        public int TotalCambios { get; set; }
    }
}
