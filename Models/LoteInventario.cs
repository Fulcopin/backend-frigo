using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Inventario de lotes de trazabilidad.
    /// Regla fundamental: PesoNeto = PesoEntrada - Desperdicio
    /// Un lote puede existir sin estar ligado a un formulario (ingreso manual).
    /// </summary>
    public class LoteInventario
    {
        [Key]
        public int Id { get; set; }

        // ── Identificación del lote ────────────────────────────────────────────
        [Required]
        [StringLength(100)]
        public string NumeroLote { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Proceso { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Producto { get; set; }

        [StringLength(100)]
        public string? Clasificacion { get; set; }

        // ── Pesos ─────────────────────────────────────────────────────────────
        [Column(TypeName = "decimal(18,4)")]
        public decimal PesoEntrada { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Desperdicio { get; set; }

        [StringLength(100)]
        public string? TipoDesperdicio { get; set; }

        /// <summary>
        /// Calculado: PesoEntrada - Desperdicio  (almacenado para eficiencia en consultas)
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal PesoNeto { get; set; }

        // ── Estado ────────────────────────────────────────────────────────────
        /// <summary>disponible | consumido | parcial</summary>
        [StringLength(20)]
        public string Estado { get; set; } = "disponible";

        // ── Trazabilidad jerárquica ───────────────────────────────────────────
        [StringLength(100)]
        public string? LotePadre { get; set; }

        // ── Origen (referencia al formulario que lo creó) ─────────────────────
        public int? FormId { get; set; }

        [StringLength(50)]
        public string? TemplateId { get; set; }

        // ── Metadatos ─────────────────────────────────────────────────────────
        [Column(TypeName = "date")]
        public DateTime? Fecha { get; set; }

        [StringLength(500)]
        public string? Notas { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    public class LoteInventarioCreateDto
    {
        [Required]
        public string NumeroLote { get; set; } = string.Empty;
        [Required]
        public string Proceso { get; set; } = string.Empty;
        public string? Producto { get; set; }
        public string? Clasificacion { get; set; }
        public decimal PesoEntrada { get; set; }
        public decimal Desperdicio { get; set; }
        public string? TipoDesperdicio { get; set; }
        public string? LotePadre { get; set; }
        public int? FormId { get; set; }
        public string? TemplateId { get; set; }
        public DateTime? Fecha { get; set; }
        public string? Notas { get; set; }
    }

    public class LoteBulkCreateDto
    {
        public List<LoteInventarioCreateDto> Lotes { get; set; } = new();
        /// <summary>Si se provee, ese lote se marca como consumido al guardar el batch.</summary>
        public string? LoteOrigenConsumir { get; set; }
    }

    public class LoteInventarioUpdateDto
    {
        public string? Producto { get; set; }
        public string? Clasificacion { get; set; }
        public decimal? PesoEntrada { get; set; }
        public decimal? Desperdicio { get; set; }
        public string? TipoDesperdicio { get; set; }
        public string? Estado { get; set; }
        public string? Notas { get; set; }
    }
}
