using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services; // ✅ Para IEmailService
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilledFormsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FilledFormsController> _logger; // ✅ Para logging
        private readonly IEmailService _emailService; // ✅ Para enviar emails

        public FilledFormsController(
            ApplicationDbContext context,
            ILogger<FilledFormsController> logger,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetFilledForms()
        {
            var forms = await _context.FilledForms
                                 .Include(f => f.Template)
                                 .OrderByDescending(f => f.CreatedAt)
                                 .ToListAsync();
            
            // Mapear a objeto anónimo con el nombre del template y datos de auditoría
            var result = forms.Select(f => new
            {
                f.FormID,
                f.TemplateID,
                TemplateName = f.Template?.Nombre ?? "Sin nombre",
                f.TemplateVersion,
                f.FechaVersion,
                
                // ✅ AUDITORÍA
                f.FilledBy,
                f.FilledByEmail,
                f.FilledByRole,
                
                // 🦐🐟 TIPO DE PRODUCTO
                f.TipoProducto,
                
                f.HeaderData,
                f.BodyData,
                f.FirmasData,
                f.Observaciones,
                f.CreatedAt,
                f.UpdatedAt
            });
            
            return Ok(result);
        }

        // 🆕 ENDPOINT LIGERO: Lista de formularios sin datos pesados (para importador de columnas)
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<object>>> GetFilledFormsList()
        {
            var forms = await _context.FilledForms
                                 .Include(f => f.Template)
                                 .OrderByDescending(f => f.CreatedAt)
                                 .Take(100)
                                 .Select(f => new
                                 {
                                     f.FormID,
                                     f.TemplateID,
                                     TemplateName = f.Template != null ? f.Template.Nombre : "Sin nombre",
                                     f.TipoProducto,
                                     f.CreatedAt,
                                     f.UpdatedAt,
                                     f.FilledBy
                                 })
                                 .ToListAsync();
            
            return Ok(forms);
        }

        // 🆕 NUEVO ENDPOINT: Obtener datos simples y parseados para importación
        [HttpGet("{id}/simple")]
        public async Task<ActionResult<object>> GetFilledFormSimple(int id)
        {
            Console.WriteLine($"📥 GetFilledFormSimple: Buscando formulario ID={id}");
            
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                Console.WriteLine($"❌ Formulario ID={id} no encontrado");
                return NotFound(new { message = $"Formulario {id} no encontrado" });
            }

            Console.WriteLine($"✅ Formulario encontrado: ID={id}, TemplateID={filledForm.TemplateID}");

            // Parsear HeaderData y BodyData directamente
            object? headerDataParsed = null;
            object? bodyDataParsed = null;

            try {
                if (!string.IsNullOrEmpty(filledForm.HeaderData)) {
                    headerDataParsed = JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                }
            } catch {
                headerDataParsed = new { raw = filledForm.HeaderData };
            }

            try {
                if (!string.IsNullOrEmpty(filledForm.BodyData)) {
                    bodyDataParsed = JsonSerializer.Deserialize<object>(filledForm.BodyData);
                }
            } catch {
                bodyDataParsed = new { raw = filledForm.BodyData };
            }

            // Parsear BodyElements del TemplateSnapshot para obtener títulos de tablas
            object? templateBodyElements = null;
            try {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot)) {
                    using var snapshotDoc = JsonDocument.Parse(filledForm.TemplateSnapshot);
                    if (snapshotDoc.RootElement.TryGetProperty("BodyElements", out var bodyElProp)) {
                        var bodyElStr = bodyElProp.GetString();
                        if (!string.IsNullOrEmpty(bodyElStr)) {
                            templateBodyElements = JsonSerializer.Deserialize<object>(bodyElStr);
                        }
                    }
                }
            } catch {
                // Si falla, intentar con la plantilla directamente
                try {
                    if (filledForm.Template != null && !string.IsNullOrEmpty(filledForm.Template.BodyElements)) {
                        templateBodyElements = JsonSerializer.Deserialize<object>(filledForm.Template.BodyElements);
                    }
                } catch { }
            }

            // Extraer BodyElements RAW del TemplateSnapshot como string
            string? templateBodyElementsRaw = null;
            try {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot)) {
                    using var snapshotDoc2 = JsonDocument.Parse(filledForm.TemplateSnapshot);
                    if (snapshotDoc2.RootElement.TryGetProperty("BodyElements", out var bodyElProp2)) {
                        templateBodyElementsRaw = bodyElProp2.GetString();
                    }
                }
            } catch {
                try {
                    if (filledForm.Template != null) {
                        templateBodyElementsRaw = filledForm.Template.BodyElements;
                    }
                } catch { }
            }

            // Devolver datos simples y directos
            // bodyDataRaw y templateBodyElementsRaw son strings puros que NO pasan por ReferenceHandler.Preserve
            var response = new {
                formID = filledForm.FormID,
                templateID = filledForm.TemplateID,
                templateName = filledForm.Template?.Nombre ?? "Sin nombre",
                templateVersion = filledForm.TemplateVersion,
                headerData = headerDataParsed,
                bodyData = bodyDataParsed,
                bodyDataRaw = filledForm.BodyData ?? "",
                templateBodyElements = templateBodyElements,
                templateBodyElementsRaw = templateBodyElementsRaw ?? "",
                createdAt = filledForm.CreatedAt,
                updatedAt = filledForm.UpdatedAt
            };

            Console.WriteLine($"📤 Retornando datos simples para FormID={id}");
            return Ok(response);
        }
