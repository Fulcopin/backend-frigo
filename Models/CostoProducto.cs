using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Costo unitario de un producto, cargado a mano por el área de Costos.
    ///
    /// El inventario de lotes solo guarda libras: no hay ningún precio ni en la
    /// trazabilidad ni en el catálogo externo. Esta tabla es la que le pone valor
    /// al kardex — un costo fijo por producto que se aplica tanto a las entradas
    /// como a las salidas.
    /// </summary>
    public class CostoProducto
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Nombre del producto tal como aparece en el inventario de lotes.</summary>
        [Required]
        [StringLength(200)]
        public string Producto { get; set; } = string.Empty;

        /// <summary>Costo por unidad (por defecto, por libra).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoUnitario { get; set; }

        [StringLength(10)]
        public string Moneda { get; set; } = "USD";

        /// <summary>Unidad a la que corresponde el costo: Lb, Kg, Unidad…</summary>
        [StringLength(20)]
        public string Unidad { get; set; } = "Lb";

        [StringLength(500)]
        public string? Notas { get; set; }

        [StringLength(150)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
        public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    }

    // ── DTOs ────────────────────────────────────────────────────────────────

    /// <summary>Alta o actualización del costo de un producto (upsert por nombre).</summary>
    public class CostoProductoUpsertDto
    {
        [Required]
        public string Producto { get; set; } = string.Empty;

        public decimal CostoUnitario { get; set; }
        public string? Moneda { get; set; }
        public string? Unidad { get; set; }
        public string? Notas { get; set; }
        public string? ActualizadoPor { get; set; }
    }
}
