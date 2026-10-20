using System.ComponentModel.DataAnnotations;

namespace FormBuilder.API.Models
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        public string CreadoPorNombre { get; set; } = string.Empty;

        [Required]
        public string CreadoPorEmail { get; set; } = string.Empty;

        [Required]
        public DateTime CreadoEn { get; set; }

        // abierto | en_progreso | cerrado
        [Required]
        public string Estado { get; set; } = "abierto";

        public string? RespuestaAdmin { get; set; }

        public string? RespondidoPor { get; set; }

        public DateTime? RespondidoEn { get; set; }

        /// <summary>
        /// A quién le llega este mensaje. Vacío = a todos los administradores,
        /// como siempre. Se completa solo, según la regla del operario
        /// (tabla TicketRutas).
        /// </summary>
        [StringLength(200)]
        public string? AsignadoAEmail { get; set; }

        [StringLength(200)]
        public string? AsignadoANombre { get; set; }
    }

    /// <summary>
    /// 📬 Regla de reparto: los mensajes de un operario van a UNA persona.
    ///
    /// Sin regla, el mensaje queda a la vista de todos los administradores
    /// (comportamiento de siempre). Con regla, solo lo ve el destinatario,
    /// aunque un administrador puede seguir viendo todo si lo pide.
    /// </summary>
    public class TicketRuta
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string UsuarioEmail { get; set; } = string.Empty;

        [StringLength(200)]
        public string? UsuarioNombre { get; set; }

        [Required]
        [StringLength(200)]
        public string DestinatarioEmail { get; set; } = string.Empty;

        [StringLength(200)]
        public string? DestinatarioNombre { get; set; }

        public bool Activa { get; set; } = true;

        [StringLength(200)]
        public string? ActualizadoPor { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class TicketViewer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserEmail { get; set; } = string.Empty;

        [Required]
        public string UserNombre { get; set; } = string.Empty;

        [Required]
        public DateTime AgregadoEn { get; set; }
    }
}