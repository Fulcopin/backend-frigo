using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FormBuilder.API.Services
{
    public class AlertBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AlertBackgroundService> _logger;

        public AlertBackgroundService(IServiceProvider serviceProvider, ILogger<AlertBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AlertBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                        await CheckMissingFormsAsync(context, emailService);
                        await CheckPendingSignaturesAsync(context, emailService);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en AlertBackgroundService");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private List<string> ParseRecipients(string? recipientsJson)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(recipientsJson)) return list;

            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(recipientsJson);
                if (parsed != null) list = parsed;
            }
            catch
            {
                // No es JSON, tratar como email simple o separado por comas
                foreach (var e in recipientsJson.Split(',', ';'))
                {
                    var trimmed = e.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
                }
            }

            return list.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
        }

        private async Task CheckMissingFormsAsync(ApplicationDbContext context, IEmailService emailService)
        {
            try
            {
                var config = await context.AlertConfigurations.FirstOrDefaultAsync();
                if (config == null || !config.EnableMissingFormAlerts) return;

                var now = DateTime.Now;
                var checkTime = config.DailyCheckTime ?? "18:00";
                if (now.ToString("HH:mm") != checkTime) return;

                var today = DateTime.Today;
                var templates = await context.Templates
                    .Where(t => t.Frecuencia == "Diaria")
                    .ToListAsync();

                foreach (var template in templates)
                {
                    var hasFormToday = await context.FilledForms
                        .AnyAsync(f => f.TemplateID == template.TemplateID && f.CreatedAt.Date == today);

                    if (!hasFormToday)
                    {
                        var recipients = ParseRecipients(config.MissingFormRecipients);

                        foreach (var recipientEmail in recipients)
                        {
                            var alert = new Models.Alert
                            {
                                Type = "missing_form",
                                Priority = "high",
                                Title = $"Formulario No Llenado: {template.Nombre}",
                                Message = $"El formulario '{template.Nombre}' del {today:dd/MM/yyyy} no ha sido completado",
                                TargetEmail = recipientEmail,
                                CreatedDate = DateTime.Now,
                                Status = "pending"
                            };

                            context.Alerts.Add(alert);
                            await context.SaveChangesAsync();

                            var sent = await emailService.SendAlertEmailAsync(recipientEmail, alert.Title, alert.Message);
                            alert.Status = sent ? "sent" : "failed";
                            await context.SaveChangesAsync();
                        }
                    }
                }

                _logger.LogInformation("CheckMissingFormsAsync ejecutado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckMissingFormsAsync");
            }
        }

        /// <summary>
        /// Verifica si TODAS las firmas en FirmasData ya están completadas (tienen imagen firma.url o firma.base64).
        /// </summary>
        private static bool AllFirmasCompleted(string? firmasData)
        {
            if (string.IsNullOrEmpty(firmasData)) return false;

            try
            {
                var firmas = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firmasData);
                if (firmas == null || firmas.Count == 0) return false;

                foreach (var kvp in firmas)
                {
                    var value = kvp.Value;
                    if (value.ValueKind != JsonValueKind.Object) return false;

                    if (!value.TryGetProperty("firma", out var firmaObj) || firmaObj.ValueKind != JsonValueKind.Object)
                        return false;

                    bool hasUrl = firmaObj.TryGetProperty("url", out var urlProp) &&
                                  urlProp.ValueKind == JsonValueKind.String &&
                                  !string.IsNullOrWhiteSpace(urlProp.GetString());

                    bool hasBase64 = firmaObj.TryGetProperty("base64", out var b64Prop) &&
                                     b64Prop.ValueKind == JsonValueKind.String &&
                                     !string.IsNullOrWhiteSpace(b64Prop.GetString());

                    if (!hasUrl && !hasBase64) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task CheckPendingSignaturesAsync(ApplicationDbContext context, IEmailService emailService)
        {
            try
            {
                var config = await context.AlertConfigurations.FirstOrDefaultAsync();
                if (config == null || !config.EnableSignatureAlerts) return;

                var threshold = DateTime.Now.AddHours(-config.SignatureAlertDelay);
                var candidateForms = await context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt < threshold &&
                               !context.Signatures.Any(s => s.FilledFormId == f.FormID))
                    .ToListAsync();

                // Filtrar: excluir formularios donde TODAS las firmas ya están completadas en FirmasData
                var pendingForms = candidateForms
                    .Where(f => !AllFirmasCompleted(f.FirmasData))
                    .ToList();

                // Precargar catálogo de firmas para buscar emails
                var catalogo = await context.CatalogoFirmas.ToListAsync();

                foreach (var form in pendingForms)
                {
                    var existingAlert = await context.Alerts
                        .AnyAsync(a => a.Type == "pending_signature" &&
                                      a.FormId == form.FormID &&
                                      a.CreatedDate > DateTime.Now.AddDays(-1));

                    if (!existingAlert)
                    {
                        // 1) Enviar a los recipients configurados globalmente (SGI) + responsable creador
                        var recipients = ParseRecipients(config.SignatureRecipients);
                        if (!string.IsNullOrWhiteSpace(form.FilledByEmail))
                        {
                            recipients.Add(form.FilledByEmail.Trim());
                        }
                        recipients = recipients
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        foreach (var recipientEmail in recipients)
                        {
                            var alert = new Alert
                            {
                                Type = "pending_signature",
                                Priority = "medium",
                                Title = $"Firma Pendiente: {form.Template?.Nombre}",
                                Message = $"El formulario '{form.Template?.Nombre}' del {form.CreatedAt:dd/MM/yyyy} esta pendiente de firma",
                                TargetEmail = recipientEmail,
                                FormId = form.FormID,
                                FormCode = form.Template?.Codigo,
                                CreatedDate = DateTime.Now,
                                Status = "pending"
                            };

                            context.Alerts.Add(alert);
                            await context.SaveChangesAsync();

                            var sent = await emailService.SendAlertEmailAsync(recipientEmail, alert.Title, alert.Message);
                            alert.Status = sent ? "sent" : "failed";
                            await context.SaveChangesAsync();
                        }

                        // 2) Escalamiento jerárquico: notificar al jefe/superior de cada firmante
                        await NotifyJefesForPendingSignaturesAsync(context, emailService, form, catalogo);
                    }
                }

                _logger.LogInformation("CheckPendingSignaturesAsync ejecutado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckPendingSignaturesAsync");
            }
        }

        /// <summary>
        /// Notifica a los jefes/superiores de cada firmante cuando hay firmas pendientes pasadas las 24h.
        /// Deserializa las firmas del template, identifica los jefeAlerta y envía emails de escalamiento.
        /// </summary>
        private async Task NotifyJefesForPendingSignaturesAsync(
            ApplicationDbContext context,
            IEmailService emailService,
            FilledForm form,
            List<CatalogoFirma> catalogo)
        {
            try
            {
                // Obtener las firmas definidas en el template
                var firmasJson = form.Template?.Firmas;
                if (string.IsNullOrEmpty(firmasJson)) return;

                var firmas = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(firmasJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (firmas == null || firmas.Count == 0) return;

                // Recopilar nombres de jefes únicos para notificar
                var jefesANotificar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var detallesFirmantes = new List<string>();

                foreach (var firma in firmas)
                {
                    var jefesActivos = firma.JefeAlerta?.Where(j => !string.IsNullOrWhiteSpace(j)).ToList() ?? new List<string>();
                    if (jefesActivos.Count > 0)
                    {
                        foreach (var jefe in jefesActivos)
                            jefesANotificar.Add(jefe);
                        detallesFirmantes.Add($"• {firma.NombreCompleto ?? "Sin nombre"} ({firma.Puesto ?? "Sin puesto"}) → Jefes: {string.Join(", ", jefesActivos)}");
                    }

                    // También incluir reemplazos como potenciales firmantes no firmados
                    if (firma.Reemplazos != null && jefesActivos.Count > 0)
                    {
                        foreach (var reemplazo in firma.Reemplazos)
                        {
                            if (!string.IsNullOrWhiteSpace(reemplazo))
                            {
                                detallesFirmantes.Add($"• Reemplazo: {reemplazo} → Jefes: {string.Join(", ", jefesActivos)}");
                            }
                        }
                    }
                }

                if (jefesANotificar.Count == 0) return;

                var subject = $"⚠️ Escalamiento: Firmas Pendientes +24h - {form.Template?.Codigo} {form.Template?.Nombre}";
                var detallesHtml = string.Join("<br/>", detallesFirmantes.Select(d => System.Net.WebUtility.HtmlEncode(d)));
                var body = $"<html><body style='font-family:Arial;padding:20px;'>"
                    + $"<div style='background:#dc2626;color:white;padding:20px;border-radius:8px 8px 0 0;'>"
                    + $"<h2 style='margin:0;'>⚠️ Escalamiento de Firmas Pendientes</h2></div>"
                    + $"<div style='border:1px solid #e5e7eb;padding:20px;border-radius:0 0 8px 8px;'>"
                    + $"<p>El siguiente formulario lleva <strong>más de 24 horas sin firmar</strong>:</p>"
                    + $"<p><strong>Código:</strong> {form.Template?.Codigo}</p>"
                    + $"<p><strong>Nombre:</strong> {form.Template?.Nombre}</p>"
                    + $"<p><strong>Creado:</strong> {form.CreatedAt:dd/MM/yyyy HH:mm}</p>"
                    + $"<hr style='border:1px solid #e5e7eb;'/>"
                    + $"<p><strong>Firmantes pendientes:</strong></p>"
                    + $"<p>{detallesHtml}</p>"
                    + $"<hr style='border:1px solid #e5e7eb;'/>"
                    + $"<p style='color:#dc2626;font-weight:bold;'>Por favor gestione las firmas pendientes lo antes posible.</p>"
                    + $"<p style='color:#6b7280;font-size:12px;'>Este correo se genera automáticamente por el sistema de escalamiento de alertas de Frigolab.</p>"
                    + $"</div></body></html>";

                foreach (var jefeNombre in jefesANotificar)
                {
                    var entry = catalogo.FirstOrDefault(c =>
                        (c.NombreCompleto ?? "").Equals(jefeNombre, StringComparison.OrdinalIgnoreCase));
                    var correo = entry?.Correo;

                    if (!string.IsNullOrWhiteSpace(correo))
                    {
                        var alert = new Alert
                        {
                            Type = "signature_escalation",
                            Priority = "high",
                            Title = subject,
                            Message = $"Escalamiento: Firmas pendientes +24h para {form.Template?.Nombre}. Jefe notificado: {jefeNombre}",
                            TargetEmail = correo,
                            FormId = form.FormID,
                            FormCode = form.Template?.Codigo,
                            CreatedDate = DateTime.Now,
                            Status = "pending"
                        };

                        context.Alerts.Add(alert);
                        await context.SaveChangesAsync();

                        var sent = await emailService.SendAlertEmailAsync(correo, subject, body);
                        alert.Status = sent ? "sent" : "failed";
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al notificar jefes para firmas pendientes del formulario {FormId}", form.FormID);
            }
        }
    }
}