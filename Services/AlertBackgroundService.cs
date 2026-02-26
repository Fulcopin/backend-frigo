using FormBuilder.API.Data;
using Microsoft.EntityFrameworkCore;

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

        private async Task CheckPendingSignaturesAsync(ApplicationDbContext context, IEmailService emailService)
        {
            try
            {
                var config = await context.AlertConfigurations.FirstOrDefaultAsync();
                if (config == null || !config.EnableSignatureAlerts) return;

                var threshold = DateTime.Now.AddHours(-config.SignatureAlertDelay);
                var pendingForms = await context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt < threshold &&
                               !context.Signatures.Any(s => s.FilledFormId == f.FormID))
                    .ToListAsync();

                foreach (var form in pendingForms)
                {
                    var existingAlert = await context.Alerts
                        .AnyAsync(a => a.Type == "pending_signature" &&
                                      a.FormId == form.FormID &&
                                      a.CreatedDate > DateTime.Now.AddDays(-1));

                    if (!existingAlert)
                    {
                        var recipients = ParseRecipients(config.SignatureRecipients);

                        foreach (var recipientEmail in recipients)
                        {
                            var alert = new Models.Alert
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
                    }
                }

                _logger.LogInformation("CheckPendingSignaturesAsync ejecutado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckPendingSignaturesAsync");
            }
        }
    }
}