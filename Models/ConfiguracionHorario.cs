using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// ⚙️ Horarios de los turnos DÍA y NOCHE, configurables desde la pantalla.
    ///
    /// Una sola fila para todo el sistema: el bloqueo del formulario (turno del
    /// encabezado vs horas del "Control del Personal") compara contra estos
    /// rangos. Si la hora de fin es menor que la de inicio, el turno cruza la
    /// medianoche (ej. NOCHE 17:00 → 05:59).
    /// </summary>
    public class ConfiguracionHorario
    {
        [Key]
        public int Id { get; set; }

        [StringLength(5)]
        public string TurnoDiaInicio { get; set; } = "06:00";

        [StringLength(5)]
        public string TurnoDiaFin { get; set; } = "17:00";

        [StringLength(5)]
        public string TurnoNocheInicio { get; set; } = "17:00";

        [StringLength(5)]
        public string TurnoNocheFin { get; set; } = "05:59";

        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime? ActualizadoEn { get; set; }
    }
}
