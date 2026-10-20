using System;
using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// ✍️ Firma puesta con PIN en una CELDA de una tabla (columna tipo "firmaPin").
    ///
    /// La celda guarda un texto legible ("Firmado digitalmente por: …  Cód. X7K2P9QM")
    /// que sale igual en pantalla, PDF y Excel. Este registro es la constancia en
    /// el servidor: con el código se puede verificar quién firmó, cuándo y dónde.
    /// </summary>
    public class FirmaCelda
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Código de verificación único que se muestra en la celda.</summary>
        [Required]
        [StringLength(12)]
        public string Codigo { get; set; } = "";

        public int? CatalogoFirmaID { get; set; }

        [Required]
        [StringLength(200)]
        public string NombreCompleto { get; set; } = "";

        [StringLength(100)]
        public string? Puesto { get; set; }

        [StringLength(150)]
        public string? Correo { get; set; }

        /// <summary>true = se firmó con la imagen de la firma; false = solo el nombre.</summary>
        public bool ConImagen { get; set; }

        /// <summary>
        /// "pin": persona del catálogo de firmas, verificada con su PIN.
        /// "documento": persona SIN usuario; firma con nombre y cédula/pasaporte.
        /// No hay PIN que lo verifique: da fe quien tiene la sesión abierta
        /// (UsuarioSesion), que queda registrado.
        /// </summary>
        [StringLength(20)]
        public string TipoFirma { get; set; } = "pin";

        /// <summary>"cedula" o "pasaporte" (solo en firmas con documento).</summary>
        [StringLength(20)]
        public string? TipoDocumento { get; set; }

        [StringLength(20)]
        public string? NumeroDocumento { get; set; }

        [StringLength(500)]
        public string? FirmaImageUrl { get; set; }

        // ── Dónde se firmó (informativo) ──
        public int? FormId { get; set; }
        public int? TemplateId { get; set; }

        [StringLength(300)]
        public string? Campo { get; set; }

        public int? Fila { get; set; }

        /// <summary>Usuario con la sesión abierta en el equipo donde se firmó.</summary>
        [StringLength(200)]
        public string? UsuarioSesion { get; set; }

        [StringLength(64)]
        public string? Ip { get; set; }

        public DateTime FirmadoEn { get; set; } = DateTime.Now;

        // ── Si la firma se quitó de la celda ──
        public bool Anulada { get; set; }
        public DateTime? AnuladaEn { get; set; }

        [StringLength(200)]
        public string? AnuladaPor { get; set; }
    }
}