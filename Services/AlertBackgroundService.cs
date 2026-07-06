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

            DateTime? lastDailySentDate = null;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;

                    // Trigger consolidated daily summary email at 7:30 AM
                    if (now.Hour == 7 && now.Minute >= 30 && (!lastDailySentDate.HasValue || lastDailySentDate.Value.Date < now.Date))
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                            // 1. Run database alert generation
                            await CheckMissingFormsAsync(context, emailService);
                            await CheckPendingSignaturesAsync(context, emailService);
                            await CheckTemplateChangesAsync(context, emailService);

                            // 2. Send consolidated emails
                            await SendDailyConsolidatedAlertsAsync(context, emailService);
                        }

                        lastDailySentDate = now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en AlertBackgroundService");
                }

                // Sleep for 1 minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private static List<string> ParseRecipients(string? recipientsJson)
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

                var today = DateTime.Today;
                var templates = await context.Templates
                    .Where(t => t.Frecuencia == "Diaria" && !t.IsObsolete && !t.IsDraft)
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

                            // Email inmediato omitido (gestionado en resumen diario a las 7:30am)
                            alert.Status = "pending";
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
        /// Envía UN correo resumen diario con los cambios de plantillas registrados en TemplateChangeLogs
        /// en las últimas 24h. A propósito NO se envía un correo por cada guardado — eso se desactivó antes
        /// porque saturaba las bandejas de entrada al hacer cambios seguidos en producción.
        /// </summary>
        private async Task CheckTemplateChangesAsync(ApplicationDbContext context, IEmailService emailService)
        {
            try
            {
                var config = await context.AlertConfigurations.FirstOrDefaultAsync();
                if (config == null || !config.EnableTemplateChangeAlerts) return;

                var recipients = ParseRecipients(config.TemplateChangeRecipients);
                if (recipients.Count == 0) return;

                var since = DateTime.Now.AddHours(-24);
                var recentChanges = await context.TemplateChangeLogs
                    .Include(c => c.Template)
                    .Where(c => c.Fecha >= since)
                    .OrderBy(c => c.Fecha)
                    .ToListAsync();

                if (recentChanges.Count == 0) return;

                var rowsHtml = string.Join("", recentChanges.Select(c =>
                    $"<tr>" +
                    $"<td style='padding:6px 10px;border-bottom:1px solid #e5e7eb;'>{c.Fecha:dd/MM/yyyy HH:mm}</td>" +
                    $"<td style='padding:6px 10px;border-bottom:1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(c.Template?.Codigo ?? c.TemplateID.ToString())}</td>" +
                    $"<td style='padding:6px 10px;border-bottom:1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(c.Template?.Nombre ?? "")}</td>" +
                    $"<td style='padding:6px 10px;border-bottom:1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(c.Version)}</td>" +
                    $"<td style='padding:6px 10px;border-bottom:1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(c.CambioRealizado)}</td>" +
                    $"</tr>"));

                var body = $@"
                    <h2>📋 Resumen diario de cambios en plantillas</h2>
                    <p>Se registraron {recentChanges.Count} cambio(s) de plantilla en las últimas 24 horas:</p>
                    <table style='border-collapse:collapse;width:100%;font-family:sans-serif;font-size:13px;'>
                        <thead>
                            <tr style='background:#f1f5f9;text-align:left;'>
                                <th style='padding:6px 10px;'>Fecha</th>
                                <th style='padding:6px 10px;'>Código</th>
                                <th style='padding:6px 10px;'>Plantilla</th>
                                <th style='padding:6px 10px;'>Versión</th>
                                <th style='padding:6px 10px;'>Cambio</th>
                            </tr>
                        </thead>
                        <tbody>{rowsHtml}</tbody>
                    </table>";

                foreach (var recipientEmail in recipients)
                {
                    await emailService.SendAlertEmailAsync(recipientEmail, "Resumen diario: cambios en plantillas", body);
                }

                _logger.LogInformation("CheckTemplateChangesAsync: enviado resumen de {Count} cambios a {Recipients} destinatario(s)", recentChanges.Count, recipients.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CheckTemplateChangesAsync");
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

                            // Email inmediato omitido (gestionado en resumen diario a las 7:30am)
                            alert.Status = "pending";
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

                        // Email inmediato de escalamiento omitido (gestionado en resumen diario a las 7:30am)
                        alert.Status = "pending";
                        await context.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al notificar jefes para firmas pendientes del formulario {FormId}", form.FormID);
            }
        }

        // ===== NUEVAS FUNCIONES PARA CONSOLIDADO DIARIO 7:30 AM =====

        private class PendingSlotDescriptor
        {
            public int FormId { get; set; }
            public string FormCode { get; set; } = string.Empty;
            public string FormName { get; set; } = string.Empty;
            public string Puesto { get; set; } = string.Empty;
            public string AssignedName { get; set; } = string.Empty;
            public string AssignedEmail { get; set; } = string.Empty;
            public List<string> Jefes { get; set; } = new();
            public DateTime FormCreatedAt { get; set; }
            public double HoursPending => (DateTime.Now - FormCreatedAt).TotalHours;
        }

        private class RecentSignatureDescriptor
        {
            public string SignedByEmail { get; set; } = string.Empty;
            public string Puesto { get; set; } = string.Empty;
            public List<string> Jefes { get; set; } = new();
        }

        private class MissingFormDescriptor
        {
            public int TemplateId { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Frecuencia { get; set; } = string.Empty;
            public string Area { get; set; } = string.Empty;
            public string Supervisa { get; set; } = string.Empty;
            public List<string> Jefes { get; set; } = new();
        }

        public static async Task SendDailyConsolidatedAlertsAsync(ApplicationDbContext context, IEmailService emailService, ILogger? logger = null)
        {
            logger?.LogInformation("Iniciando envío de alertas consolidadas diarias a las 7:30 AM / Manual");

            try
            {
                // 1. Obtener datos de la base de datos
                var users = await context.CatalogoFirmas.Where(u => u.Activo).ToListAsync();
                var templates = await context.Templates.Where(t => !t.IsObsolete && !t.IsDraft).ToListAsync();
                
                var cutoffDate = DateTime.Today.AddDays(-30);
                var recentFilledForms = await context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt >= cutoffDate)
                    .ToListAsync();

                var pendingForms = await context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => !context.Signatures.Any(s => s.FilledFormId == f.FormID) && 
                               !context.SignatureRejections.Any(r => r.FilledFormId == f.FormID))
                    .ToListAsync();
                    
                // Filtrar las que realmente tienen alguna firma pendiente en FirmasData
                pendingForms = pendingForms.Where(f => !AllFirmasCompleted(f.FirmasData)).ToList();

                var recentSignatures = await context.Signatures
                    .Include(s => s.FilledForm)
                    .ThenInclude(f => f.Template)
                    .Where(s => s.SignedDate >= DateTime.Now.AddDays(-1))
                    .ToListAsync();

                // 2. Parsear y construir descriptores en memoria para firmas pendientes
                var allPendingSlots = new List<PendingSlotDescriptor>();
                foreach (var form in pendingForms)
                {
                    if (string.IsNullOrEmpty(form.FirmasData)) continue;
                    
                    Dictionary<string, JsonElement> firmasDict;
                    try
                    {
                        firmasDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.FirmasData) ?? new();
                    }
                    catch
                    {
                        continue;
                    }

                    // Parsear definición del template
                    var templateFirmasList = new List<FirmaAlertInfo>();
                    if (!string.IsNullOrEmpty(form.Template?.Firmas))
                    {
                        try
                        {
                            templateFirmasList = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(form.Template!.Firmas, 
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                        }
                        catch {}
                    }

                    foreach (var kvp in firmasDict)
                    {
                        var slotName = kvp.Key;
                        var slotData = kvp.Value;
                        if (slotData.ValueKind != JsonValueKind.Object) continue;

                        // Verificar si está firmado
                        bool isSigned = false;
                        if (slotData.TryGetProperty("firma", out var firmaObj) && firmaObj.ValueKind == JsonValueKind.Object)
                        {
                            bool hasUrl = firmaObj.TryGetProperty("url", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString());
                            bool hasBase64 = firmaObj.TryGetProperty("base64", out var b64Prop) && !string.IsNullOrWhiteSpace(b64Prop.GetString());
                            isSigned = hasUrl || hasBase64;
                        }

                        if (isSigned) continue; // ya firmado, saltar

                        // Es una firma pendiente
                        string assignedName = slotData.TryGetProperty("nombre", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        string assignedEmail = slotData.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
                        
                        // Si no hay email, ver si el nombre tiene formato de email
                        if (string.IsNullOrEmpty(assignedEmail) && assignedName.Contains("@"))
                        {
                            assignedEmail = assignedName;
                        }

                        // Obtener Jefes desde el template para este puesto
                        var templateFirmaDef = templateFirmasList.FirstOrDefault(tf => 
                            (tf.Puesto ?? "").Equals(slotName, StringComparison.OrdinalIgnoreCase));
                        var jefesList = templateFirmaDef?.JefeAlerta ?? new List<string>();

                        allPendingSlots.Add(new PendingSlotDescriptor
                        {
                            FormId = form.FormID,
                            FormCode = form.Template?.Codigo ?? "N/A",
                            FormName = form.Template?.Nombre ?? "Formulario",
                            Puesto = slotName,
                            AssignedName = assignedName,
                            AssignedEmail = assignedEmail,
                            Jefes = jefesList,
                            FormCreatedAt = form.CreatedAt
                        });
                    }
                }

                // 3. Parsear y construir descriptores en memoria para firmas realizadas recientemente (últimas 24h)
                var allRecentSignatures = new List<RecentSignatureDescriptor>();
                foreach (var sig in recentSignatures)
                {
                    if (sig.FilledForm == null) continue;

                    string puesto = "";
                    var jefesList = new List<string>();

                    // Intentar buscar el puesto en FirmasData
                    if (!string.IsNullOrEmpty(sig.FilledForm.FirmasData))
                    {
                        try
                        {
                            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(sig.FilledForm.FirmasData);
                            if (dict != null)
                            {
                                foreach (var kvp in dict)
                                {
                                    if (kvp.Value.ValueKind == JsonValueKind.Object && 
                                        kvp.Value.TryGetProperty("email", out var eProp) && 
                                        (eProp.GetString() ?? "").Equals(sig.SignedBy, StringComparison.OrdinalIgnoreCase))
                                    {
                                        puesto = kvp.Key;
                                        break;
                                    }
                                }
                            }
                        }
                        catch {}
                    }

                    // Si se encontró el puesto, buscar los jefes definidos en el template
                    if (!string.IsNullOrEmpty(puesto) && !string.IsNullOrEmpty(sig.FilledForm.Template?.Firmas))
                    {
                        try
                        {
                            var tfList = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(sig.FilledForm.Template!.Firmas,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            var tfDef = tfList?.FirstOrDefault(tf => (tf.Puesto ?? "").Equals(puesto, StringComparison.OrdinalIgnoreCase));
                            if (tfDef?.JefeAlerta != null)
                            {
                                jefesList = tfDef.JefeAlerta;
                            }
                        }
                        catch {}
                    }

                    allRecentSignatures.Add(new RecentSignatureDescriptor
                    {
                        SignedByEmail = sig.SignedBy,
                        Puesto = puesto,
                        Jefes = jefesList
                    });
                }

                // 4. Identificar formularios faltantes acorde a la periodicidad
                var allMissingForms = new List<MissingFormDescriptor>();
                foreach (var temp in templates)
                {
                    var freq = temp.Frecuencia ?? "Diaria";
                    DateTime limitDate;
                    if (freq.Equals("Semanal", StringComparison.OrdinalIgnoreCase))
                    {
                        limitDate = DateTime.Now.AddDays(-7);
                    }
                    else if (freq.Equals("Mensual", StringComparison.OrdinalIgnoreCase))
                    {
                        limitDate = DateTime.Now.AddDays(-30);
                    }
                    else // "Diaria" u otros
                    {
                        limitDate = DateTime.Now.AddDays(-1);
                    }

                    // Verificar si hay algún formulario lleno en el rango
                    var hasForm = recentFilledForms.Any(f => f.TemplateID == temp.TemplateID && f.CreatedAt >= limitDate);
                    if (!hasForm)
                    {
                        // Extraer jefes definidos para este template
                        var jefesEnTemplate = new List<string>();
                        if (!string.IsNullOrEmpty(temp.Firmas))
                        {
                            try
                            {
                                var tfList = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(temp.Firmas,
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                if (tfList != null)
                                {
                                    foreach (var tf in tfList)
                                    {
                                        if (tf.JefeAlerta != null)
                                        {
                                            jefesEnTemplate.AddRange(tf.JefeAlerta);
                                        }
                                    }
                                }
                            }
                            catch {}
                        }

                        allMissingForms.Add(new MissingFormDescriptor
                        {
                            TemplateId = temp.TemplateID,
                            Code = temp.Codigo,
                            Name = temp.Nombre,
                            Frecuencia = freq,
                            Area = temp.Area ?? "N/A",
                            Supervisa = temp.Supervisa ?? "N/A",
                            Jefes = jefesEnTemplate.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                        });
                    }
                }

                // 5. Enviar correo a cada usuario activo con su resumen
                foreach (var user in users)
                {
                    if (string.IsNullOrWhiteSpace(user.Correo)) continue;

                    // - Sus firmas realizadas en las últimas 24h
                    var realizadasEllosCount = allRecentSignatures.Count(s => s.SignedByEmail.Equals(user.Correo, StringComparison.OrdinalIgnoreCase));
                    
                    // - Sus firmas pendientes
                    var pendientesEllos = allPendingSlots.Where(s => s.AssignedEmail.Equals(user.Correo, StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    // - Firmas realizadas por su gente (subordinados)
                    var realizadasGenteCount = allRecentSignatures.Count(s => s.Jefes.Any(j => j.Equals(user.NombreCompleto, StringComparison.OrdinalIgnoreCase)));
                    
                    // - Firmas pendientes por su gente (subordinados)
                    var pendientesGente = allPendingSlots.Where(s => s.Jefes.Any(j => j.Equals(user.NombreCompleto, StringComparison.OrdinalIgnoreCase))).ToList();

                    // - Formularios no realizados relevantes a su área o donde es jefe
                    var missingReforms = allMissingForms.Where(m => 
                        m.Jefes.Any(j => j.Equals(user.NombreCompleto, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(m.Area) && m.Area.Equals(user.Area, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(m.Supervisa) && m.Supervisa.Equals(user.Area, StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    // Solo enviar email si tiene algo que reportar
                    bool hasAlertsToSend = pendientesEllos.Any() || pendientesGente.Any() || missingReforms.Any() || realizadasEllosCount > 0 || realizadasGenteCount > 0;
                    
                    if (hasAlertsToSend)
                    {
                        var htmlBody = BuildConsolidatedEmailHtml(user, realizadasEllosCount, pendientesEllos, realizadasGenteCount, pendientesGente, missingReforms);
                        var subject = $"📊 Resumen Diario de Alertas y Firmas - {user.NombreCompleto}";
                        
                        await emailService.SendAlertEmailAsync(user.Correo, subject, htmlBody);
                    }
                }

                // 6. Enviar reporte global consolidado al SGI y destinatarios globales de configuración
                var config = await context.AlertConfigurations.FirstOrDefaultAsync();
                if (config != null)
                {
                    var globalRecipients = new List<string>();
                    globalRecipients.AddRange(ParseRecipients(config.SignatureRecipients));
                    globalRecipients.AddRange(ParseRecipients(config.MissingFormRecipients));
                    globalRecipients = globalRecipients.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    if (globalRecipients.Any())
                    {
                        var dummyGlobalUser = new CatalogoFirma { NombreCompleto = "Dirección / SGI Quality", Area = "Global" };
                        
                        var htmlGlobal = BuildConsolidatedEmailHtml(
                            dummyGlobalUser, 
                            allRecentSignatures.Count, 
                            allPendingSlots, 
                            0, 
                            new List<PendingSlotDescriptor>(), 
                            allMissingForms,
                            isGlobalReport: true
                        );

                        var subjectGlobal = "📊 Reporte Consolidado Global de Firmas y Formularios Faltantes - Frigolab";
                        foreach (var email in globalRecipients)
                        {
                            await emailService.SendAlertEmailAsync(email, subjectGlobal, htmlGlobal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error en SendDailyConsolidatedAlertsAsync");
            }
        }

        private static string FormatPendingTime(double totalHours)
        {
            var span = TimeSpan.FromHours(totalHours);
            if (span.TotalDays >= 1)
            {
                int days = (int)span.TotalDays;
                int hours = span.Hours;
                return $"{days}d {hours}h";
            }
            else
            {
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            }
        }

        private static string GetPendingTimePillHtml(double totalHours)
        {
            var timeStr = FormatPendingTime(totalHours);
            string bg, color;
            if (totalHours < 24)
            {
                bg = "#dcfce7"; // light green
                color = "#15803d"; // dark green
            }
            else if (totalHours < 72)
            {
                bg = "#fef3c7"; // light orange/yellow
                color = "#b45309"; // dark orange
            }
            else
            {
                bg = "#fee2e2"; // light red
                color = "#b91c1c"; // dark red
            }
            return $"<span style='background-color:{bg}; color:{color}; padding: 4px 10px; border-radius: 9999px; font-weight: bold; font-size: 11px; white-space: nowrap;'>{timeStr}</span>";
        }

        private static string BuildConsolidatedEmailHtml(
            CatalogoFirma user, 
            int realizadasEllosCount, 
            List<PendingSlotDescriptor> pendientesEllos, 
            int realizadasGenteCount, 
            List<PendingSlotDescriptor> pendientesGente, 
            List<MissingFormDescriptor> missingForms,
            bool isGlobalReport = false)
        {
            var todayStr = DateTime.Now.ToString("dd/MM/yyyy");
            
            var title = isGlobalReport ? "Reporte Consolidado Global de Calidad y Producción" : "Resumen Diario de Alertas y Firmas";
            var scopeName = isGlobalReport ? "Dirección / SGI" : user.NombreCompleto;
            var areaName = isGlobalReport ? "Toda la Planta" : (user.Area ?? "N/A");

            // KPI Blocks construidos como celdas de tabla para máxima compatibilidad con clientes de correo (Outlook, etc.)
            string kpiSectionHtml = "";
            if (isGlobalReport)
            {
                kpiSectionHtml = $@"
                    <table style='width: 100%; border-collapse: separate; border-spacing: 15px 0; margin-bottom: 25px;'>
                        <tr>
                            <td style='width: 50%; background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 10px; padding: 15px; text-align: center;'>
                                <div style='font-size: 11px; font-weight: bold; color: #16a34a; text-transform: uppercase; letter-spacing: 0.05em;'>Total Firmas Pendientes</div>
                                <div style='font-size: 32px; font-weight: bold; color: #166534; margin: 5px 0;'>{pendientesEllos.Count}</div>
                                <div style='font-size: 11px; color: #14532d;'>({realizadasEllosCount} realizadas hoy)</div>
                            </td>
                            <td style='width: 50%; background-color: #fffbeb; border: 1px solid #fef3c7; border-radius: 10px; padding: 15px; text-align: center;'>
                                <div style='font-size: 11px; font-weight: bold; color: #d97706; text-transform: uppercase; letter-spacing: 0.05em;'>Formularios Faltantes</div>
                                <div style='font-size: 32px; font-weight: bold; color: #92400e; margin: 5px 0;'>{missingForms.Count}</div>
                                <div style='font-size: 11px; color: #78350f;'>según periodicidad</div>
                            </td>
                        </tr>
                    </table>";
            }
            else
            {
                kpiSectionHtml = $@"
                    <table style='width: 100%; border-collapse: separate; border-spacing: 15px 0; margin-bottom: 25px;'>
                        <tr>
                            <td style='width: 33.33%; background-color: #fdf2f8; border: 1px solid #fbcfe8; border-radius: 10px; padding: 15px; text-align: center;'>
                                <div style='font-size: 11px; font-weight: bold; color: #db2777; text-transform: uppercase; letter-spacing: 0.05em;'>Mis Firmas Pendientes</div>
                                <div style='font-size: 32px; font-weight: bold; color: #9d174d; margin: 5px 0;'>{pendientesEllos.Count}</div>
                                <div style='font-size: 11px; color: #86198f;'>({realizadasEllosCount} realizadas hoy)</div>
                            </td>
                            <td style='width: 33.33%; background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 10px; padding: 15px; text-align: center;'>
                                <div style='font-size: 11px; font-weight: bold; color: #16a34a; text-transform: uppercase; letter-spacing: 0.05em;'>Firmas de mi Gente</div>
                                <div style='font-size: 32px; font-weight: bold; color: #166534; margin: 5px 0;'>{pendientesGente.Count}</div>
                                <div style='font-size: 11px; color: #14532d;'>({realizadasGenteCount} realizadas hoy)</div>
                            </td>
                            <td style='width: 33.33%; background-color: #fffbeb; border: 1px solid #fef3c7; border-radius: 10px; padding: 15px; text-align: center;'>
                                <div style='font-size: 11px; font-weight: bold; color: #d97706; text-transform: uppercase; letter-spacing: 0.05em;'>Formularios Faltantes</div>
                                <div style='font-size: 32px; font-weight: bold; color: #92400e; margin: 5px 0;'>{missingForms.Count}</div>
                                <div style='font-size: 11px; color: #78350f;'>según periodicidad</div>
                            </td>
                        </tr>
                    </table>";
            }

            // Build Tables
            var misPendientesTableHtml = "";
            if (!isGlobalReport && pendientesEllos.Any())
            {
                var rows = string.Join("", pendientesEllos.Select(p => $@"
                    <tr>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'><strong>{p.FormCode}</strong></td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'>{p.FormName}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{p.Puesto}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{p.FormCreatedAt:dd/MM/yyyy HH:mm}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; text-align: center;'>{GetPendingTimePillHtml(p.HoursPending)}</td>
                    </tr>"));

                misPendientesTableHtml = $@"
                    <div style='margin-top: 25px;'>
                        <h3 style='font-size: 16px; color: #1e3a8a; border-bottom: 2px solid #3b82f6; padding-bottom: 5px; margin-bottom: 15px;'>✍️ Mis Firmas Pendientes</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px; text-align: left;'>
                            <thead>
                                <tr style='background-color: #f8fafc; color: #334155; font-weight: bold;'>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Código</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Formulario</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Puesto</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Creado El</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1; text-align: center;'>Tiempo Pendiente</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows}
                            </tbody>
                        </table>
                    </div>";
            }

            var equipoPendientesTableHtml = "";
            var listForEquipo = isGlobalReport ? pendientesEllos : pendientesGente;
            if (listForEquipo.Any())
            {
                var sectionTitle = isGlobalReport ? "📋 Detalle de Todas las Firmas Pendientes" : "👥 Firmas Pendientes de mi Equipo";
                var rows = string.Join("", listForEquipo.Select(p => $@"
                    <tr>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'><strong>{p.FormCode}</strong></td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'>{p.FormName}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{p.AssignedName}<br/><span style='font-size: 11px; color:#6b7280;'>({p.Puesto})</span></td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{p.FormCreatedAt:dd/MM/yyyy HH:mm}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; text-align: center;'>{GetPendingTimePillHtml(p.HoursPending)}</td>
                    </tr>"));

                equipoPendientesTableHtml = $@"
                    <div style='margin-top: 25px;'>
                        <h3 style='font-size: 16px; color: #1e3a8a; border-bottom: 2px solid #3b82f6; padding-bottom: 5px; margin-bottom: 15px;'>{sectionTitle}</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px; text-align: left;'>
                            <thead>
                                <tr style='background-color: #f8fafc; color: #334155; font-weight: bold;'>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Código</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Formulario</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Firmante Asignado</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1;'>Creado El</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #cbd5e1; text-align: center;'>Tiempo Pendiente</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows}
                            </tbody>
                        </table>
                    </div>";
            }

            var missingFormsTableHtml = "";
            if (missingForms.Any())
            {
                var rows = string.Join("", missingForms.Select(m => $@"
                    <tr>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'><strong>{m.Code}</strong></td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'>{m.Name}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb;'><span style='background-color:#fee2e2; color:#b91c1c; padding:2px 8px; border-radius:4px; font-weight:bold; font-size:11px;'>{m.Frecuencia}</span></td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{m.Area}</td>
                        <td style='padding: 10px; border-bottom: 1px solid #e5e7eb; color: #4b5563;'>{m.Supervisa}</td>
                    </tr>"));

                missingFormsTableHtml = $@"
                    <div style='margin-top: 25px;'>
                        <h3 style='font-size: 16px; color: #9a3412; border-bottom: 2px solid #ea580c; padding-bottom: 5px; margin-bottom: 15px;'>⚠️ Formularios Faltantes según Periodicidad</h3>
                        <table style='width: 100%; border-collapse: collapse; font-size: 13px; text-align: left;'>
                            <thead>
                                <tr style='background-color: #fafaf9; color: #44403c; font-weight: bold;'>
                                    <th style='padding: 10px; border-bottom: 2px solid #e7e5e4;'>Código</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #e7e5e4;'>Formulario</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #e7e5e4;'>Periodicidad</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #e7e5e4;'>Área</th>
                                    <th style='padding: 10px; border-bottom: 2px solid #e7e5e4;'>Proceso</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows}
                            </tbody>
                        </table>
                    </div>";
            }

            var html = $@"
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='font-family: Arial, sans-serif; background-color: #f4f6f9; margin: 0; padding: 20px; color: #1e293b;'>
    <div style='max-width: 800px; margin: 0 auto; background-color: white; border-radius: 12px; box-shadow: 0 4px 15px rgba(0,0,0,0.05); overflow: hidden; border: 1px solid #e2e8f0;'>
        
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); padding: 30px; color: white;'>
            <table style='width: 100%; border-collapse: collapse;'>
                <tr>
                    <td>
                        <h1 style='margin: 0; font-size: 22px; font-weight: bold; letter-spacing: -0.025em;'>{title}</h1>
                        <p style='margin: 5px 0 0 0; font-size: 13px; color: #93c5fd;'>Sistema de Gestión de Alertas y Calidad - Frigolab</p>
                    </td>
                    <td style='text-align: right; font-size: 13px; color: #dbeafe;'>
                        <div>Fecha: <strong>{todayStr}</strong></div>
                        <div style='margin-top: 4px;'>Hora: <strong>07:30 AM</strong></div>
                    </td>
                </tr>
            </table>
        </div>

        <!-- Content -->
        <div style='padding: 30px;'>
            
            <!-- User Scope Badge -->
            <div style='background-color: #f1f5f9; border-radius: 8px; padding: 12px 15px; margin-bottom: 25px; border-left: 4px solid #1e3a8a;'>
                <table style='width: 100%; font-size: 13px;'>
                    <tr>
                        <td>Destinatario: <strong style='color:#1e3a8a;'>{scopeName}</strong></td>
                        <td style='text-align: right;'>Área/Proceso: <strong style='color:#1e3a8a;'>{areaName}</strong></td>
                    </tr>
                </table>
            </div>

            <!-- Stats Table -->
            {kpiSectionHtml}

            <hr style='border: 0; border-top: 1px solid #e2e8f0; margin: 25px 0;' />

            <!-- Tables Sections -->
            {misPendientesTableHtml}
            {equipoPendientesTableHtml}
            {missingFormsTableHtml}

            <!-- CTA -->
            <div style='margin-top: 40px; text-align: center;'>
                <a href='http://localhost:5173/signatures' style='display: inline-block; background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); color: white; padding: 14px 35px; border-radius: 8px; font-weight: bold; text-decoration: none; box-shadow: 0 4px 10px rgba(59, 130, 246, 0.3); font-size: 15px;'>✍️ Ir a Gestionar Firmas en el Portal</a>
            </div>
            
        </div>

        <!-- Footer -->
        <div style='background-color: #f8fafc; padding: 20px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 11px; color: #64748b;'>
            <p style='margin: 0 0 5px 0; font-weight: bold; color: #475569;'>Este es un informe diario consolidado de Frigolab.</p>
            <p style='margin: 0;'>Por favor, no responda directamente a este correo electrónico.</p>
            <p style='margin: 5px 0 0 0; color: #94a3b8;'>© 2026 Frigolab. Todos los derechos reservados.</p>
        </div>
        
    </div>
</body>
</html>";
            return html;
        }
    }
}