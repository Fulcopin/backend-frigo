using System;
using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// 📢 Novedad publicada por un administrador para todo el personal:
    /// cambios de formato, avisos de planta, recordatorios.
    ///
    /// Al entrar al sistema, quien tenga novedades vigentes sin leer las ve
    /// ANTES de poder usar cualquier pantalla; al confirmar que las leyó, queda
    /// registrado en NovedadLectura y no le vuelven a aparecer.
    /// </summary>
    public class Novedad
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Titulo { get; set; } = "";

        /// <summary>Texto libre; se muestra respetando los saltos de línea.</summary>
        [Required]
        public string Mensaje { get; set; } = "";

        /// <summary>info | importante | urgente — solo cambia el color y el orden.</summary>
        [StringLength(20)]
        public string Importancia { get; set; } = "info";

        /// <summary>Archivada = false: ya no aparece a nadie, pero queda el historial.</summary>
        public bool Activa { get; set; } = true;

        /// <summary>
        /// 📺 Novedad GRANDE: se muestra como pantalla completa al entrar, antes
        /// de cualquier otra cosa. Si es false, solo aparece en el Inicio, en la
        /// sección Novedades y en el contador del menú.
        /// </summary>
        public bool MostrarAlEntrar { get; set; } = true;

        /// <summary>Desde qué día se muestra (null = desde que se publica).</summary>
        public DateTime? VisibleDesde { get; set; }

        /// <summary>Hasta qué día se muestra, inclusive (null = sin fecha de fin).</summary>
        public DateTime? VisibleHasta { get; set; }

        /// <summary>Imagen opcional de la novedad en formato data URL (base64).</summary>
        public string? ImagenBase64 { get; set; }

        [StringLength(200)]
        public string CreadoPor { get; set; } = "";

        [StringLength(200)]
        public string? CreadoPorNombre { get; set; }

        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Constancia de que un usuario leyó una novedad (una sola por usuario).
    /// </summary>
    public class NovedadLectura
    {
        [Key]
        public int Id { get; set; }

        public int NovedadId { get; set; }

        /// <summary>Usuario de la sesión (ej. Kpenafiel), sin distinguir mayúsculas.</summary>
        [Required]
        [StringLength(200)]
        public string Usuario { get; set; } = "";

        [StringLength(200)]
        public string? NombreUsuario { get; set; }

        public DateTime LeidaEn { get; set; } = DateTime.Now;
    }
}
