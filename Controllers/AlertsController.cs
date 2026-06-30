using System.Net;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AlertsController> _logger;

        public AlertsController(
            ApplicationDbContext context,
            IEmailService emailService,
            ILogger<AlertsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // GET /api/Alerts/active
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<Alert>>> GetActiveAlerts()
        {
            try
            {
                var alerts = await _context.Alerts
                    .Where(a => !a.IsRead)
                    .OrderByDescending(a => a.CreatedDate)
                    .ToListAsync();

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener alertas activas");
                return StatusCode(500, new { message = "Error al obtener alertas" });
            }
        }

        // GET /api/Alerts/config
        [HttpGet("config")]
        public async Task<ActionResult<AlertConfiguration>> GetConfiguration()
        {
            try
            {
                var config = await _context.AlertConfigurations.FirstOrDefaultAsync();
                
                // Si no existe configuraciÃ³n, crear una por defecto
                if (config == null)
                {
                    config = new AlertConfiguration();
                    _context.AlertConfigurations.Add(config);
                    await _context.SaveChangesAsync();
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuraciÃ³n");
                return StatusCode(500, new { message = "Error al obtener configuraciÃ³n" });
            }
        }

        // PUT /api/Alerts/config
        [HttpPut("config")]
        public async Task<ActionResult> UpdateConfiguration([FromBody] AlertConfiguration config)
        {
            try
            {
                var existingConfig = await _context.AlertConfigurations.FirstOrDefaultAsync();
                
                if (existingConfig == null)
                {
                    _context.AlertConfigurations.Add(config);
                }
                else
                {
                    existingConfig.EnableMissingFormAlerts = config.EnableMissingFormAlerts;
                    existingConfig.DailyCheckTime = config.DailyCheckTime;
                    existingConfig.MissingFormRecipients = config.MissingFormRecipients;
                    existingConfig.EnableSignatureAlerts = config.EnableSignatureAlerts;
                    existingConfig.SignatureAlertDelay = config.SignatureAlertDelay;
                    existingConfig.SignatureRecipients = config.SignatureRecipients;
                    existingConfig.SummaryFrequencyDays = config.SummaryFrequencyDays;
                    existingConfig.SenderEmail = config.SenderEmail;
                    existingConfig.SenderName = config.SenderName;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "ConfiguraciÃ³n actualizada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuraciÃ³n");
                return StatusCode(500, new { message = "Error al actualizar configuraciÃ³n" });
            }
        }

        // PUT /api/Alerts/mark-read/{alertId}
        [HttpPut("mark-read/{alertId}")]
        public async Task<ActionResult> MarkAsRead(int alertId)
        {
            try
            {
                var alert = await _context.Alerts.FindAsync(alertId);
                if (alert == null)
                {
                    return NotFound(new { message = "Alerta no encontrada" });
                }

                alert.IsRead = true;
                alert.ReadDate = DateTime.Now;
                alert.Status = "read";

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Alerta marcada como leÃ­da"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar alerta como leÃ­da");
                return StatusCode(500, new { message = "Error al marcar alerta" });
            }
        }

        // POST /api/Alerts/test
        [HttpPost("test")]
        public async Task<ActionResult> SendTestEmail([FromBody] SendTestEmailRequest request)
        {
            try
            {
                var result = await _emailService.SendTestEmailAsync(request.Email);

                if (result)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "Email de prueba enviado exitosamente"
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "Error al enviar email de prueba"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar email de prueba");
                return StatusCode(500, new { message = "Error al enviar email de prueba" });
            }
        }

        // POST /api/Alerts/trigger-summary
        [HttpPost("trigger-summary")]
        public async Task<ActionResult> TriggerConsolidatedSummary()
        {
            try
            {
                await AlertBackgroundService.SendDailyConsolidatedAlertsAsync(_context, _emailService, _logger);
                return Ok(new
                {
                    success = true,
                    message = "Resumen consolidado de firmas pendientes y formularios faltantes enviado exitosamente a todos los usuarios y jefes de área."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al disparar resumen consolidado");
                return StatusCode(500, new { message = "Error al disparar resumen consolidado" });
            }
        }

        // GET /api/Alerts/history
        [HttpGet("history")]
        public async Task<ActionResult<IEnumerable<Alert>>> GetHistory(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? type,
            [FromQuery] string? status)
        {
            try
            {
                var query = _context.Alerts.AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(a => a.CreatedDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(a => a.CreatedDate <= endDate.Value);
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(a => a.Type == type);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(a => a.Status == status);
                }

                var alerts = await query
                    .OrderByDescending(a => a.CreatedDate)
                    .ToListAsync();

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de alertas");
                return StatusCode(500, new { message = "Error al obtener historial" });
            }
        }

        // GET /api/Alerts/stats
        [HttpGet("stats")]
        public async Task<ActionResult<AlertStatsResponse>> GetStats()
        {
            try
            {
                var today = DateTime.Today;

                var stats = new AlertStatsResponse
                {
                    ActiveCount = await _context.Alerts
                        .Where(a => !a.IsRead)
                        .CountAsync(),

                    SentToday = await _context.Alerts
                        .Where(a => a.CreatedDate.Date == today && a.Status == "sent")
                        .CountAsync(),

                    TotalSent = await _context.Alerts
                        .Where(a => a.Status == "sent" || a.Status == "read")
                        .CountAsync(),

                    FailedCount = await _context.Alerts
                        .Where(a => a.Status == "failed")
                        .CountAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadÃ­sticas");
                return StatusCode(500, new { message = "Error al obtener estadÃ­sticas" });
            }
        }

        // POST /api/Alerts/manual
        [HttpPost("manual")]
        public async Task<ActionResult> CreateManualAlert([FromBody] CreateManualAlertRequest request)
        {
            try
            {
                var alert = new Alert
                {
                    Type = request.Type,
                    Priority = request.Priority,
                    Title = request.Title,
                    Message = request.Message,
                    TargetEmail = request.TargetEmail,
                    CreatedDate = DateTime.Now,
                    Status = "pending"
                };

                _context.Alerts.Add(alert);
                await _context.SaveChangesAsync();

                // Enviar email
                var emailSent = await _emailService.SendAlertEmailAsync(
                    request.TargetEmail,
                    request.Title,
                    request.Message
                );

                alert.Status = emailSent ? "sent" : "failed";
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Alerta creada y enviada exitosamente",
                    alertId = alert.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear alerta manual");
                return StatusCode(500, new { message = "Error al crear alerta" });
            }
        }

        // POST /api/Alerts/send-form
        // Enviar datos de formulario llenado por correo electronico
        [HttpPost("send-form")]
        public async Task<ActionResult> SendFormByEmail([FromBody] SendFormEmailRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Email))
                    return BadRequest(new { success = false, message = "El email es requerido" });

                // Construir HTML del formulario
                var html = BuildFormEmailHtml(request);

                var subject = $" {request.FormCode} - {request.FormName} | Frigolab";
                var result = await _emailService.SendAlertEmailAsync(request.Email, subject, html);

                if (result)
                {
                    return Ok(new { success = true, message = $"Formulario enviado exitosamente a {request.Email}" });
                }
                else
                {
                    return StatusCode(500, new { success = false, message = "Error al enviar el correo" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar formulario por email");
                return StatusCode(500, new { message = "Error al enviar formulario por email" });
            }
        }

        private string BuildFormEmailHtml(SendFormEmailRequest request)
        {
            var html = $@"
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
        .container {{ max-width: 800px; margin: 0 auto; background: white; border-radius: 12px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); overflow: hidden; }}
        .header {{ background: linear-gradient(135deg, #1e40af, #3b82f6); color: white; padding: 25px 30px; }}
        .header h1 {{ margin: 0 0 5px 0; font-size: 22px; }}
        .header .code {{ opacity: 0.85; font-size: 14px; }}
        .header .date {{ opacity: 0.7; font-size: 12px; margin-top: 8px; }}
        .content {{ padding: 25px 30px; }}
        .section {{ margin-bottom: 20px; }}
        .section h3 {{ color: #1e40af; border-bottom: 2px solid #e5e7eb; padding-bottom: 8px; margin-bottom: 12px; font-size: 16px; }}
        .data-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }}
        .data-item {{ padding: 6px 0; }}
        .data-item .label {{ font-weight: 600; color: #374151; font-size: 13px; }}
        .data-item .value {{ color: #6b7280; font-size: 13px; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 10px; font-size: 12px; }}
        th {{ background: #1e40af; color: white; padding: 8px 10px; text-align: left; }}
        td {{ border: 1px solid #e5e7eb; padding: 6px 10px; }}
        tr:nth-child(even) {{ background: #f9fafb; }}
        .obs-box {{ background: #fffbeb; border: 1px solid #fcd34d; border-radius: 8px; padding: 12px; margin-top: 8px; }}
        .firma-grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; }}
        .firma-box {{ border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px; text-align: center; }}
        .firma-box h4 {{ color: #1e40af; margin: 0 0 8px 0; font-size: 14px; }}
        .firma-box p {{ margin: 3px 0; font-size: 12px; color: #6b7280; }}
        .footer {{ background: #f9fafb; padding: 15px 30px; text-align: center; color: #9ca3af; font-size: 11px; border-top: 1px solid #e5e7eb; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1> {System.Net.WebUtility.HtmlEncode(request.FormName)}</h1>
            <div class='code'>Codigo: {System.Net.WebUtility.HtmlEncode(request.FormCode)}</div>
            <div class='date'>Fecha: {request.CreatedAt}</div>
        </div>
        <div class='content'>";

            // Header Data
            if (request.HeaderData != null && request.HeaderData.Count > 0)
            {
                html += "<div class='section'><h3> Informacion General</h3><div class='data-grid'>";
                foreach (var item in request.HeaderData)
                {
                    html += $"<div class='data-item'><span class='label'>{System.Net.WebUtility.HtmlEncode(item.Key)}:</span> <span class='value'>{System.Net.WebUtility.HtmlEncode(item.Value?.ToString() ?? "-")}</span></div>";
                }
                html += "</div></div>";
            }

            // Body Data (tables/sections)
            if (request.BodySections != null)
            {
                foreach (var section in request.BodySections)
                {
                    html += $"<div class='section'><h3> {System.Net.WebUtility.HtmlEncode(section.Title)}</h3>";

                    if (section.Type == "table" && section.Columns != null && section.Rows != null)
                    {
                        html += "<table><thead><tr><th>#</th>";
                        foreach (var col in section.Columns)
                        {
                            html += $"<th>{System.Net.WebUtility.HtmlEncode(col)}</th>";
                        }
                        html += "</tr></thead><tbody>";

                        for (int i = 0; i < section.Rows.Count; i++)
                        {
                            html += $"<tr><td>{i + 1}</td>";
                            var row = section.Rows[i];
                            foreach (var col in section.Columns)
                            {
                                var val = row.ContainsKey(col) ? row[col]?.ToString() ?? "-" : "-";
                                html += $"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>";
                            }
                            html += "</tr>";
                        }
                        html += "</tbody></table>";
                    }
                    else if (section.Data != null)
                    {
                        html += "<div class='data-grid'>";
                        foreach (var item in section.Data)
                        {
                            html += $"<div class='data-item'><span class='label'>{System.Net.WebUtility.HtmlEncode(item.Key)}:</span> <span class='value'>{System.Net.WebUtility.HtmlEncode(item.Value?.ToString() ?? "-")}</span></div>";
                        }
                        html += "</div>";
                    }
                    html += "</div>";
                }
            }

            // Observaciones
            if (!string.IsNullOrEmpty(request.Observaciones))
            {
                html += $"<div class='section'><h3> Observaciones</h3><div class='obs-box'>{System.Net.WebUtility.HtmlEncode(request.Observaciones)}</div></div>";
            }

            // Firmas
            if (request.Firmas != null && request.Firmas.Count > 0)
            {
                html += "<div class='section'><h3> Firmas</h3><div class='firma-grid'>";
                foreach (var firma in request.Firmas)
                {
                    html += $"<div class='firma-box'><h4>{System.Net.WebUtility.HtmlEncode(firma.Puesto)}</h4><p><strong>Nombre:</strong> {System.Net.WebUtility.HtmlEncode(firma.Nombre ?? "-")}</p><p><strong>Fecha:</strong> {System.Net.WebUtility.HtmlEncode(firma.Fecha ?? "-")}</p><p>{(firma.TieneFirma ? " Firmado" : " Pendiente")}</p></div>";
                }
                html += "</div></div>";
            }

            html += @"
        </div>
        <div class='footer'>
            <p>Este correo fue enviado desde el Sistema de Gestion de Frigolab</p>
            <p> Enviado automaticamente - No responder a este correo</p>
        </div>
    </div>
</body>
</html>";

            return html;
        }
    }
}

