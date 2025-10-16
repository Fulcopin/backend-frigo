using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class TableRow
    {
        [Key]
        public int RowID { get; set; }

        public int FormID { get; set; }
        [ForeignKey("FormID")]
        public virtual FilledForm FilledForm { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? RowData { get; set; }
    }
}