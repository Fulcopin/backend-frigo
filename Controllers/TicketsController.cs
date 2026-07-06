using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(ApplicationDbContext context, IEmailService emailService, ILogger<TicketsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // GET /api/Tickets
        // Devuelve todos los tickets; el filtrado "míos" vs "todos" se hace en el frontend,
        // igual que el resto de los módulos de esta app (no hay autorización a nivel de servidor).
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Ticket>>> GetTickets()
        {
            try
            {
                var tickets = await _context.Tickets
                    .OrderByDescending(t => t.CreadoEn)
                    .ToListAsync();

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tickets");
                return StatusCode(500, new { message = "Error al obtener tickets" });
            }
        }

        // POST /api/Tickets
        [HttpPost]
        public async Task<ActionResult<Ticket>> CreateTicket([FromBody] Ticket ticket)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ticket.Titulo) || string.IsNullOrWhiteSpace(ticket.Descripcion))
                {
                    return BadRequest(new { message = "Título y descripción son requeridos" });
                }

                ticket.Id = 0;
                ticket.CreadoEn = DateTime.Now;
                ticket.Estado = "abierto";
                ticket.RespuestaAdmin = null;
                ticket.RespondidoPor = null;
                ticket.RespondidoEn = null;

                _context.Tickets.Add(ticket);
                await _context.SaveChangesAsync();

                await NotifyViewersAsync(ticket);

                return Ok(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear ticket");
                return StatusCode(500, new { message = "Error al crear ticket" });
            }
        }

        // PUT /api/Tickets/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateTicket(int id, [FromBody] Ticket update)
        {
            try
            {
                var ticket = await _context.Tickets.FindAsync(id);
                if (ticket == null) return NotFound();

                ticket.Estado = update.Estado;
                ticket.RespuestaAdmin = update.RespuestaAdmin;
                if (!string.IsNullOrWhiteSpace(update.RespuestaAdmin))
                {
                    ticket.RespondidoPor = update.RespondidoPor;
                    ticket.RespondidoEn = DateTime.Now;
                }

                await _context.SaveChangesAsync();
                return Ok(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar ticket {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar ticket" });
            }
        }

        // GET /api/Tickets/viewers
        [HttpGet("viewers")]
        public async Task<ActionResult<IEnumerable<TicketViewer>>> GetViewers()
        {
            try
            {
                var viewers = await _context.TicketViewers.OrderBy(v => v.UserNombre).ToListAsync();
                return Ok(viewers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener viewers de tickets");
                return StatusCode(500, new { message = "Error al obtener la lista de acceso" });
            }
        }

        // PUT /api/Tickets/viewers
        // Reemplaza la lista completa de usuarios con acceso a "Todos los Tickets".
        [HttpPut("viewers")]
        public async Task<ActionResult> ReplaceViewers([FromBody] List<TicketViewer> viewers)
        {
            try
            {
                var existing = await _context.TicketViewers.ToListAsync();
                _context.TicketViewers.RemoveRange(existing);

                foreach (var v in viewers)
                {
                    _context.TicketViewers.Add(new TicketViewer
                    {
                        UserEmail = v.UserEmail,
                        UserNombre = v.UserNombre,
                        AgregadoEn = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar viewers de tickets");
                return StatusCode(500, new { message = "Error al actualizar la lista de acceso" });
            }
        }

        private async Task NotifyViewersAsync(Ticket ticket)
        {
            try
            {
                var viewerEmails = await _context.TicketViewers.Select(v => v.UserEmail).ToListAsync();
                var config = await _context.AlertConfigurations.FirstOrDefaultAsync();

                var recipients = viewerEmails
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Si todavía no hay viewers configurados, se notifica al correo remitente/admin por defecto.
                if (recipients.Count == 0 && !string.IsNullOrWhiteSpace(config?.SenderEmail))
                {
                    recipients.Add(config.SenderEmail);
                }

                var subject = $"🎫 Nuevo ticket: {ticket.Titulo}";
                var body = $@"
                    <h2>🎫 Nuevo ticket reportado</h2>
                    <p><strong>Título:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Titulo)}</p>
                    <p><strong>Descripción:</strong> {System.Net.WebUtility.HtmlEncode(ticket.Descripcion)}</p>
                    <p><strong>Creado por:</strong> {System.Net.WebUtility.HtmlEncode(ticket.CreadoPorNombre)} ({System.Net.WebUtility.HtmlEncode(ticket.CreadoPorEmail)})</p>
                    <p><strong>Fecha:</strong> {ticket.CreadoEn:dd/MM/yyyy HH:mm}</p>";

                foreach (var email in recipients)
                {
                    await _emailService.SendAlertEmailAsync(email, subject, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al notificar el nuevo ticket {Id}", ticket.Id);
            }
        }
    }
}
