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
