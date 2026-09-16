using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// DOCUMENTO DE LA LISTA MAESTRA CARGADO A MANO POR SGI.
    ///
    /// La lista maestra (FOR-SGC-3) no son solo los formularios del sistema:
    /// también entran procedimientos, programas, manuales e instructivos que
    /// viven en papel o en Word (PR-TH-1, PR-TH-2…). Esta tabla los digitaliza
    /// para que la lista maestra salga completa de un solo lugar.
    ///
    /// Se agrupan por ÁREA (TH, SGC, BOD, CA, CC, PD…), que es la pestaña en la
    /// que se cargan.
    /// </summary>
    public class DocumentoManual
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Área / pestaña donde se carga: "TH", "SGC", "BOD"…</summary>
        [Required]
        [StringLength(100)]
        public string Area { get; set; } = string.Empty;

        [Required]
        [StringLength(400)]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Código del documento: PR-TH-1, MA-SGC-2…</summary>
        [StringLength(100)]
        public string? Codigo { get; set; }

        [StringLength(50)]
        public string? Version { get; set; }

        /// <summary>Fecha de la versión vigente del documento.</summary>
        public DateTime? Fecha { get; set; }

        /// <summary>"Si" | "No" — si existe copia controlada del documento.</summary>
        [StringLength(10)]
        public string CopiaControlada { get; set; } = "No";

        /// <summary>Dónde está la copia física / controlada.</summary>
        [StringLength(300)]
        public string? Ubicacion { get; set; }

        /// <summary>Un documento obsoleto se conserva pero no sale en la lista.</summary>
        public bool Obsoleto { get; set; }

        [StringLength(500)]
        public string? Observaciones { get; set; }

        [StringLength(200)]
        public string? CreadoPor { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public DateTime? ActualizadoEn { get; set; }
    }
}
