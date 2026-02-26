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
        
        // 🦐🐟 NUEVO: Tipo de producto (Camarón o Pescado)
        public string? TipoProducto { get; set; }
        
        // ✅ AUDITORÍA: Información del usuario que llenó el formulario
        public string? FilledBy { get; set; }
        public string? FilledByEmail { get; set; }
        public string? FilledByRole { get; set; }
        
        public string? Observaciones { get; set; }
    }
}