using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Cómo se arma el reporte de "Descargar Datos" para un formulario.
    ///
    /// Cada plantilla necesita su propio armado: en un PD-04 interesan las libras y el
    /// personal, en un control de termómetros interesan las temperaturas. Antes esto vivía
    /// en el navegador de cada persona (localStorage), así que se perdía al cambiar de
    /// computadora y no lo veían los demás. Guardado acá es del sistema: se arma una vez
    /// y lo usa toda la planta.
    /// </summary>
    public class ConfiguracionReporte
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Plantilla a la que aplica. NULL = la vista de "todos los formularios".</summary>
        public int? TemplateID { get; set; }

        /// <summary>Nombre para reconocerla en la lista (ej. "Resumen mano de obra").</summary>
        public string Nombre { get; set; } = "Resumen";

        /// <summary>Columnas marcadas, como array JSON de nombres.</summary>
        public string ColumnasVisibles { get; set; } = "[]";

        /// <summary>
        /// Qué hace cada columna en el total, como objeto JSON:
        /// { "PESO NETO": "suma", "TEMPERATURA": "promedio", "LOTE": "no" }
        /// </summary>
        public string Operaciones { get; set; } = "{}";

        // ── Interruptores de la vista ────────────────────────────────────────
        /// <summary>Cada formulario se colapsa en una sola línea con sus totales.</summary>
        public bool UnaLineaPorForm { get; set; } = false;
        /// <summary>Deja fuera las columnas que no tienen ni un dato.</summary>
        public bool OcultarVacias { get; set; } = true;
        /// <summary>Une por posición las tablas de un mismo formulario.</summary>
        public bool CompactarFilas { get; set; } = true;
        /// <summary>Descarta las líneas de TOTAL que trae el propio formulario.</summary>
        public bool OmitirTotalesDelForm { get; set; } = false;
        /// <summary>Corta con un subtotal cada vez que cambia de formulario.</summary>
        public bool SubtotalPorForm { get; set; } = true;
        /// <summary>Muestra solo los totales en vez de fila por fila.</summary>
        public bool ModoResumen { get; set; } = false;
        /// <summary>Columna por la que agrupa el modo resumen ("" = total general).</summary>
        public string AgruparPor { get; set; } = "";

        // ── Auditoría ────────────────────────────────────────────────────────
        public string? ActualizadoPor { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
