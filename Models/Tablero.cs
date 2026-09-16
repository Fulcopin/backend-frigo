using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    // Una pestaña del tablero de "Indicadores" (equivale a una página de Power BI).
    // Cada pestaña tiene un alcance (scope): puede apuntar a un registro de producción
    // concreto, a un proceso completo, o a todos los datos.
    // Los indicadores que viven dentro heredan ese alcance.
    public class Tablero
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Nombre { get; set; } = string.Empty;

        // "todos" | "registro" | "proceso"
        [Required]
        [StringLength(20)]
        public string ScopeTipo { get; set; } = "todos";

        // Nombre del registro (Template.Nombre) o del proceso (Template.Proceso).
        // Null cuando ScopeTipo == "todos".
        public string? ScopeValor { get; set; }

        public int Orden { get; set; } = 0;

        public string? CreadoPor { get; set; }

        [Required]
        public DateTime CreadoEn { get; set; }
    }
}
