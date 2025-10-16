// Contenido para el nuevo archivo: Models/FilledFormDtos.cs

namespace FormBuilder.API.Models
{
    // Este DTO representa una fila de la tabla, tal como la envía el frontend
    public class TableRowInputDto
    {
        public string RowData { get; set; } = string.Empty;
    }

    // Este DTO representa el objeto COMPLETO que se recibe en la petición POST
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string HeaderData { get; set; } = string.Empty;
        public string FirmasData { get; set; } = string.Empty;
        public string? Observaciones { get; set; }

        // La colección usará el DTO de fila que definimos arriba
        public ICollection<TableRowInputDto> TableRows { get; set; } = new List<TableRowInputDto>();
    }
}