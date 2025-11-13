// Contenido para: Models/FilledFormDtos.cs

namespace FormBuilder.API.Models
{
    // DTO actualizado para la petición POST de un nuevo formulario
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string HeaderData { get; set; } = string.Empty;
        
        // NUEVO: Recibimos los datos del cuerpo como un solo string JSON.
        public string BodyData { get; set; } = string.Empty;

        public string FirmasData { get; set; } = string.Empty;
        public string? Observaciones { get; set; }
    }
}