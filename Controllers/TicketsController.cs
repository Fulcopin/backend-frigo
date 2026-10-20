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
        /// <summary>
        /// Regla general: a quién van los mensajes de quien no tiene una regla
        /// propia. Se guarda como una fila más de TicketRutas, con este valor
        /// en lugar de un correo.
        /// </summary>
        public const string DESTINATARIO_POR_DEFECTO = "*";

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
        /// <summary>
        /// Mensajes. Con ?para=correo devuelve SOLO los de esa persona: los que
        /// se le asignaron y los que no tienen destinatario (van a todos).
        /// Sin el parámetro, o con todos=true, devuelve todo como siempre.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Ticket>>> GetTickets(
            [FromQuery] string? para, [FromQuery] bool todos = false)
        {
            try
            {
                var consulta = _context.Tickets.AsQueryable();

                var destinatario = (para ?? "").Trim().ToLower();
                if (!todos && destinatario.Length > 0)
                {
                    consulta = consulta.Where(t =>
                        t.AsignadoAEmail == null || t.AsignadoAEmail == ""
                        || t.AsignadoAEmail.ToLower() == destinatario);
                }

                var tickets = await consulta
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

                // 📬 Si el operario tiene una regla, el mensaje va a esa persona
                //    y no queda a la vista de todos los administradores.
                var correo = (ticket.CreadoPorEmail ?? "").Trim().ToLower();

                // Primero la regla de esa persona; si no tiene, la regla general
                // (DESTINATARIO_POR_DEFECTO). Así los mensajes llegan a UNA sola
                // persona y el aviso rojo no le aparece a todos los administradores.
                var ruta = correo.Length > 0
                    ? await _context.TicketRutas.FirstOrDefaultAsync(r => r.Activa && r.UsuarioEmail.ToLower() == correo)
                    : null;

                ruta ??= await _context.TicketRutas
                    .FirstOrDefaultAsync(r => r.Activa && r.UsuarioEmail == DESTINATARIO_POR_DEFECTO);

                if (ruta != null)
                {
                    ticket.AsignadoAEmail = ruta.DestinatarioEmail;
                    ticket.AsignadoANombre = ruta.DestinatarioNombre;
                    _logger.LogInformation("📬 Mensaje de {De} asignado a {Para}", correo, ruta.DestinatarioEmail);
                }
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

        // ── 📬 Reglas de reparto ────────────────────────────────────────

        /// <summary>Reglas: de quién son los mensajes y a quién le llegan.</summary>
        [HttpGet("rutas")]
        public async Task<ActionResult<IEnumerable<TicketRuta>>> GetRutas()
        {
            var rutas = await _context.TicketRutas.AsNoTracking()
                .OrderBy(r => r.UsuarioNombre).ThenBy(r => r.UsuarioEmail)
                .ToListAsync();
            return Ok(rutas);
        }

        /// <summary>
        /// Crea o cambia la regla de un operario. Si el destinatario viene
        /// vacío, se quita la regla y sus mensajes vuelven a ir a todos.
        /// </summary>
        [HttpPut("rutas")]
        public async Task<ActionResult> GuardarRuta([FromBody] TicketRuta ruta)
        {
            var usuario = (ruta?.UsuarioEmail ?? "").Trim();
            if (usuario.Length == 0)
                return BadRequest(new { message = "Falta el correo del operario." });

            var existente = await _context.TicketRutas
                .FirstOrDefaultAsync(r => r.UsuarioEmail.ToLower() == usuario.ToLower());

            var destinatario = (ruta!.DestinatarioEmail ?? "").Trim();
            if (destinatario.Length == 0)
            {
                if (existente != null)
                {
                    _context.TicketRutas.Remove(existente);
                    await _context.SaveChangesAsync();
                }
                return Ok(new { message = $"Los mensajes de {usuario} vuelven a ir a todos los administradores." });
            }

            if (existente == null)
            {
                _context.TicketRutas.Add(new TicketRuta
                {
                    UsuarioEmail = usuario,
                    UsuarioNombre = ruta.UsuarioNombre,
                    DestinatarioEmail = destinatario,
                    DestinatarioNombre = ruta.DestinatarioNombre,
                    Activa = true,
                    ActualizadoPor = ruta.ActualizadoPor,
                });
            }
            else
            {
                existente.UsuarioNombre = ruta.UsuarioNombre;
                existente.DestinatarioEmail = destinatario;
                existente.DestinatarioNombre = ruta.DestinatarioNombre;
                existente.Activa = true;
                existente.ActualizadoPor = ruta.ActualizadoPor;
                existente.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Los mensajes de {usuario} le llegarán a {destinatario}." });
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