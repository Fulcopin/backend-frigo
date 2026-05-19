using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class LoteTrazabilidad
    {
        [Key]
        public int Id { get; set; }

        public int FormID { get; set; }
        [ForeignKey("FormID")]
        public virtual FilledForm? FilledForm { get; set; }

        [StringLength(100)]
        public string LoteOrigen { get; set; } = string.Empty;

        [StringLength(100)]
        public string? LoteDestino { get; set; }

        [StringLength(200)]
        public string Proceso { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Producto { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CantidadEntrada { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CantidadSalida { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
