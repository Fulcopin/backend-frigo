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
        public DateTime? FechaVersion { get; set; } // Fecha de la versión
        public DateTime? VersionCreatedAt { get; set; } // ✅ NUEVO: Fecha de creación de esta versión
        public string? ChangeDescription { get; set; } // ✅ NUEVO: Descripción del cambio
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
        public Dictionary<string, object>? HeaderFieldsData { get; set; } // ⬅️ NUEVO: Datos de encabezado
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
        public DetailedChanges? DetailedChanges { get; set; } // ⬅️ NUEVO: Cambios detallados
    }

    // DTO para cambios detallados
    public class DetailedChanges
{
    public List<FieldChangeDto> HeaderFieldsChanges { get; set; } = new();
    public List<FieldChangeDto> BodyElementsChanges { get; set; } = new();
    public List<FieldChangeDto> SignaturesChanges { get; set; } = new(); // ✅ Para las firmas
    public List<string> MetadataChanges { get; set; } = new(); // Para Nombre, Proceso, Objetivo, etc.
    
    // Indica si hubo algún cambio en cualquier parte
    public bool HasChanges => HeaderFieldsChanges.Any() || 
                             BodyElementsChanges.Any() || 
                             SignaturesChanges.Any() || 
                             MetadataChanges.Any();
}

    // DTO para un cambio específico en un campo
    public class FieldChangeDto
    {
        public string ChangeType { get; set; } = string.Empty; // "added", "removed", "modified"
        public string FieldName { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string Description { get; set; } = string.Empty; // Descripción legible del cambio
    }
}