[HttpGet("erp-report")]
public async Task<ActionResult<IEnumerable<object>>> GetErpReport(
    [FromQuery] DateTime? inicio, 
    [FromQuery] DateTime? fin,
    [FromQuery] int? templateId,
    [FromQuery] string? lote)
{
    try 
    {
        // 1. Iniciar consulta
        var query = _context.FilledForms
            .Include(f => f.Template)
            .AsNoTracking()
            .AsQueryable();

        // 2. Filtros
        if (inicio.HasValue) query = query.Where(f => f.CreatedAt >= inicio.Value);
        if (fin.HasValue) 
        {
            var fechaFin = fin.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            query = query.Where(f => f.CreatedAt <= fechaFin);
        }
        if (templateId.HasValue && templateId.Value > 0)
        {
            query = query.Where(f => f.TemplateID == templateId.Value);
        }
        if (!string.IsNullOrEmpty(lote))
        {
            query = query.Where(f => f.HeaderData.Contains(lote) || f.BodyData.Contains(lote));
        }

        // 3. Ejecutar
        var forms = await query
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        // 4. Mapeo COMPLETO (Incluyendo Firmas y Observaciones)
        var result = forms.Select(f => new
        {
            formID = f.FormID,
            templateID = f.TemplateID,
            templateName = f.Template?.Nombre ?? "Sin nombre",
            createdAt = f.CreatedAt,
            headerData = f.HeaderData, 
            bodyData = f.BodyData,
            // 👇 ESTOS SON LOS CAMPOS QUE FALTABAN 👇
            firmasData = f.FirmasData,      
            observaciones = f.Observaciones 
        });

        return Ok(result);
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Error interno: {ex.Message}");
    }
}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetFilledForm(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound();
            }

            Console.WriteLine($"📋 GetFilledForm ID={id}, CreatedAt={filledForm.CreatedAt:yyyy-MM-dd}, TemplateID={filledForm.TemplateID}");

            // 🎯 LÓGICA DE VERSIONAMIENTO POR FECHA
            object? templateData = null;
            string versionUsada = filledForm.TemplateVersion ?? "1";
            bool versionCorrecta = false;

            // 1️⃣ Determinar qué versión DEBERÍA usar según la fecha de creación
            if (filledForm.CreatedAt != default(DateTime))
            {
                string versionVigente = await GetVersionVigenteEnFecha(filledForm.TemplateID, filledForm.CreatedAt);
                Console.WriteLine($"🔍 Versión vigente en {filledForm.CreatedAt:yyyy-MM-dd}: {versionVigente}");

                // 2️⃣ Si la versión guardada NO coincide con la vigente, buscar la estructura correcta
                if (filledForm.TemplateVersion != versionVigente)
                {
                    Console.WriteLine($"⚠️ Versión guardada ({filledForm.TemplateVersion}) != Versión vigente ({versionVigente})");
                    templateData = await GetTemplateStructureByVersion(filledForm.TemplateID, versionVigente);
                    versionUsada = versionVigente;
                    versionCorrecta = false; // Indica que se corrigió la versión
                }
                else
                {
                    Console.WriteLine($"✅ Versión guardada coincide con la vigente");
                    versionCorrecta = true;
                }
            }

            // 3️⃣ Si no se pudo determinar por fecha, usar snapshot o template actual
            if (templateData == null)
            {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
                {
                    try
                    {
                        templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
                        Console.WriteLine($"📸 Usando TemplateSnapshot");
                    }
                    catch
                    {
                        templateData = GetCurrentTemplateData(filledForm.Template);
                        Console.WriteLine($"⚠️ Fallback a template actual (error deserialización)");
                    }
                }
                else
                {
                    templateData = GetCurrentTemplateData(filledForm.Template);
                    Console.WriteLine($"⚠️ Fallback a template actual (sin snapshot)");
                }
            }

            // 4️⃣ Crear respuesta con metadata de versión
            var response = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion, // Versión GUARDADA
                VersionUsada = versionUsada, // Versión REALMENTE usada
                VersionCorrecta = versionCorrecta, // TRUE si la guardada coincide con la vigente
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                HeaderData = filledForm.HeaderData,
                BodyData = filledForm.BodyData,
                FirmasData = filledForm.FirmasData,
                Observaciones = filledForm.Observaciones,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = templateData,
                IsHistorical = !string.IsNullOrEmpty(filledForm.TemplateSnapshot)
            };

            return Ok(response);
        }

        [HttpGet("{id}/edit")]
        public async Task<ActionResult<object>> GetFilledFormForEdit(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound(new { message = "Formulario no encontrado" });
            }

            // VERSIONAMIENTO: Para edición, SIEMPRE usar el snapshot guardado
            object? templateData = null;
            
            if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
            {
                // Usar el snapshot histórico
                try
                {
                    templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
                }
                catch
                {
                    // Fallback si falla la deserialización
                    templateData = GetCurrentTemplateData(filledForm.Template);
                }
            }
            else
            {
                // Fallback para datos antiguos
                templateData = GetCurrentTemplateData(filledForm.Template);
            }

            // Preparar datos para edición - asegurar que los JSON strings estén correctos
            var editData = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion, // Versión guardada
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                HeaderData = !string.IsNullOrEmpty(filledForm.HeaderData) ? filledForm.HeaderData : "{}",
                BodyData = !string.IsNullOrEmpty(filledForm.BodyData) ? filledForm.BodyData : "{}",
                FirmasData = !string.IsNullOrEmpty(filledForm.FirmasData) ? filledForm.FirmasData : "{}",
                Observaciones = filledForm.Observaciones ?? "",
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = templateData, // Usar snapshot histórico
                IsHistorical = !string.IsNullOrEmpty(filledForm.TemplateSnapshot)
            };

            return Ok(editData);
        }

       [HttpPost]
        public async Task<ActionResult<FilledForm>> PostFilledForm([FromBody] FilledFormInputDto dto)
        {
            // Obtener el template completo para crear el snapshot
            var template = await _context.Templates.FindAsync(dto.TemplateID);
            if (template == null)
            {
                return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
            }

            // VERSIONAMIENTO: Crear snapshot del template al momento de creación
            var templateSnapshot = new
            {
                TemplateID = template.TemplateID,
                Codigo = template.Codigo,
                Nombre = template.Nombre,
                Version = template.Version,
                FechaVersion = template.FechaVersion,
                Objetivo = template.Supervisa,
                Proceso = template.Proceso,
                CuandoSeUsa = template.CuandoSeUsa,
                QuienLoLlena = template.QuienLoLlena,
                HeaderFields = template.HeaderFields,
                BodyElements = template.BodyElements,
                Firmas = template.Firmas,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
            
            // Mapeo actualizado para usar BodyData y guardar snapshot
            var filledForm = new FilledForm
            {
                TemplateID = dto.TemplateID,
                TemplateVersion = template.Version, // Guardar la versión específica usada
                TemplateSnapshot = JsonSerializer.Serialize(templateSnapshot), // Guardar snapshot completo
                FechaVersion = DateTime.Now, // ✅ Hora local del servidor
                
                // ✅ AUDITORÍA: Guardar quién creó el formulario
                FilledBy = dto.FilledBy,
                FilledByEmail = dto.FilledByEmail,
                FilledByRole = dto.FilledByRole,
                
                HeaderData = dto.HeaderData,
                BodyData = dto.BodyData,
                FirmasData = dto.FirmasData,
                TipoProducto = dto.TipoProducto, // 🦐🐟 NUEVO: Guardar tipo de producto
                Observaciones = dto.Observaciones,
                CreatedAt = DateTime.Now
            };

            _context.FilledForms.Add(filledForm);
            await _context.SaveChangesAsync();
            
            // ✅ INDEXAR PARA TRAZABILIDAD RÁPIDA
            await IndexTraceabilityRecords(filledForm, template);

            _logger.LogInformation("✅ Formulario {FormId} creado por {User} ({Email})", 
                filledForm.FormID, filledForm.FilledBy ?? "Desconocido", filledForm.FilledByEmail ?? "Sin email");

            // ✅ CREAR ALERTAS INMEDIATAS para todos los firmantes
            await CreateInitialSignatureAlerts(filledForm, template);
            
            return CreatedAtAction(nameof(GetFilledForm), new { id = filledForm.FormID }, filledForm);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutFilledForm(int id, [FromBody] FilledFormInputDto dto)
        {
            // Verificar que el formulario existe
            var existingForm = await _context.FilledForms.FindAsync(id);
            if (existingForm == null)
            {
                return NotFound(new { message = "El formulario llenado no fue encontrado." });
            }

            // Verificar que el TemplateID es válido
            var templateExists = await _context.Templates.AnyAsync(t => t.TemplateID == dto.TemplateID);
            if (!templateExists)
            {
                return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
            }

            // Actualizar los campos del formulario existente
            existingForm.TemplateID = dto.TemplateID;
            existingForm.HeaderData = dto.HeaderData;
            existingForm.BodyData = dto.BodyData;
            existingForm.FirmasData = dto.FirmasData;
            existingForm.TipoProducto = dto.TipoProducto; // 🦐🐟 NUEVO: Actualizar tipo de producto
            existingForm.Observaciones = dto.Observaciones;
            existingForm.UpdatedAt = DateTime.Now; // ✅ Hora local del servidor
            // CreatedAt se mantiene sin cambios

            _context.Entry(existingForm).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                
                // ✅ RE-INDEXAR PARA TRAZABILIDAD RÁPIDA
                var template = await _context.Templates.FindAsync(dto.TemplateID);
                if (template != null) {
                    await IndexTraceabilityRecords(existingForm, template);
                }
                
                return Ok(new { 
                    message = "Formulario actualizado exitosamente", 
                    formId = id,
                    updatedAt = existingForm.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FilledFormExists(id))
                {
                    return NotFound(new { message = "El formulario ya no existe." });
                }
                else
                {
                    return BadRequest(new { message = "Error de concurrencia al actualizar. Intente nuevamente." });
                }
            }
        }

        // PATCH: api/FilledForms/{id}/autosave - Para autoguardado específico
        [HttpPatch("{id}/autosave")]
        public async Task<IActionResult> AutosaveFilledForm(int id, [FromBody] AutosaveDto dto)
        {
            var existingForm = await _context.FilledForms.FindAsync(id);
            if (existingForm == null)
            {
                return NotFound(new { message = "El formulario llenado no fue encontrado." });
            }

            // Solo actualizar datos para autoguardado (sin cambiar template)
            if (!string.IsNullOrEmpty(dto.HeaderData))
                existingForm.HeaderData = dto.HeaderData;
                
            if (!string.IsNullOrEmpty(dto.BodyData))
                existingForm.BodyData = dto.BodyData;
                
            if (!string.IsNullOrEmpty(dto.FirmasData))
                existingForm.FirmasData = dto.FirmasData;
            
            // 🦐🐟 NUEVO: Actualizar tipo de producto en autoguardado
            if (!string.IsNullOrEmpty(dto.TipoProducto))
                existingForm.TipoProducto = dto.TipoProducto;
                
            if (!string.IsNullOrEmpty(dto.Observaciones))
                existingForm.Observaciones = dto.Observaciones;

            existingForm.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
                
                // ✅ RE-INDEXAR PARA TRAZABILIDAD RÁPIDA
                var template = await _context.Templates.FindAsync(existingForm.TemplateID);
                if (template != null) {
                    await IndexTraceabilityRecords(existingForm, template);
                }
                
                return Ok(new { 
                    message = "Autoguardado exitoso", 
                    formId = id,
                    timestamp = existingForm.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return BadRequest(new { message = "Error en autoguardado. Intente nuevamente." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFilledForm(int id)
        {
            var filledForm = await _context.FilledForms.FindAsync(id);
            if (filledForm == null)
            {
                return NotFound();
            }

            // Eliminar registros relacionados para evitar error de FK
            var relatedSignatures = await _context.Signatures
                .Where(s => s.FilledFormId == id)
                .ToListAsync();
            if (relatedSignatures.Any())
                _context.Signatures.RemoveRange(relatedSignatures);

            var relatedRejections = await _context.SignatureRejections
                .Where(r => r.FilledFormId == id)
                .ToListAsync();
            if (relatedRejections.Any())
                _context.SignatureRejections.RemoveRange(relatedRejections);

            var relatedAlerts = await _context.Alerts
                .Where(a => a.FormId == id)
                .ToListAsync();
            if (relatedAlerts.Any())
                _context.Alerts.RemoveRange(relatedAlerts);

            _context.FilledForms.Remove(filledForm);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Función de ayuda para verificar si existe un formulario
        private bool FilledFormExists(int id)
        {
            return _context.FilledForms.Any(e => e.FormID == id);
        }

        // Función helper para obtener datos del template actual
        private object? GetCurrentTemplateData(Template? template)
        {
            if (template == null) return null;

            return new
            {
                TemplateID = template.TemplateID,
                Codigo = template.Codigo,
                Nombre = template.Nombre,
                Version = template.Version,
                Objetivo = template.Supervisa,
                Proceso = template.Proceso,
                CuandoSeUsa = template.CuandoSeUsa,
                QuienLoLlena = template.QuienLoLlena,
                HeaderFields = template.HeaderFields,
                BodyElements = template.BodyElements,
                Firmas = template.Firmas,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        //==============================================================
        // ENDPOINTS PARA EXPORTACIÓN PDF/EXCEL
        //==============================================================

        /// <summary>
        /// GET: api/FilledForms/5/with-template
        /// Obtiene el formulario con su template completo incluido y parseado (para PDF/Excel)
        /// </summary>
        [HttpGet("{id}/with-template")]
        public async Task<ActionResult<object>> GetFilledFormWithTemplate(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound(new { message = "Formulario no encontrado" });
            }

            // VERSIONAMIENTO: Usar snapshot si existe, si no usar template actual
            Template? templateToUse = null;
            bool isHistorical = false;

            if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
            {
                try
                {
                    templateToUse = JsonSerializer.Deserialize<Template>(filledForm.TemplateSnapshot);
                    isHistorical = true;
                }
                catch
                {
                    templateToUse = filledForm.Template;
                }
            }
            else
            {
                templateToUse = filledForm.Template;
            }

            if (templateToUse == null)
            {
                return NotFound(new { message = "Template no encontrado" });
            }
            
            // Si el snapshot no tenía FechaVersion, usar la del template actual
            if (templateToUse.FechaVersion == null && filledForm.Template?.FechaVersion != null)
            {
                templateToUse.FechaVersion = filledForm.Template.FechaVersion;
            }

            // Parsear datos JSON del formulario
            object? headerData = null;
            object? bodyData = null;
            object? firmasData = null;
            
            try
            {
                headerData = string.IsNullOrEmpty(filledForm.HeaderData) 
                    ? new { } 
                    : JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                    
                bodyData = string.IsNullOrEmpty(filledForm.BodyData) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(filledForm.BodyData);
                    
                firmasData = string.IsNullOrEmpty(filledForm.FirmasData) 
                    ? new { } 
                    : JsonSerializer.Deserialize<object>(filledForm.FirmasData);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error parseando datos del formulario", error = ex.Message });
            }

            // Parsear estructura JSON del template
            object? headerFields = null;
            object? bodyElements = null;
            object? firmas = null;
            
            try
            {
                // Para headerFields usar SIEMPRE el template actual (tiene los defaultValues actualizados)
                // Para bodyElements usar el snapshot (tiene la estructura correcta del momento del llenado)
                var currentTemplate = filledForm.Template;
                var headerFieldsSource = (!string.IsNullOrEmpty(currentTemplate?.HeaderFields))
                    ? currentTemplate.HeaderFields
                    : templateToUse.HeaderFields;

                headerFields = string.IsNullOrEmpty(headerFieldsSource) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(headerFieldsSource);
                    
                bodyElements = string.IsNullOrEmpty(templateToUse.BodyElements) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(templateToUse.BodyElements);
                    
                firmas = string.IsNullOrEmpty(templateToUse.Firmas) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(templateToUse.Firmas);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error parseando estructura del template", error = ex.Message });
            }

            // Respuesta completa optimizada para exportación
            var response = new
            {
                // Metadatos del formulario
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion,
                FechaVersion = filledForm.FechaVersion,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Observaciones = filledForm.Observaciones,
                IsHistorical = isHistorical,
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                // Datos del formulario (parseados)
                Data = new
                {
                    Header = headerData,
                    Body = bodyData,
                    Firmas = firmasData
                },
                
                // Estructura del template (parseada)
                Template = new
                {
                    TemplateID = templateToUse.TemplateID,
                    Codigo = templateToUse.Codigo,
                    Nombre = templateToUse.Nombre,
                    Version = templateToUse.Version,
                    FechaVersion = templateToUse.FechaVersion,
                    CreatedAt = templateToUse.CreatedAt,
                    Objetivo = templateToUse.Supervisa,
                    Proceso = templateToUse.Proceso,
                    CuandoSeUsa = templateToUse.CuandoSeUsa,
                    QuienLoLlena = templateToUse.QuienLoLlena,
                    Structure = new
                    {
                        HeaderFields = headerFields,
                        BodyElements = bodyElements,
                        Firmas = firmas
                    }
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// POST: api/FilledForms/export-multiple
        /// Obtiene múltiples formularios con sus templates para exportación masiva
        /// Body: [1, 2, 3, 4, 5]
        /// </summary>
        [HttpPost("export-multiple")]
        public async Task<ActionResult<object>> ExportMultipleForms([FromBody] int[] formIds)
        {
            if (formIds == null || formIds.Length == 0)
            {
                return BadRequest(new { message = "Debe proporcionar al menos un FormID" });
            }

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => formIds.Contains(f.FormID))
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return NotFound(new { message = "No se encontraron formularios con los IDs proporcionados" });
            }

            var result = forms.Select(filledForm =>
            {
                // VERSIONAMIENTO: Usar snapshot si existe
                Template? templateToUse = null;
                bool isHistorical = false;

                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
                {
                    try
                    {
                        templateToUse = JsonSerializer.Deserialize<Template>(filledForm.TemplateSnapshot);
                        isHistorical = true;
                    }
                    catch
                    {
                        templateToUse = filledForm.Template;
                    }
                }
                else
                {
                    templateToUse = filledForm.Template;
                }

                // Parsear datos del formulario
                object? headerData = null;
                object? bodyData = null;
                object? firmasData = null;
                
                try
                {
                    headerData = string.IsNullOrEmpty(filledForm.HeaderData) 
                        ? new { } 
                        : JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                        
                    bodyData = string.IsNullOrEmpty(filledForm.BodyData) 
                        ? new object[] { } 
                        : JsonSerializer.Deserialize<object>(filledForm.BodyData);
                        
                    firmasData = string.IsNullOrEmpty(filledForm.FirmasData) 
                        ? new { } 
                        : JsonSerializer.Deserialize<object>(filledForm.FirmasData);
                }
                catch { }

                // Parsear estructura del template
                object? headerFields = null;
                object? bodyElements = null;
                object? firmas = null;
                
                if (templateToUse != null)
                {
                    try
                    {
                        // Para headerFields usar SIEMPRE el template actual (tiene los defaultValues actualizados)
                        // Para bodyElements usar el snapshot (tiene la estructura correcta del momento del llenado)
                        var headerFieldsSource = (!string.IsNullOrEmpty(filledForm.Template?.HeaderFields))
                            ? filledForm.Template.HeaderFields
                            : templateToUse.HeaderFields;

                        headerFields = string.IsNullOrEmpty(headerFieldsSource) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(headerFieldsSource);
                            
                        bodyElements = string.IsNullOrEmpty(templateToUse.BodyElements) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(templateToUse.BodyElements);
                            
                        firmas = string.IsNullOrEmpty(templateToUse.Firmas) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(templateToUse.Firmas);
                    }
                    catch { }
                }

                return new
                {
                    FormID = filledForm.FormID,
                    TemplateID = filledForm.TemplateID,
                    TemplateVersion = filledForm.TemplateVersion,
                    CreatedAt = filledForm.CreatedAt,
                    UpdatedAt = filledForm.UpdatedAt,
                    Observaciones = filledForm.Observaciones,
                    IsHistorical = isHistorical,
                    Data = new
                    {
                        Header = headerData,
                        Body = bodyData,
                        Firmas = firmasData
                    },
                    Template = templateToUse == null ? null : new
                    {
                        TemplateID = templateToUse.TemplateID,
                        Codigo = templateToUse.Codigo,
                        Nombre = templateToUse.Nombre,
                        Version = templateToUse.Version,
                        FechaVersion = templateToUse.FechaVersion,
                        CreatedAt = templateToUse.CreatedAt,
                        Objetivo = templateToUse.Supervisa,
                        Proceso = templateToUse.Proceso,
                        Structure = new
                        {
                            HeaderFields = headerFields,
                            BodyElements = bodyElements,
                            Firmas = firmas
                        }
                    }
                };
            }).ToList();

            return Ok(new 
            { 
                count = result.Count, 
                forms = result,
                message = $"{result.Count} formulario(s) listos para exportación"
            });
        }

        /// <summary>
        /// GET: api/FilledForms/export-by-template/9
        /// Obtiene todos los formularios de un template específico para exportación masiva
        /// </summary>
        [HttpGet("export-by-template/{templateId}")]
        public async Task<ActionResult<object>> ExportFormsByTemplate(int templateId)
        {
            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.TemplateID == templateId)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return Ok(new 
                { 
                    count = 0, 
                    forms = new object[] { },
                    message = "No hay formularios para este template"
                });
            }

            var formIds = forms.Select(f => f.FormID).ToArray();
            
            // Reutilizar la lógica de export-multiple
            return await ExportMultipleForms(formIds);
        }

        /// <summary>
        /// GET: api/FilledForms/export-by-date-range?startDate=2025-01-01&endDate=2025-12-31
        /// Obtiene formularios por rango de fechas para exportación
        /// </summary>
        [HttpGet("export-by-date-range")]
        public async Task<ActionResult<object>> ExportFormsByDateRange(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest(new { message = "La fecha de inicio debe ser menor a la fecha final" });
            }

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return Ok(new 
                { 
                    count = 0, 
                    forms = new object[] { },
                    message = $"No hay formularios entre {startDate:yyyy-MM-dd} y {endDate:yyyy-MM-dd}"
                });
            }

            var formIds = forms.Select(f => f.FormID).ToArray();
            
            return await ExportMultipleForms(formIds);
        }

        // 🎯 MÉTODO HELPER: Determina qué versión estaba vigente en una fecha específica
        private async Task<string> GetVersionVigenteEnFecha(int templateId, DateTime fecha)
        {
            // Obtener todas las versiones con fecha asignada
            var versiones = await _context.TemplateVersions
                .Where(tv => tv.TemplateID == templateId && tv.FechaVersion != null)
                .OrderByDescending(tv => tv.FechaVersion)
                .ToListAsync();

            Console.WriteLine($"🔍 DEBUG - GetVersionVigenteEnFecha: TemplateID={templateId}, Fecha={fecha:yyyy-MM-dd}");
            Console.WriteLine($"🔍 DEBUG - Versiones encontradas: {versiones.Count}");

            // Buscar la versión más reciente que sea <= a la fecha del formulario
            var versionVigente = versiones
                .Where(v => v.FechaVersion <= fecha)
                .OrderByDescending(v => v.FechaVersion)
                .FirstOrDefault();

            if (versionVigente != null)
            {
                Console.WriteLine($"✅ Versión vigente encontrada: {versionVigente.Version} (FechaVersion: {versionVigente.FechaVersion:yyyy-MM-dd})");
                return versionVigente.Version;
            }

            // Si no hay versión vigente, usar la versión actual del template
            var currentTemplate = await _context.Templates.FindAsync(templateId);
            var fallbackVersion = currentTemplate?.Version ?? "1";
            Console.WriteLine($"⚠️ No se encontró versión vigente, usando fallback: {fallbackVersion}");
            return fallbackVersion;
        }

        // 🎯 MÉTODO HELPER: Obtiene la estructura de una versión específica
        private async Task<object?> GetTemplateStructureByVersion(int templateId, string version)
        {
            Console.WriteLine($"🔍 DEBUG - GetTemplateStructureByVersion: TemplateID={templateId}, Version={version}");

            // Buscar la versión específica
            var templateVersion = await _context.TemplateVersions
                .FirstOrDefaultAsync(tv => tv.TemplateID == templateId && tv.Version == version);

            if (templateVersion == null)
            {
                Console.WriteLine($"⚠️ No se encontró TemplateVersion para version={version}");
                return null;
            }

            // Construir objeto con la estructura
            var structure = new
            {
                headerFields = string.IsNullOrEmpty(templateVersion.HeaderFields) 
                    ? new List<object>() 
                    : JsonSerializer.Deserialize<List<object>>(templateVersion.HeaderFields),
                bodyElements = string.IsNullOrEmpty(templateVersion.BodyElements) 
                    ? new List<object>() 
                    : JsonSerializer.Deserialize<List<object>>(templateVersion.BodyElements),
                firmas = string.IsNullOrEmpty(templateVersion.Firmas) 
                    ? new List<object>() 
                    : JsonSerializer.Deserialize<List<object>>(templateVersion.Firmas)
            };

            Console.WriteLine($"✅ Estructura recuperada para version={version}");
            return structure;
        }

        /// <summary>
        /// ✨ NUEVO: Crea alertas para TODOS los firmantes cuando se crea el formulario
        /// </summary>
        private async Task CreateInitialSignatureAlerts(FilledForm form, Template template)
        {
            try
            {
                if (string.IsNullOrEmpty(form.FirmasData))
                {
                    _logger.LogInformation("⚠️ Formulario {FormId} no tiene FirmasData, no se crean alertas", form.FormID);
                    return;
                }

                var firmasDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.FirmasData);
                if (firmasDict == null || firmasDict.Count == 0)
                {
                    _logger.LogInformation("⚠️ Formulario {FormId} - FirmasData vacío", form.FormID);
                    return;
                }

                var templateName = template.Nombre ?? "Formulario";
                var formCode = template.Codigo ?? "N/A";

                _logger.LogInformation("📋 Creando alertas iniciales para formulario {FormId} ({FormCode}). Total puestos: {Count}", 
                    form.FormID, formCode, firmasDict.Count);

                int alertasCreadas = 0;
                var emailsFirmantesNotificados = new List<string>();

                foreach (var kvp in firmasDict)
                {
                    string puesto = kvp.Key;
                    var firmaData = kvp.Value;

                    if (firmaData.ValueKind != JsonValueKind.Object)
                    {
                        _logger.LogWarning("  ⚠️ Puesto {Puesto} no es un objeto JSON válido", puesto);
                        continue;
                    }

                    // Extraer email del usuario asignado
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

                    // Fallback 1: buscar en nombre si contiene @
                    if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName) && targetName.Contains("@"))
                    {
                        targetEmail = targetName;
                    }

                    // Fallback 2: Buscar en CatalogoFirmas por nombre
                    if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName))
                    {
                        var catalogoEntry = await _context.Set<CatalogoFirma>()
                            .FirstOrDefaultAsync(c => 
                                c.Activo && 
                                (c.NombreCompleto.ToLower() == targetName.ToLower() || 
                                 c.NombreCompleto.ToLower().Contains(targetName.ToLower())));
                        
                        if (catalogoEntry != null && !string.IsNullOrEmpty(catalogoEntry.Correo))
                        {
                            targetEmail = catalogoEntry.Correo;
                            _logger.LogInformation("  🔎 Email encontrado en CatalogoFirmas para {Name}: {Email}", 
                                targetName, targetEmail);
                        }
                    }

                    // Fallback 3: Buscar en CatalogoFirmas por puesto
                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        var catalogoEntries = await _context.Set<CatalogoFirma>()
                            .Where(c => c.Activo && c.Puesto.ToLower().Contains(puesto.ToLower()))
                            .ToListAsync();
                        
                        if (catalogoEntries.Any(c => !string.IsNullOrEmpty(c.Correo)))
                        {
                            targetEmail = catalogoEntries.First(c => !string.IsNullOrEmpty(c.Correo)).Correo;
                            targetName = catalogoEntries.First(c => !string.IsNullOrEmpty(c.Correo)).NombreCompleto;
                            _logger.LogInformation("  🔎 Email encontrado en CatalogoFirmas para puesto {Puesto}: {Email}", 
                                puesto, targetEmail);
                        }
                    }

                    // Si no hay email asignado, saltar este puesto
                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        _logger.LogWarning("  ⚠️ Puesto {Puesto}: No se encontró email en FirmasData ni en CatalogoFirmas, saltando", puesto);
                        continue;
                    }

                    _logger.LogInformation("  🔍 Puesto {Puesto}: Usuario asignado = {Name} ({Email})", 
                        puesto, targetName ?? "Sin nombre", targetEmail);

                    // Verificar si ya firmó (en caso de formularios pre-firmados)
                    bool yaFirmo = false;
                    if (firmaData.TryGetProperty("firma", out var firmaObj) && firmaObj.ValueKind == JsonValueKind.Object)
                    {
                        bool tieneUrl = firmaObj.TryGetProperty("url", out var urlProp) && !string.IsNullOrEmpty(urlProp.GetString());
                        bool tieneBase64 = firmaObj.TryGetProperty("base64", out var b64Prop) && !string.IsNullOrEmpty(b64Prop.GetString());
                        yaFirmo = tieneUrl || tieneBase64;

                        if (yaFirmo)
                        {
                            _logger.LogInformation("  ✅ Puesto {Puesto} ({Email}): Ya tiene firma, no se crea alerta", puesto, targetEmail);
                            continue;
                        }
                    }

                    // Verificar si ya existe alerta (evitar duplicados)
                    var existingAlert = await _context.Set<Alert>()
                        .FirstOrDefaultAsync(a =>
                            a.FormId == form.FormID &&
                            a.TargetEmail == targetEmail &&
                            a.Type == "signature" &&
                            a.Status == "pending");

                    if (existingAlert != null)
                    {
                        _logger.LogInformation("  ℹ️ Ya existe alerta para {Email} en formulario {FormId}", targetEmail, form.FormID);
                        continue;
                    }

                    // ✅ CREAR ALERTA
                    var alert = new Alert
                    {
                        Type = "signature",
                        Priority = "high",
                        Title = $"Firma requerida: {templateName}",
                        Message = $"Se ha creado el formulario {formCode} ({templateName}) que requiere tu firma en el puesto: {puesto}. Por favor revisa y firma el formulario lo antes posible.",
                        TargetEmail = targetEmail,
                        FormId = form.FormID,
                        FormCode = formCode,
                        CreatedDate = DateTime.Now,
                        IsRead = false,
                        Status = "pending"
                    };

                    _context.Set<Alert>().Add(alert);
                    alertasCreadas++;
                    emailsFirmantesNotificados.Add(targetEmail);
                    
                    _logger.LogInformation("  ✅ ALERTA CREADA para {Email} en puesto {Puesto}", targetEmail, puesto);

                    // 📧 ENVIAR EMAIL (con estado real, sin fire-and-forget)
                    try
                    {
                        var emailSubject = $"✍️ Firma Requerida - {templateName}";
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
                                            <h1 style='margin: 0;'>✍️ Nuevo Formulario Requiere tu Firma</h1>
                                            <p style='margin: 10px 0 0 0; font-size: 18px;'>Sistema de Gestión Frigolab</p>
                                        </div>
                                        
                                        <div style='background: #f8f9fa; 
                                                    padding: 20px; 
                                                    border-radius: 10px; 
                                                    margin-bottom: 20px;'>
                                            <h2 style='color: #333; margin-top: 0;'>Hola {targetName ?? "Usuario"},</h2>
                                            <p style='color: #555; font-size: 16px; line-height: 1.6;'>
                                                Se ha creado un nuevo formulario que requiere tu firma digital:
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
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Código</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{formCode}</td>
                                                </tr>
                                                <tr style='background: #f8f9fa;'>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Tu puesto</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{puesto}</td>
                                                </tr>
                                                <tr>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Creado por</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{form.FilledBy ?? "Sistema"}</td>
                                                </tr>
                                                <tr style='background: #f8f9fa;'>
                                                    <td style='padding: 12px; font-weight: bold;'>Fecha de creación</td>
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
                                                <strong>Nota:</strong> Por favor firma este formulario lo antes posible.
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

                        var sent = await _emailService.SendAlertEmailAsync(targetEmail, emailSubject, emailBody);
                        alert.Status = sent ? "sent" : "failed";
                        _logger.LogInformation("  📧 EMAIL {Result} a {Email} ({Name})", sent ? "ENVIADO" : "FALLIDO", targetEmail, targetName ?? "Sin nombre");
                    }
                    catch (Exception emailEx)
                    {
                        alert.Status = "failed";
                        _logger.LogError(emailEx, "  ❌ Error al enviar email a {Email}", targetEmail);
                    }
                }

                // 📣 Notificar al responsable (quien creó/envió el formulario) solo internamente en el sistema (sin enviar correo)
                if (!string.IsNullOrWhiteSpace(form.FilledByEmail) && alertasCreadas > 0)
                {
                    var responsableEmail = form.FilledByEmail.Trim();
                    var responsableAlert = new Alert
                    {
                        Type = "signature_creator_notice",
                        Priority = "medium",
                        Title = $"Solicitudes de firma enviadas: {templateName}",
                        Message = $"Se enviaron {alertasCreadas} solicitudes de firma para el formulario {formCode}.",
                        TargetEmail = responsableEmail,
                        FormId = form.FormID,
                        FormCode = formCode,
                        CreatedDate = DateTime.Now,
                        IsRead = false,
                        Status = "sent_internal"
                    };

                    _context.Set<Alert>().Add(responsableAlert);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("📨 {Count} alertas creadas para formulario {FormId}", alertasCreadas, form.FormID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear alertas iniciales para formulario {FormId}", form.FormID);
                // No lanzar excepción para no bloquear la creación del formulario
            }
        }
        
        private async Task IndexTraceabilityRecords(FilledForm filledForm, Template template)
        {
            // Clear existing records for this form if any (for Put/Patch)
            var existingRecords = await _context.LotesTrazabilidad.Where(x => x.FormID == filledForm.FormID).ToListAsync();
            if (existingRecords.Any())
            {
                _context.LotesTrazabilidad.RemoveRange(existingRecords);
            }

            var lotesEncontrados = new HashSet<string>();
            string? producto = filledForm.TipoProducto;
            string? subproducto = null;

            if (!string.IsNullOrEmpty(filledForm.HeaderData))
            {
                try
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(filledForm.HeaderData);
                    if (headerDict != null)
                    {
                        foreach (var kvp in headerDict)
                        {
                            if (kvp.Key.Contains("lote", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                            {
                                // Si el valor es un JsonElement (puede ser array lote_entrante o string simple)
                                if (kvp.Value is JsonElement je)
                                {
                                    if (je.ValueKind == JsonValueKind.Array)
                                    {
                                        // lote_entrante: array de objetos con campo "lote"
                                        foreach (var entry in je.EnumerateArray())
                                        {
                                            if (entry.ValueKind != JsonValueKind.Object) continue;
                                            foreach (var prop in entry.EnumerateObject())
                                            {
                                                if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var lv = prop.Value.GetString()?.Trim();
                                                    if (!string.IsNullOrEmpty(lv) && lv.Length <= 100)
                                                        lotesEncontrados.Add(lv);
                                                }
                                            }
                                        }
                                    }
                                    else if (je.ValueKind == JsonValueKind.String)
                                    {
                                        var lv = je.GetString()?.Trim();
                                        if (!string.IsNullOrEmpty(lv) && lv.Length <= 100)
                                            lotesEncontrados.Add(lv);
                                    }
                                }
                                else
                                {
                                    var val = kvp.Value.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(val) && val.Length <= 100)
                                        lotesEncontrados.Add(val);
                                }
                            }
                            else if (kvp.Key.Contains("producto", StringComparison.OrdinalIgnoreCase) && 
                                     !kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && 
                                     kvp.Value != null && producto == null)
                            {
                                producto = kvp.Value is JsonElement jeProd && jeProd.ValueKind == JsonValueKind.String
                                    ? jeProd.GetString()?.Trim()
                                    : kvp.Value.ToString()?.Trim();
                            }
                            else if (kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                            {
                                subproducto = kvp.Value is JsonElement jeSub && jeSub.ValueKind == JsonValueKind.String
                                    ? jeSub.GetString()?.Trim()
                                    : kvp.Value.ToString()?.Trim();
                            }
                        }
                    }
                }
                catch { }
            }

            // Search in BodyData
            if (!string.IsNullOrEmpty(filledForm.BodyData))
            {
                try
                {
                    var bodyData = JsonSerializer.Deserialize<List<JsonElement>>(filledForm.BodyData);
                    if (bodyData != null)
                    {
                        foreach (var element in bodyData)
                        {
                            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "table")
                            {
                                if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var row in dataProp.EnumerateArray())
                                    {
                                        if (row.ValueKind == JsonValueKind.Object)
                                        {
                                            foreach (var prop in row.EnumerateObject())
                                            {
                                                if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var val = prop.Value.ToString()?.Trim();
                                                    if (!string.IsNullOrEmpty(val))
                                                        lotesEncontrados.Add(val);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Guardar en la tabla LotesTrazabilidad
            foreach (var lote in lotesEncontrados)
            {
                _context.LotesTrazabilidad.Add(new LoteTrazabilidad
                {
                    FormID = filledForm.FormID,
                    LoteOrigen = lote,
                    Proceso = template.Proceso ?? "Desconocido",
                    Producto = producto,
                    CantidadEntrada = 0,
                    CantidadSalida = 0,
                    FechaRegistro = filledForm.CreatedAt
                });
            }

            await _context.SaveChangesAsync();
        }
    }

    // DTO para recibir datos de entrada
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? TipoProducto { get; set; } // 🦐🐟 NUEVO: Tipo de producto
        public string? Observaciones { get; set; }
        
        // ✅ AUDITORÍA: Datos del usuario que crea el formulario
        public string? FilledBy { get; set; }
        public string? FilledByEmail { get; set; }
        public string? FilledByRole { get; set; }
    }

    // DTO para autoguardado (campos opcionales)
    public class AutosaveDto
    {
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? TipoProducto { get; set; } // 🦐🐟 NUEVO
        public string? Observaciones { get; set; }
    }
}

