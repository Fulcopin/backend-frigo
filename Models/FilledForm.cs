using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class FilledForm
    {
        [Key]
        public int FormID { get; set; }

        public int TemplateID { get; set; }
        [ForeignKey("TemplateID")]
        public virtual Template Template { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? HeaderData { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? FirmasData { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? Observaciones { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<TableRow> TableRows { get; set; }
    }
}