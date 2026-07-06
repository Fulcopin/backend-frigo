using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FormBuilder.API.Models
{
    public class CatalogoFirma
    {
        [Key]
        public int CatalogoFirmaID { get; set; }

        [Required]
        [StringLength(100)]
        public string Puesto { get; set; } = string.Empty;

        [StringLength(200)]
        public string? NombreCompleto { get; set; }

        [StringLength(100)]
        public string? Area { get; set; }

        [StringLength(150)]
        [EmailAddress]
        public string? Correo { get; set; }

        /// <summary>
        /// URL de la firma guardada en Cloudinary (persistente)
        /// </summary>
        [StringLength(500)]
        public string? FirmaImageUrl { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>
        /// PIN personal (hasheado SHA-256) para firmar desde la pantalla del operador.
        /// [JsonIgnore] garantiza que NUNCA se serialice en las respuestas de la API.
        /// </summary>
        [StringLength(200)]
        [JsonIgnore]
        public string? PinHash { get; set; }
    }
}
