using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    // Un indicador guardado del tablero de "Indicadores".
    // La configuración completa (formulario/tabla/columnas/operación/gráfico) se guarda
    // serializada como JSON en ConfigJson, para no atarse a un esquema rígido.
    public class Indicador
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        public string ConfigJson { get; set; } = "{}";

        public string? CreadoPor { get; set; }

        public int Orden { get; set; } = 0;

        [Required]
        public DateTime CreadoEn { get; set; }
    }
}
