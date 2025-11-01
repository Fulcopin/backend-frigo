// Contenido para: Models/FilledFormDtos.cs

namespace FormBuilder.API.Models
{
    // ANTERIOR: Este DTO ya no es necesario.
    // public class TableRowInputDto
    // {
    //     public string RowData { get; set; } = string.Empty;
    // }

    // DTO actualizado para la petición POST de un nuevo formulario
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string HeaderData { get; set; } = string.Empty;
        
        // NUEVO: Recibimos los datos del cuerpo como un solo string JSON.
        public string BodyData { get; set; } = string.Empty;

        public string FirmasData { get; set; } = string.Empty;
        public string? Observaciones { get; set; }

        // ANTERIOR: La colección de filas se elimina.
        // public ICollection<TableRowInputDto> TableRows { get; set; } = new List<TableRowInputDto>();
    }
}