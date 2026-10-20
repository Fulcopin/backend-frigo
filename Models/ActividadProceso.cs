using System;
using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// 🏭 Actividad de proceso (Fileteo, Descongelar, Pelar, Tumbado…).
    ///
    /// Antes estaban escritas dentro del código del sistema: agregar una
    /// actividad obligaba a publicar una versión nueva. Ahora viven acá y se
    /// administran desde la pantalla.
    ///
    /// Una actividad puede estar en los dos catálogos (Cambio de Etiquetas se
    /// usa en pescado y en camarón).
    /// </summary>
    public class ActividadProceso
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Nombre { get; set; } = "";

        public bool Pescado { get; set; } = true;
        public bool Camaron { get; set; }

        /// <summary>
        /// Inactiva = deja de ofrecerse al llenar y al planificar, pero los
        /// formularios que ya la usaron se siguen leyendo igual.
        /// </summary>
        public bool Activa { get; set; } = true;

        public int Orden { get; set; }

        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
