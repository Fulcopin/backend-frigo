using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class Template
    {
        [Key]
        public int TemplateID { get; set; }

        [Required]
        [StringLength(50)]
        public string Codigo { get; set; }

        [Required]
        [StringLength(255)]
        public string Nombre { get; set; }

        [StringLength(20)]
        public string Version { get; set; } = "1";

        public string? Objetivo { get; set; }
        public string? Proceso { get; set; }
        public string? CuandoSeUsa { get; set; }
        public string? QuienLoLlena { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderFields { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? TableColumns { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Firmas { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}