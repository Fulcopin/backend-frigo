using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    [Table("TemplateChangeLogs")]
    public class TemplateChangeLog
    {
        [Key]
        public int Id { get; set; }

        public int TemplateID { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        [Required]
        [MaxLength(50)]
        public string Version { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string CambioRealizado { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("TemplateID")]
        public virtual Template? Template { get; set; }
    }
}
