using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Una consulta fija del Comparativo Plan vs Producción: "para la actividad
    /// Fileteo del bloque PESCADO, la columna PROD·Libras se llena con el total
    /// de Peso Neto del PD-04".
    ///
    /// Antes esto vivía en el navegador de cada persona (localStorage): lo que
    /// configuraba uno no lo veía nadie más y se perdía al cambiar de
    /// computadora. Guardado acá es del sistema: se configura una vez y lo usa
    /// toda la planta, cualquier día y cualquier plan.
    /// </summary>
    public class ConsultaPlan
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Actividad a la que aplica ("Fileteo"). Cadena vacía = consulta
        /// GENÉRICA de la columna: la usan las filas cuya actividad no tiene
        /// una propia.
        /// </summary>
        [StringLength(200)]
        public string Actividad { get; set; } = "";

        /// <summary>
        /// Bloque de la matriz: "PESCADO" o "CAMARON". Vacío = sin bloque (lo
        /// configurado antes de que la matriz se separara en dos, y las filas
        /// de pestañas que no declaran especie). «Empaque» de camarón no sale
        /// del mismo formulario que el de pescado: por eso es parte de la clave.
        /// </summary>
        [StringLength(20)]
        public string Grupo { get; set; } = "";

        /// <summary>Columna que se calcula, como "seccion.campo" (ej. "prod.libras").</summary>
        [StringLength(60)]
        public string Clave { get; set; } = "";

        /// <summary>
        /// La receta en JSON, tal cual la arma la calculadora:
        /// { terminos: [...], decimales, actividad, grupo }.
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string Receta { get; set; } = "{}";

        // ── Auditoría: quién dejó configurada la consulta y cuándo ───────────
        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
