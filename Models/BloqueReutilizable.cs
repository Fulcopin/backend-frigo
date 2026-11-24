using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// 📚 Bloque de formulario reutilizable (sección, tabla, Control del
    /// Personal, procesos, materia prima…). Se guarda como JSON y se puede
    /// insertar en cualquier plantilla desde el editor.
    /// </summary>
    public class BloqueReutilizable
    {
        [Key]
        public int Id { get; set; }

        [StringLength(200)]
        public string Nombre { get; set; } = "";

        [StringLength(50)]
        public string Tipo { get; set; } = "section";

        /// <summary>JSON del elemento tal como lo arma el editor.</summary>
        public string Contenido { get; set; } = "{}";

        [StringLength(200)]
        public string? CreadoPor { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.Now;
    }
}
