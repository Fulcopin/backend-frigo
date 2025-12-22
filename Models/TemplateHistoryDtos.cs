namespace FormBuilder.API.Models
{
    // DTO para mostrar el historial de versiones de un template
    public class TemplateVersionHistoryDto
    {
        public string Version { get; set; } = string.Empty;
        public DateTime? FirstUsedDate { get; set; }
        public DateTime? LastUsedDate { get; set; }
        public int FormCount { get; set; } // Cantidad de formularios con esta versión
        public bool IsCurrentVersion { get; set; } // True si es la versión actual del template
    }

    // DTO para los detalles de una versión específica
    public class TemplateVersionDetailDto
    {
        public string Version { get; set; } = string.Empty;
        public int TemplateID { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Objetivo { get; set; }
        public string? Proceso { get; set; }
        public string? HeaderFields { get; set; }
        public string? BodyElements { get; set; }
        public string? Firmas { get; set; }
        public List<FormSummaryDto> AssociatedForms { get; set; } = new();
    }

    // DTO resumido de un formulario
    public class FormSummaryDto
    {
        public int FormID { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? HeaderData { get; set; }
        public string? Observaciones { get; set; }
    }

    // DTO para comparar dos versiones
    public class VersionComparisonDto
    {
        public string OldVersion { get; set; } = string.Empty;
        public string NewVersion { get; set; } = string.Empty;
        public List<string> Changes { get; set; } = new();
        public DateTime? ComparisonDate { get; set; }
    }
}
