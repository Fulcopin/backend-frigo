using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Registro de personal por proceso (planta vs externo, rango de horario).
    /// Se guarda como fila estructurada (no dentro del JSON de FilledForm) para
    /// que los indicadores de costos no requieran parsear BodyData en cada consulta,
    /// siguiendo el mismo patrón que LoteInventario / MovimientoInventario.
    /// </summary>
    public class PersonalRegistro
    {
        [Key]
        public int Id { get; set; }

        // ── Origen / trazabilidad ────────────────────────────────────────────
        public int FormID { get; set; }
        [ForeignKey("FormID")]
        public virtual FilledForm? FilledForm { get; set; }

        [StringLength(50)]
        public string? TemplateId { get; set; }

        /// <summary>
        /// Proceso que se está midiendo (título del registro o campo específico).
        /// Debe coincidir con Template.Proceso / ProcesoEstandar.Proceso para
        /// permitir join, filtrado y ordenamiento por la jerarquía
        /// Materia Prima -> Producción -> Personal -> Insumos/Subproductos.
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Proceso { get; set; } = string.Empty;

        [Column(TypeName = "date")]
        public DateTime Fecha { get; set; }

        // ── Datos funcionales ────────────────────────────────────────────────
        public int PersonalPlanta { get; set; }
        public int PersonalExterno { get; set; }

        public TimeSpan HoraDesde { get; set; }
        public TimeSpan HoraHasta { get; set; }

        /// <summary>Calculado: HoraHasta - HoraDesde, en horas (guardado para eficiencia en consultas).</summary>
        [Column(TypeName = "decimal(6,2)")]
        public decimal HorasTrabajadas { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Observaciones { get; set; }

        // ── Resultado de la validación de reglas de negocio ─────────────────
        public bool CumpleEstandar { get; set; } = true;

        /// <summary>"inicio_tardio" | "fin_temprano" | "duracion_menor_al_minimo" | "duracion_mayor_al_maximo" (combinables con coma)</summary>
        [StringLength(500)]
        public string? MotivoIncumplimiento { get; set; }

        /// <summary>Obligatoria cuando CumpleEstandar = false (validada en el servicio, no en la BD).</summary>
        [StringLength(1000)]
        public string? JustificacionVariacion { get; set; }

        // ── Auditoría ─────────────────────────────────────────────────────────
        [StringLength(200)]
        public string? CreadoPor { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public DateTime? ActualizadoEn { get; set; }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class PersonalRegistroCreateDto
    {
        [Required]
        public int FormID { get; set; }
        public string? TemplateId { get; set; }

        [Required]
        public string Proceso { get; set; } = string.Empty;

        [Required]
        public DateTime Fecha { get; set; }

        public int PersonalPlanta { get; set; }
        public int PersonalExterno { get; set; }

        [Required]
        public TimeSpan HoraDesde { get; set; }

        [Required]
        public TimeSpan HoraHasta { get; set; }

        public string? Observaciones { get; set; }
        public string? JustificacionVariacion { get; set; }
    }

    public class PersonalRegistroUpdateDto
    {
        public int PersonalPlanta { get; set; }
        public int PersonalExterno { get; set; }
        public TimeSpan HoraDesde { get; set; }
        public TimeSpan HoraHasta { get; set; }
        public string? Observaciones { get; set; }
        public string? JustificacionVariacion { get; set; }
    }

    public class PersonalIndicadorDto
    {
        public string Proceso { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public int TotalPlanta { get; set; }
        public int TotalExterno { get; set; }
        public decimal TotalHoras { get; set; }
        public int Registros { get; set; }
        public int Incumplimientos { get; set; }
    }
}
