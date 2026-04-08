using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SignaturesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SignaturesController> _logger;
        private readonly IEmailService _emailService;

        public SignaturesController(ApplicationDbContext context, ILogger<SignaturesController> logger, IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        // GET /api/Signatures/pending
        [HttpGet("pending")]
        public async Task<ActionResult<IEnumerable<object>>> GetPendingForms()
        {
            try
            {
                var rawForms = await _context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => !_context.Signatures.Any(s => s.FilledFormId == f.FormID))
                    .Where(f => !_context.SignatureRejections.Any(r => r.FilledFormId == f.FormID))
                    .Select(f => new
                    {
                        id = f.FormID,
                        templateId = f.TemplateID,
                        templateName = f.Template!.Nombre,
                        formCode = f.Template!.Codigo,
                        headerData = f.HeaderData,
                        firmasData = f.FirmasData,
                        createdDate = f.CreatedAt,
                        area = f.Template.Proceso ?? f.Template.Area ?? "N/A"
                    })
                    .ToListAsync();

                // Excluir formularios donde TODAS las firmas ya están completadas en FirmasData
                var pendingForms = rawForms
                    .Where(f => !AllFirmasCompleted(f.firmasData))
                    .Select(f => new
                {
                    f.id,
                    f.templateId,
                    f.templateName,
                    f.formCode,
                    createdBy = ExtractCreatedBy(f.headerData, f.firmasData),
                    f.createdDate,
                    f.area,
                    isSigned = false,
                    f.firmasData
                }).ToList();

                return Ok(pendingForms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener formularios pendientes");
                return StatusCode(500, new { message = "Error al obtener formularios pendientes" });
            }
        }

        // POST /api/Signatures/sign/{formId}
        [HttpPost("sign/{formId}")]
        public async Task<ActionResult> SignForm(int formId, [FromBody] SignFormRequest request)
        {
            try
            {
                var form = await _context.FilledForms
                    .Include(f => f.Template)
                    .FirstOrDefaultAsync(f => f.FormID == formId);
                if (form == null)
                {
                    return NotFound(new { message = "Formulario no encontrado" });
                }

                var existingSignature = await _context.Signatures
                    .FirstOrDefaultAsync(s => s.FilledFormId == formId);

                if (existingSignature != null)
                {
                    return BadRequest(new { message = "El formulario ya esta firmado" });
                }

                var signature = new Signature
                {
                    FilledFormId = formId,
                    SignatureImage = request.SignatureImage,
                    SignedBy = request.SignedBy,
                    SignedDate = request.SignedDate,
                    Comments = request.Comments
                };

                _context.Signatures.Add(signature);

                // CLAVE: Actualizar FirmasData del formulario con la firma realizada
                UpdateFirmasDataWithSignature(form, request.SignatureImage, request.SignedBy, request.SignedDate);

                // Guardar firma y cambios al formulario ANTES de crear alertas
                await _context.SaveChangesAsync();

                // Crear alertas para firmantes pendientes
                await CreateSignatureAlertsForPendingSigners(form, request.SignedBy);

                

                // Enviar notificacion por email (en background, no bloquea)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendSignatureNotification(form, request.SignedBy);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogWarning(emailEx, "No se pudo enviar notificacion de firma para formulario {FormId}", formId);
                    }
                });

                return Ok(new
                {
                    success = true,
                    message = "Formulario firmado exitosamente",
                    signatureId = signature.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar formulario {FormId}", formId);
                return StatusCode(500, new { message = "Error al firmar formulario" });
            }
        }

        // POST /api/Signatures/sign-multiple
        [HttpPost("sign-multiple")]
        public async Task<ActionResult> SignMultipleForms([FromBody] SignMultipleFormsRequest request)
        {
            try
            {
                int signedCount = 0;
                int failedCount = 0;
                var signedFormNames = new List<string>();

                foreach (var formId in request.FormIds)
                {
                    try
                    {
                        var form = await _context.FilledForms
                            .Include(f => f.Template)
                            .FirstOrDefaultAsync(f => f.FormID == formId);
                        if (form == null)
                        {
                            failedCount++;
                            continue;
                        }

                        var existingSignature = await _context.Signatures
                            .FirstOrDefaultAsync(s => s.FilledFormId == formId);

                        if (existingSignature != null)
                        {
                            failedCount++;
                            continue;
                        }

                        var signature = new Signature
                        {
                            FilledFormId = formId,
                            SignatureImage = request.SignatureImage,
                            SignedBy = request.SignedBy,
                            SignedDate = request.SignedDate,
                            Comments = request.Comments
                        };

                        _context.Signatures.Add(signature);

                        // CLAVE: Actualizar FirmasData del formulario
                        UpdateFirmasDataWithSignature(form, request.SignatureImage, request.SignedBy, request.SignedDate);
                            await CreateSignatureAlertsForPendingSigners(form, request.SignedBy);
                            
                        signedFormNames.Add(form.Template?.Nombre ?? $"Formulario #{formId}");
                    }
                    catch
                    {
                        failedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                // Enviar notificacion resumen
                if (signedCount > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SendBulkSignatureNotification(signedFormNames, request.SignedBy, signedCount);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogWarning(emailEx, "No se pudo enviar notificacion de firma masiva");
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = $"{signedCount} formularios firmados exitosamente",
                    signedCount,
                    failedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al firmar multiples formularios");
                return StatusCode(500, new { message = "Error al firmar formularios" });
            }
        }

        // GET /api/Signatures/history/{formId}
        [HttpGet("history/{formId}")]
        public async Task<ActionResult<IEnumerable<Signature>>> GetSignatureHistory(int formId)
        {
            try
            {
                var signatures = await _context.Signatures
                    .Where(s => s.FilledFormId == formId)
                    .OrderByDescending(s => s.SignedDate)
                    .Select(s => new
                    {
                        id = s.Id,
                        signedBy = s.SignedBy,
                        signedDate = s.SignedDate,
                        comments = s.Comments,
                        isModifiedBySGI = s.IsModifiedBySGI,
                        originalSignedDate = s.OriginalSignedDate
                    })
                    .ToListAsync();

                return Ok(signatures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de firmas");
                return StatusCode(500, new { message = "Error al obtener historial" });
            }
        }

        // GET /api/Signatures/stats
        [HttpGet("stats")]
        public async Task<ActionResult<SignatureStatsResponse>> GetStats()
        {
            try
            {
                var todayLocal = DateTime.Today;
                var todayUtc = DateTime.Now.Date;

                // Obtener formularios sin firma en tabla Signatures y verificar si FirmasData tiene firmas completas
                var formsWithoutSignature = await _context.FilledForms
                    .Where(f => !_context.Signatures.Any(s => s.FilledFormId == f.FormID))
                    .Select(f => f.FirmasData)
                    .ToListAsync();

                var pendingCount = formsWithoutSignature.Count(fd => !AllFirmasCompleted(fd));

                var stats = new SignatureStatsResponse
                {
                    PendingCount = pendingCount,

                    SignedToday = await _context.Signatures
                        .Where(s => s.SignedDate.Date == todayLocal || s.SignedDate.Date == todayUtc)
                        .CountAsync(),

                    TotalSigned = await _context.Signatures.CountAsync(),

                    RejectedCount = await _context.SignatureRejections.CountAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadisticas");
                return StatusCode(500, new { message = "Error al obtener estadisticas" });
            }
        }

        // GET /api/Signatures/puestos?search=supervisor
        [HttpGet("puestos")]
        public async Task<ActionResult<IEnumerable<object>>> SearchPuestos([FromQuery] string? search = null)
        {
            try
            {
                // Obtener todos los templates para extraer puestos de FirmasData
                var templates = await _context.Templates
                    .Select(t => new { t.Firmas })
                    .ToListAsync();

                var puestosSet = new HashSet<PuestoInfo>(new PuestoInfoComparer());

                foreach (var template in templates)
                {
                    if (string.IsNullOrWhiteSpace(template.Firmas)) continue;

                    try
                    {
                        var firmas = JsonSerializer.Deserialize<List<FirmaTemplate>>(template.Firmas);
                        if (firmas != null)
                        {
                            foreach (var firma in firmas)
                            {
                                if (!string.IsNullOrWhiteSpace(firma.Puesto))
                                {
                                    puestosSet.Add(new PuestoInfo
                                    {
                                        Puesto = firma.Puesto.Trim(),
                                        NombreCompleto = firma.NombreCompleto?.Trim()
                                    });
                                }
                            }
                        }
                    }
                    catch { }
                }

                var puestos = puestosSet
                    .OrderBy(p => p.Puesto)
                    .AsEnumerable();

                // Filtrar si hay búsqueda
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchLower = search.ToLower();
                    puestos = puestos.Where(p =>
                        p.Puesto.ToLower().Contains(searchLower) ||
                        (p.NombreCompleto != null && p.NombreCompleto.ToLower().Contains(searchLower))
                    );
                }

                return Ok(puestos.Take(50)); // Límite de 50 resultados
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar puestos");
                return StatusCode(500, new { message = "Error al buscar puestos" });
            }
        }

        // POST /api/Signatures/reject/{formId}
        [HttpPost("reject/{formId}")]
        public async Task<ActionResult> RejectForm(int formId, [FromBody] RejectFormRequest request)
        {
            try
            {
                var form = await _context.FilledForms
                    .Include(f => f.Template)
                    .FirstOrDefaultAsync(f => f.FormID == formId);
                if (form == null)
                {
                    return NotFound(new { message = "Formulario no encontrado" });
                }

                // Registrar el rechazo en la tabla SignatureRejections
                var rejection = new SignatureRejection
                {
                    FilledFormId = formId,
                    RejectedBy = request.RejectedBy,
                    RejectedDate = request.RejectedDate != default ? request.RejectedDate : DateTime.Now,
                    Reason = request.Reason, // Campo OPCIONAL
                    Status = "rejected"
                };

                _context.SignatureRejections.Add(rejection);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Formulario {FormId} rechazado por {RejectedBy}. Motivo: {Reason}",
                    formId, request.RejectedBy, request.Reason ?? "Sin motivo especificado");

                return Ok(new
                {
                    success = true,
                    message = "Formulario rechazado exitosamente",
                    rejectionId = rejection.Id,
                    reason = rejection.Reason
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al rechazar formulario");
                return StatusCode(500, new { message = "Error al rechazar formulario" });
            }
        }

        // GET /api/Signatures/timing-report
        /// <summary>
        /// Reporte de tiempos de firma: muestra cuánto se demoran en firmar y motivos de rechazo
        /// </summary>
        [HttpGet("timing-report")]
        public async Task<ActionResult<SignatureTimingSummary>> GetTimingReport([FromQuery] int? days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days.Value);

                // Obtener todos los formularios con sus firmas y rechazos
                var forms = await _context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt >= cutoffDate)
                    .Select(f => new
                    {
                        f.FormID,
                        TemplateName = f.Template!.Nombre ?? "Sin nombre",
                        FormCode = f.Template!.Codigo ?? "N/A",
                        Area = f.Template.Proceso ?? f.Template.Area ?? "N/A",
                        f.CreatedAt,
                        Signature = _context.Signatures
                            .Where(s => s.FilledFormId == f.FormID)
                            .OrderByDescending(s => s.SignedDate)
                            .FirstOrDefault(),
                        Rejection = _context.SignatureRejections
                            .Where(r => r.FilledFormId == f.FormID)
                            .OrderByDescending(r => r.RejectedDate)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                var details = forms.Select(f =>
                {
                    double? hoursToSign = null;
                    string status = "pending";
                    string? timingLabel = null;

                    if (f.Signature != null)
                    {
                        hoursToSign = (f.Signature.SignedDate - f.CreatedAt).TotalHours;
                        status = "signed";
                        timingLabel = FormatTimeDifference(f.Signature.SignedDate - f.CreatedAt);
                    }
                    else if (f.Rejection != null)
                    {
                        status = "rejected";
                    }

                    return new SignatureTimingReport
                    {
                        FilledFormId = f.FormID,
                        TemplateName = f.TemplateName,
                        FormCode = f.FormCode,
                        Area = f.Area,
                        CreatedDate = f.CreatedAt,
                        SignedDate = f.Signature?.SignedDate,
                        SignedBy = f.Signature?.SignedBy,
                        HoursToSign = hoursToSign,
                        TimingLabel = timingLabel,
                        Status = status,
                        RejectionReason = f.Rejection?.Reason,
                        RejectedBy = f.Rejection?.RejectedBy,
                        RejectedDate = f.Rejection?.RejectedDate
                    };
                }).OrderByDescending(f => f.CreatedDate).ToList();

                var signedItems = details.Where(d => d.Status == "signed" && d.HoursToSign.HasValue).ToList();

                var summary = new SignatureTimingSummary
                {
                    AverageHoursToSign = signedItems.Any() ? Math.Round(signedItems.Average(s => s.HoursToSign!.Value), 2) : 0,
                    FastestHours = signedItems.Any() ? Math.Round(signedItems.Min(s => s.HoursToSign!.Value), 2) : 0,
                    SlowestHours = signedItems.Any() ? Math.Round(signedItems.Max(s => s.HoursToSign!.Value), 2) : 0,
                    TotalSigned = details.Count(d => d.Status == "signed"),
                    TotalPending = details.Count(d => d.Status == "pending"),
                    TotalRejected = details.Count(d => d.Status == "rejected"),
                    SignedWithin24h = signedItems.Count(s => s.HoursToSign < 24),
                    SignedAfter24h = signedItems.Count(s => s.HoursToSign >= 24 && s.HoursToSign < 72),
                    SignedAfter72h = signedItems.Count(s => s.HoursToSign >= 72),
                    Details = details
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de tiempos de firma");
                return StatusCode(500, new { message = "Error al generar reporte de tiempos" });
            }
        }

        // GET /api/Signatures/rejections
        /// <summary>
        /// Obtener todos los rechazos con sus motivos
        /// </summary>
        [HttpGet("rejections")]
        public async Task<ActionResult> GetRejections([FromQuery] int? days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days.Value);

                var rejections = await _context.SignatureRejections
                    .Where(r => r.RejectedDate >= cutoffDate)
                    .Join(_context.FilledForms.Include(f => f.Template),
                        r => r.FilledFormId,
                        f => f.FormID,
                        (r, f) => new
                        {
                            r.Id,
                            r.FilledFormId,
                            TemplateName = f.Template!.Nombre ?? "Sin nombre",
                            FormCode = f.Template!.Codigo ?? "N/A",
                            Area = f.Template.Proceso ?? "N/A",
                            r.RejectedBy,
                            r.RejectedDate,
                            Reason = r.Reason ?? "Sin motivo especificado",
                            r.Status
                        })
                    .OrderByDescending(r => r.RejectedDate)
                    .ToListAsync();

                return Ok(rejections);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener rechazos");
                return StatusCode(500, new { message = "Error al obtener rechazos" });
            }
        }

        private static string FormatTimeDifference(TimeSpan diff)
        {
            if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} min";
            if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours}h {diff.Minutes}min";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}d {diff.Hours}h";
            return $"{(int)diff.TotalDays} días";
        }

        // PUT /api/Signatures/update-date/{signatureId}
        [HttpPut("update-date/{signatureId}")]
        public async Task<ActionResult> UpdateSignatureDate(int signatureId, [FromBody] UpdateSignatureDateRequest request)
        {
            try
            {
                var signature = await _context.Signatures.FindAsync(signatureId);
                if (signature == null)
                {
                    return NotFound(new { message = "Firma no encontrada" });
                }

                if (!signature.IsModifiedBySGI)
                {
                    signature.OriginalSignedDate = signature.SignedDate;
                    signature.IsModifiedBySGI = true;
                }

                signature.SignedDate = request.NewDate;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Fecha de firma actualizada exitosamente"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar fecha de firma");
                return StatusCode(500, new { message = "Error al actualizar fecha" });
            }
        }

        // ===== HELPERS =====

        /// <summary>
        /// CLAVE: Actualiza el campo FirmasData del formulario con la imagen de firma.
        /// Busca el primer puesto que no tenga firma y le asigna la imagen,
        /// o si todos ya tienen firma, agrega la firma al primer puesto encontrado.
        /// Esto hace que la firma aparezca en ViewForms, PDF y Excel.
        /// </summary>
        private void UpdateFirmasDataWithSignature(FilledForm form, string signatureImage, string signedBy, DateTime signedDate)
        {
            try
            {
                var firmasDict = new Dictionary<string, JsonElement>();

                if (!string.IsNullOrEmpty(form.FirmasData))
                {
                    firmasDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.FirmasData) 
                        ?? new Dictionary<string, JsonElement>();
                }

                // Buscar el puesto que corresponde al firmante (por email o nombre)
                string targetPuesto = null;

                foreach (var kvp in firmasDict)
                {
                    // Verificar si este puesto tiene email que coincide con signedBy
                    if (kvp.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (kvp.Value.TryGetProperty("email", out var emailProp))
                        {
                            var email = emailProp.GetString();
                            if (!string.IsNullOrEmpty(email) && email.Equals(signedBy, StringComparison.OrdinalIgnoreCase))
                            {
                                targetPuesto = kvp.Key;
                                break;
                            }
                        }
                        // Tambien buscar por nombre
                        if (kvp.Value.TryGetProperty("nombre", out var nombreProp))
                        {
                            var nombre = nombreProp.GetString();
                            if (!string.IsNullOrEmpty(nombre) && nombre.Equals(signedBy, StringComparison.OrdinalIgnoreCase))
                            {
                                targetPuesto = kvp.Key;
                                break;
                            }
                        }
                    }
                }

                // Si no encontramos por email/nombre, buscar el primer puesto sin firma
                if (targetPuesto == null)
                {
                    foreach (var kvp in firmasDict)
                    {
                        if (kvp.Value.ValueKind == JsonValueKind.Object)
                        {
                            bool hasFirma = false;
                            if (kvp.Value.TryGetProperty("firma", out var firmaObj))
                            {
                                if (firmaObj.ValueKind == JsonValueKind.Object)
                                {
                                    if (firmaObj.TryGetProperty("url", out var urlProp) && !string.IsNullOrEmpty(urlProp.GetString()))
                                        hasFirma = true;
                                    else if (firmaObj.TryGetProperty("base64", out var b64Prop) && !string.IsNullOrEmpty(b64Prop.GetString()))
                                        hasFirma = true;
                                }
                            }

                            if (!hasFirma)
                            {
                                targetPuesto = kvp.Key;
                                break;
                            }
                        }
                    }
                }

                // Si aun no hay target, usar el primer puesto disponible
                if (targetPuesto == null && firmasDict.Count > 0)
                {
                    targetPuesto = firmasDict.Keys.First();
                }

                // Si no hay ningun puesto en firmasData, crear uno generico
                if (targetPuesto == null)
                {
                    targetPuesto = "Aprobado por";
                }

                // Construir el nuevo objeto de firma para ese puesto
                var existingData = firmasDict.ContainsKey(targetPuesto) ? firmasDict[targetPuesto] : default;

                string existingNombre = signedBy;
                string existingEmail = signedBy;
                string existingFecha = signedDate.ToString("yyyy-MM-dd");
                string existingHora = signedDate.ToString("HH:mm");

                if (existingData.ValueKind == JsonValueKind.Object)
                {
                    if (existingData.TryGetProperty("nombre", out var n) && !string.IsNullOrEmpty(n.GetString()))
                        existingNombre = n.GetString()!;
                    if (existingData.TryGetProperty("email", out var e) && !string.IsNullOrEmpty(e.GetString()))
                        existingEmail = e.GetString()!;
                    if (existingData.TryGetProperty("fecha", out var f) && !string.IsNullOrEmpty(f.GetString()))
                        existingFecha = f.GetString()!;
                }

                // Crear el nuevo objeto con la firma incluida
                var updatedPuesto = new Dictionary<string, object>
                {
                    ["nombre"] = existingNombre,
                    ["email"] = existingEmail,
                    ["fecha"] = existingFecha,
                    ["hora"] = existingHora,
                    ["fechaHoraCapturada"] = true,
                    ["firma"] = new Dictionary<string, string>
                    {
                        ["base64"] = signatureImage,
                        ["url"] = "",
                        ["provider"] = "signature-management"
                    }
                };

                // Reconstruir todo el firmasData
                var newFirmasDict = new Dictionary<string, object>();
                foreach (var kvp in firmasDict)
                {
                    if (kvp.Key == targetPuesto)
                    {
                        newFirmasDict[kvp.Key] = updatedPuesto;
                    }
                    else
                    {
                        newFirmasDict[kvp.Key] = kvp.Value;
                    }
                }

                // Si el targetPuesto no existia, agregarlo
                if (!newFirmasDict.ContainsKey(targetPuesto))
                {
                    newFirmasDict[targetPuesto] = updatedPuesto;
                }

                form.FirmasData = JsonSerializer.Serialize(newFirmasDict);
                _logger.LogInformation("FirmasData actualizado para formulario {FormId}, puesto: {Puesto}", form.FormID, targetPuesto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo actualizar FirmasData para formulario {FormId}", form.FormID);
            }
        }

        /// <summary>
        /// Verifica si TODAS las firmas en FirmasData ya están completadas (tienen imagen firma.url o firma.base64).
        /// Si FirmasData está vacío o no se puede parsear, retorna false.
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

                    // Verificar si tiene un objeto "firma" con "url" o "base64"
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

                return true; // Todas las firmas tienen imagen
            }
            catch
            {
                return false;
            }
        }

        private static string ExtractCreatedBy(string? headerData, string? firmasData)
        {
            if (!string.IsNullOrEmpty(headerData))
            {
                try
                {
                    var header = JsonSerializer.Deserialize<JsonElement>(headerData);
                    foreach (var prop in new[] { "elaborado_por", "elaboradoPor", "creado_por", "creadoPor", "responsable", "usuario", "operador", "filledBy" })
                    {
                        if (header.TryGetProperty(prop, out var val))
                        {
                            var value = val.GetString();
                            if (!string.IsNullOrEmpty(value)) return value;
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(firmasData))
            {
                try
                {
                    var firmas = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firmasData);
                    if (firmas != null)
                    {
                        foreach (var kvp in firmas)
                        {
                            if (kvp.Value.TryGetProperty("nombre", out var nombre))
                            {
                                var value = nombre.GetString();
                                if (!string.IsNullOrEmpty(value)) return value;
                            }
                        }
                    }
                }
                catch { }
            }

            return "No registrado";
        }

        private static List<string> ExtractFirmanteEmails(string? firmasData)
        {
            var emails = new List<string>();
            if (string.IsNullOrEmpty(firmasData)) return emails;

            try
            {
                var firmas = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firmasData);
                if (firmas == null) return emails;

                foreach (var kvp in firmas)
                {
                    if (kvp.Value.TryGetProperty("email", out var emailProp))
                    {
                        var email = emailProp.GetString();
                        if (!string.IsNullOrEmpty(email) && email.Contains("@"))
                        {
                            emails.Add(email);
                        }
                    }
                }
            }
            catch { }

            return emails;
        }

        private async Task SendSignatureNotification(FilledForm form, string signedBy)
        {
            var recipientEmails = ExtractFirmanteEmails(form.FirmasData);

            if (recipientEmails.Count == 0)
            {
                var config = await _context.Set<AlertConfiguration>().FirstOrDefaultAsync();
                if (config != null && !string.IsNullOrEmpty(config.SignatureRecipients))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<List<string>>(config.SignatureRecipients);
                        if (parsed != null) recipientEmails.AddRange(parsed);
                    }
                    catch { }
                }
            }

            if (recipientEmails.Count == 0) return;

            var templateName = form.Template?.Nombre ?? "Formulario";
            var templateCode = form.Template?.Codigo ?? "N/A";
            var subject = $"Firma Realizada - {templateCode} - {templateName}";
            var body = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background: linear-gradient(135deg, #1e40af, #3b82f6); padding: 20px; border-radius: 8px 8px 0 0;'>
        <h2 style='color: white; margin: 0;'>Firma Realizada</h2>
        <p style='color: #dbeafe; margin: 5px 0 0 0;'>Sistema de Gestion Frigolab</p>
    </div>
    <div style='padding: 20px; border: 1px solid #e5e7eb; border-top: none;'>
        <p>Se ha registrado una nueva firma en el sistema:</p>
        <table style='width: 100%; border-collapse: collapse; margin: 15px 0;'>
            <tr style='background: #f3f4f6;'>
                <td style='padding: 10px; border: 1px solid #e5e7eb; font-weight: bold;'>Formulario</td>
                <td style='padding: 10px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(templateName)}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e5e7eb; font-weight: bold;'>Codigo</td>
                <td style='padding: 10px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(templateCode)}</td>
            </tr>
            <tr style='background: #f3f4f6;'>
                <td style='padding: 10px; border: 1px solid #e5e7eb; font-weight: bold;'>Firmado por</td>
                <td style='padding: 10px; border: 1px solid #e5e7eb;'>{System.Net.WebUtility.HtmlEncode(signedBy)}</td>
            </tr>
            <tr>
                <td style='padding: 10px; border: 1px solid #e5e7eb; font-weight: bold;'>Fecha</td>
                <td style='padding: 10px; border: 1px solid #e5e7eb;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td>
            </tr>
        </table>
        <p style='color: #6b7280; font-size: 12px;'>Este es un correo automatico del sistema de Frigolab.</p>
    </div>
</body>
</html>";

            foreach (var email in recipientEmails.Distinct())
            {
                try
                {
                    await _emailService.SendAlertEmailAsync(email, subject, body);
                    _logger.LogInformation("Notificacion de firma enviada a {Email} para formulario {FormId}", email, form.FormID);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al enviar notificacion a {Email}", email);
                }
            }
        }

        private async Task SendBulkSignatureNotification(List<string> formNames, string signedBy, int count)
        {
            var config = await _context.Set<AlertConfiguration>().FirstOrDefaultAsync();
            if (config == null) return;

            var recipientEmails = new List<string>();
            if (!string.IsNullOrEmpty(config.SignatureRecipients))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(config.SignatureRecipients);
                    if (parsed != null) recipientEmails.AddRange(parsed);
                }
                catch { }
            }

            if (recipientEmails.Count == 0) return;

            var formListHtml = string.Join("", formNames.Select(n =>
                $"<li style='padding: 4px 0;'>{System.Net.WebUtility.HtmlEncode(n)}</li>"));

            var subject = $"Firma Masiva - {count} formularios firmados";
            var body = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background: linear-gradient(135deg, #1e40af, #3b82f6); padding: 20px; border-radius: 8px 8px 0 0;'>
        <h2 style='color: white; margin: 0;'>Firma Masiva Realizada</h2>
        <p style='color: #dbeafe; margin: 5px 0 0 0;'>Sistema de Gestion Frigolab</p>
    </div>
    <div style='padding: 20px; border: 1px solid #e5e7eb; border-top: none;'>
        <p><strong>{signedBy}</strong> ha firmado <strong>{count}</strong> formularios:</p>
        <ul style='margin: 10px 0;'>{formListHtml}</ul>
        <p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>
        <p style='color: #6b7280; font-size: 12px;'>Este es un correo automatico del sistema de Frigolab.</p>
    </div>
</body>
</html>";

            foreach (var email in recipientEmails.Distinct())
            {
                try
                {
                    await _emailService.SendAlertEmailAsync(email, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error al enviar notificacion masiva a {Email}", email);
                }
            }
        }

        /// <summary>
        /// Crea alertas en la tabla Alerts para cada firmante pendiente
        /// </summary>
      private async Task CreateSignatureAlertsForPendingSigners(FilledForm form, string justSignedBy)
{
    try
    {
        if (string.IsNullOrEmpty(form.FirmasData))
        {
            _logger.LogWarning("FirmasData vacío para formulario {FormId}", form.FormID);
            return;
        }

        var firmasDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.FirmasData);
        if (firmasDict == null || firmasDict.Count == 0)
        {
            _logger.LogWarning("No se pudo parsear FirmasData del formulario {FormId}", form.FormID);
            return;
        }

        var templateName = form.Template?.Nombre ?? "Formulario";
        var formCode = form.Template?.Codigo ?? "N/A";

        // Cargar catálogo de firmas para buscar emails de reemplazos
        var catalogo = await _context.CatalogoFirmas.Where(c => c.Activo).ToListAsync();

        // Parsear las firmas del TEMPLATE para obtener reemplazos configurados por puesto
        var templateFirmas = new List<FirmaAlertInfo>();
        if (!string.IsNullOrEmpty(form.Template?.Firmas))
        {
            try
            {
                templateFirmas = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(form.Template.Firmas,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<FirmaAlertInfo>();
            }
            catch { }
        }

        _logger.LogInformation("📋 Procesando alertas y emails para formulario {FormId} ({FormCode}). Total puestos: {Count}", form.FormID, formCode, firmasDict.Count);

        foreach (var kvp in firmasDict)
        {
            string puesto = kvp.Key;
            var firmaData = kvp.Value;

            if (firmaData.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("  ⚠️ Puesto {Puesto} no es un objeto JSON válido", puesto);
                continue;
            }

            // Extraer email Y NOMBRE del usuario ASIGNADO a este puesto específico
            string? targetEmail = null;
            string? targetName = null;
            
            if (firmaData.TryGetProperty("email", out var emailProp))
            {
                targetEmail = emailProp.GetString();
            }

            if (firmaData.TryGetProperty("nombre", out var nombreProp))
            {
                targetName = nombreProp.GetString();
            }

            // Fallback: buscar en nombre si contiene @
            if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName) && targetName.Contains("@"))
            {
                targetEmail = targetName;
            }

            // Verificar si este puesto ya tiene firma digital (imagen)
            bool yaFirmo = false;
            if (firmaData.TryGetProperty("firma", out var firmaObj) && firmaObj.ValueKind == JsonValueKind.Object)
            {
                bool tieneUrl = firmaObj.TryGetProperty("url", out var urlProp) && !string.IsNullOrEmpty(urlProp.GetString());
                bool tieneBase64 = firmaObj.TryGetProperty("base64", out var b64Prop) && !string.IsNullOrEmpty(b64Prop.GetString());
                yaFirmo = tieneUrl || tieneBase64;
            }

            // Si ya firmó este puesto, NO crear alerta ni enviar email
            if (yaFirmo)
            {
                _logger.LogInformation("  ✅ Puesto {Puesto}: YA FIRMÓ", puesto);
                continue;
            }

            _logger.LogInformation("  ⏳ Puesto {Puesto}: Pendiente de firma", puesto);

            // Recopilar TODOS los emails a notificar para este puesto: titular + reemplazos
            var emailsParaPuesto = new List<(string email, string nombre, bool esSuplente)>();

            // 1) Titular
            if (!string.IsNullOrEmpty(targetEmail))
            {
                emailsParaPuesto.Add((targetEmail, targetName ?? "Usuario", false));
            }

            // 2) Reemplazos/Suplentes: buscar en el template la config de este puesto
            var templateFirma = templateFirmas.FirstOrDefault(f =>
                (f.Puesto ?? "").Equals(puesto, StringComparison.OrdinalIgnoreCase));
            if (templateFirma?.Reemplazos != null)
            {
                foreach (var reemplazoNombre in templateFirma.Reemplazos)
                {
                    if (string.IsNullOrWhiteSpace(reemplazoNombre)) continue;

                    // Buscar email del reemplazo en el catálogo de firmas
                    var entry = catalogo.FirstOrDefault(c =>
                        (c.NombreCompleto ?? "").Equals(reemplazoNombre, StringComparison.OrdinalIgnoreCase));
                    var reemplazoEmail = entry?.Correo;

                    if (!string.IsNullOrWhiteSpace(reemplazoEmail))
                    {
                        emailsParaPuesto.Add((reemplazoEmail, reemplazoNombre, true));
                        _logger.LogInformation("  👥 Suplente encontrado para {Puesto}: {Nombre} ({Email})", puesto, reemplazoNombre, reemplazoEmail);
                    }
                    else
                    {
                        _logger.LogWarning("  ⚠️ Suplente {Nombre} para {Puesto} no tiene correo en el catálogo", reemplazoNombre, puesto);
                    }
                }
            }

            if (emailsParaPuesto.Count == 0)
            {
                _logger.LogWarning("  ⚠️ Puesto {Puesto}: No se encontró email de titular ni suplentes", puesto);
                continue;
            }

            // Enviar alerta y email a cada destinatario (titular + suplentes)
            foreach (var (email, nombre, esSuplente) in emailsParaPuesto)
            {
                // Verificar si ya existe una alerta pendiente (evitar duplicados)
                var existingAlert = await _context.Set<Alert>()
                    .FirstOrDefaultAsync(a =>
                        a.FormId == form.FormID &&
                        a.TargetEmail == email &&
                        a.Type == "signature" &&
                        a.Status == "pending");

                if (existingAlert != null)
                {
                    _logger.LogInformation("  ℹ️ Ya existe alerta pendiente para {Email} en formulario {FormId}", email, form.FormID);
                    continue;
                }

                var rolTexto = esSuplente ? $"(Suplente de {targetName ?? puesto})" : "";
                var alert = new Alert
                {
                    Type = "signature",
                    Priority = "high",
                    Title = $"Firma requerida: {templateName}",
                    Message = $"El formulario {formCode} ({templateName}) requiere firma en el puesto: {puesto}. {rolTexto} Por favor revisa y firma el formulario lo antes posible.",
                    TargetEmail = email,
                    FormId = form.FormID,
                    FormCode = formCode,
                    CreatedDate = DateTime.Now,
                    IsRead = false,
                    Status = "pending"
                };

                _context.Set<Alert>().Add(alert);
                _logger.LogInformation("  ✅ ALERTA CREADA para {Email} ({Nombre}) {Rol} en puesto {Puesto}", email, nombre, esSuplente ? "[SUPLENTE]" : "[TITULAR]", puesto);

                // 📧 ENVIAR EMAIL DE NOTIFICACIÓN
                try
                {
                    var saludoTexto = esSuplente
                        ? $"Hola {nombre}, como suplente de <strong>{targetName ?? puesto}</strong>, se requiere tu firma:"
                        : $"Se requiere tu firma digital en el siguiente formulario:";
                    var emailSubject = esSuplente
                        ? $"✍️ Firma Requerida (Suplente) - {templateName}"
                        : $"✍️ Firma Requerida - {templateName}";
                    var emailBody = $@"
                    <html>
                    <head>
                        <style>
                            body {{ margin: 0; padding: 0; font-family: Arial, sans-serif; }}
                            .container {{ max-width: 600px; margin: 0 auto; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                        padding: 30px; 
                                        border-radius: 10px; 
                                        color: white; 
                                        margin-bottom: 20px;'>
                                <h1 style='margin: 0;'>✍️ Firma Requerida</h1>
                                <p style='margin: 10px 0 0 0; font-size: 18px;'>Sistema de Gestión Frigolab</p>
                            </div>
                            
                            <div style='background: #f8f9fa; 
                                        padding: 20px; 
                                        border-radius: 10px; 
                                        margin-bottom: 20px;'>
                                <h2 style='color: #333; margin-top: 0;'>Hola {nombre},</h2>
                                <p style='color: #555; font-size: 16px; line-height: 1.6;'>
                                    {saludoTexto}
                                </p>
                                
                                <table style='width: 100%; 
                                            margin: 20px 0; 
                                            background: white; 
                                            border-radius: 8px; 
                                            overflow: hidden;
                                            box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
                                    <tr style='background: #667eea; color: white;'>
                                        <td style='padding: 12px; font-weight: bold; width: 40%;'>Formulario</td>
                                        <td style='padding: 12px;'>{templateName}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; 
                                                   border-bottom: 1px solid #ddd; 
                                                   font-weight: bold;'>Código</td>
                                        <td style='padding: 12px; 
                                                   border-bottom: 1px solid #ddd;'>{formCode}</td>
                                    </tr>
                                    <tr style='background: #f8f9fa;'>
                                        <td style='padding: 12px; 
                                                   border-bottom: 1px solid #ddd; 
                                                   font-weight: bold;'>Puesto</td>
                                        <td style='padding: 12px; 
                                                   border-bottom: 1px solid #ddd;'>{puesto}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 12px; font-weight: bold;'>Fecha de solicitud</td>
                                        <td style='padding: 12px;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td>
                                    </tr>
                                </table>
                                
                                <div style='margin: 30px 0; text-align: center;'>
                                    <a href='http://localhost:5173/signatures' 
                                       style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                              color: white; 
                                              padding: 15px 40px; 
                                              text-decoration: none; 
                                              border-radius: 8px; 
                                              font-size: 16px; 
                                              font-weight: bold;
                                              display: inline-block;
                                              box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);'>
                                        ✍️ Ir a Firmar Ahora
                                    </a>
                                </div>
                                
                                <p style='color: #777; font-size: 14px; margin-top: 20px;'>
                                    <strong>Nota:</strong> Por favor firma este formulario lo antes posible para 
                                    completar el proceso de aprobación.
                                </p>
                            </div>
                            
                            <div style='color: #999; 
                                        font-size: 12px; 
                                        text-align: center; 
                                        margin-top: 30px; 
                                        padding: 20px;
                                        border-top: 1px solid #ddd;'>
                                <p style='margin: 5px 0;'>Este es un mensaje automático del Sistema de Gestión Frigolab.</p>
                                <p style='margin: 5px 0;'>Por favor no responder a este correo.</p>
                                <p style='margin: 5px 0; color: #bbb;'>© 2026 Frigolab - Todos los derechos reservados</p>
                            </div>
                        </div>
                    </body>
                    </html>
                ";

                    await _emailService.SendAlertEmailAsync(email, emailSubject, emailBody);
                    _logger.LogInformation("  📧 EMAIL ENVIADO a {Email} ({Name}) {Rol}", email, nombre, esSuplente ? "[SUPLENTE]" : "[TITULAR]");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "  ❌ Error al enviar email a {Email}", email);
                }
            }
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("📨 Alertas guardadas y emails enviados para formulario {FormId}", form.FormID);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Error al crear alertas y enviar emails para formulario {FormId}", form.FormID);
    }
}

    }

    // Clases auxiliares para el endpoint de búsqueda de puestos
    public class FirmaTemplate
    {
        public string Puesto { get; set; } = string.Empty;
        public string? NombreCompleto { get; set; }
    }

    public class PuestoInfo
    {
        public string Puesto { get; set; } = string.Empty;
        public string? NombreCompleto { get; set; }
    }

    public class PuestoInfoComparer : IEqualityComparer<PuestoInfo>
    {
        public bool Equals(PuestoInfo? x, PuestoInfo? y)
        {
            if (x == null || y == null) return false;
            return string.Equals(x.Puesto, y.Puesto, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(PuestoInfo obj)
        {
            return obj.Puesto.ToLower().GetHashCode();
        }
    }
}
