using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Excepción de alertas para UNA plantilla.
    ///
    /// AlertConfiguration es una sola fila para todo el sistema: 24 horas de
    /// plazo de firma para los 57 formularios por igual. Pero un control de
    /// temperatura de cámara no puede esperar lo mismo que una lista de empaque,
    /// y hay formularios que directamente no deberían alertar.
    ///
    /// Esta tabla guarda solo las plantillas que se apartan del criterio
    /// general. La que no tiene fila acá sigue con la configuración global, así
    /// que cambiar el default sigue afectando a todas las demás.
    /// </summary>
    [Table("AlertConfigTemplates")]
    public class AlertConfigTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TemplateID { get; set; }

        /// <summary>Apaga las alertas de firma pendiente solo para esta plantilla.</summary>
        public bool EnableSignatureAlerts { get; set; } = true;

        /// <summary>Horas de espera antes de avisar. Reemplaza al global.</summary>
        public int SignatureAlertDelay { get; set; } = 24;

        /// <summary>Hora del chequeo diario ("18:00"). Vacío = usa la global.</summary>
        [StringLength(5)]
        public string? DailyCheckTime { get; set; }

        /// <summary>
        /// Destinatarios propios, en JSON. Vacío = usa los globales.
        /// Sirve para que el aviso de un formulario de cámara vaya al jefe de
        /// cámara y no a toda la lista de SGI.
        /// </summary>
        public string? SignatureRecipients { get; set; }

        /// <summary>Se suman a los globales en vez de reemplazarlos.</summary>
        public bool RecipientsSeSuman { get; set; } = true;

        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreadoEn { get; set; } = DateTime.Now;
        public DateTime ActualizadoEn { get; set; } = DateTime.Now;

        // Sin propiedad de navegación a Template a propósito.
        //
        // EF la tomaba como relación y, al no haber colección inversa ni
        // configuración explícita, fallaba al construir el modelo. Cuando eso
        // pasa revientan TODAS las consultas del contexto, no solo las de esta
        // tabla: por eso /api/Alerts/config también devolvía 500 aunque no se
        // hubiera tocado.
        //
        // No hace falta: el controlador junta los datos con un join manual, y
        // la integridad la garantiza la llave foránea de la base.
    }

    /// <summary>Lo que manda la pantalla para aplicar a VARIAS plantillas de una vez.</summary>
    public class AlertConfigLoteDto
    {
        /// <summary>Plantillas a las que se aplica lo mismo.</summary>
        public List<int> TemplateIds { get; set; } = new();

        public bool EnableSignatureAlerts { get; set; } = true;
        public int SignatureAlertDelay { get; set; } = 24;
        public string? DailyCheckTime { get; set; }
        public List<string>? SignatureRecipients { get; set; }
        public bool RecipientsSeSuman { get; set; } = true;
        public string? ActualizadoPor { get; set; }
    }
}