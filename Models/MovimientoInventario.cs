using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Libro de movimientos del inventario de lotes: cada fila es una ENTRADA
    /// (el lote se creó / ingresó peso) o una SALIDA (un proceso posterior consumió
    /// cierta cantidad). El saldo de un lote = suma de entradas − suma de salidas.
    /// </summary>
    public class MovimientoInventario
    {
        [Key]
        public int Id { get; set; }

        // Lote afectado
        public int LoteInventarioId { get; set; }

        [Required]
        [StringLength(100)]
        public string NumeroLote { get; set; } = string.Empty;

        /// <summary>entrada | salida</summary>
        [Required]
        [StringLength(10)]
        public string Tipo { get; set; } = "salida";

        [Column(TypeName = "decimal(18,4)")]
        public decimal Cantidad { get; set; }

        /// <summary>Saldo del lote DESPUÉS de aplicar este movimiento (foto para auditoría).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal SaldoResultante { get; set; }

        // ── Contexto: qué proceso/formulario generó el movimiento ─────────────
        [StringLength(200)]
        public string? Proceso { get; set; }

        public int? FormId { get; set; }

        [StringLength(500)]
        public string? Notas { get; set; }

        // ── Trazabilidad: la arista padre → hijo ──────────────────────────────
        /// <summary>
        /// Lote que se GENERÓ con esta salida.
        ///
        /// Sin este campo un movimiento dice "salieron 872 Lbs del lote 251110"
        /// pero no hacia dónde, y la paternidad tiene que vivir en el campo
        /// LotePadre del hijo, donde cabe UN solo padre. Con el destino acá, un
        /// lote hijo puede tener todos los padres que haga falta: son filas
        /// distintas de esta tabla, cada una con su cantidad real, y el
        /// porcentaje de aporte se calcula en vez de escribirse.
        ///
        /// Queda NULL en las salidas que no generan lote (consumo simple,
        /// cambio de proceso) y en todo lo anterior a la migración.
        /// </summary>
        [StringLength(100)]
        public string? LoteDestino { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    /// <summary>Consumir (dar salida) a una cantidad de un lote desde un proceso posterior.</summary>
    public class ConsumirCantidadDto
    {
        [Required]
        public string NumeroLote { get; set; } = string.Empty;

        [Required]
        public decimal Cantidad { get; set; }

        public string? Proceso { get; set; }
        public int? FormId { get; set; }
        public string? Notas { get; set; }
    }
}