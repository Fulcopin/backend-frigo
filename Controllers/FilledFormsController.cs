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

            // Devolver datos simples y directos
            var response = new {
                formID = filledForm.FormID,
                templateID = filledForm.TemplateID,
                templateName = filledForm.Template?.Nombre ?? "Sin nombre",
                templateVersion = filledForm.TemplateVersion,
                headerData = headerDataParsed,
                bodyData = bodyDataParsed,
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
    [FromQuery] int? templateId)
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
                FechaVersion = DateTime.UtcNow, // ✅ Fecha de versión
                
                // ✅ AUDITORÍA: Guardar quién creó el formulario
                FilledBy = dto.FilledBy,
                FilledByEmail = dto.FilledByEmail,
                FilledByRole = dto.FilledByRole,
                
                HeaderData = dto.HeaderData,
                BodyData = dto.BodyData,
                FirmasData = dto.FirmasData,
                TipoProducto = dto.TipoProducto, // 🦐🐟 NUEVO: Guardar tipo de producto
                Observaciones = dto.Observaciones,
                CreatedAt = DateTime.UtcNow
            };

            _context.FilledForms.Add(filledForm);
            await _context.SaveChangesAsync();
            
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
            existingForm.UpdatedAt = DateTime.UtcNow; // Agregar timestamp de actualización
            // CreatedAt se mantiene sin cambios

            _context.Entry(existingForm).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                
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

            existingForm.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                
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
                headerFields = string.IsNullOrEmpty(templateToUse.HeaderFields) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(templateToUse.HeaderFields);
                    
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
                        headerFields = string.IsNullOrEmpty(templateToUse.HeaderFields) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(templateToUse.HeaderFields);
                            
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

                    // Fallback: buscar en nombre si contiene @
                    if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName) && targetName.Contains("@"))
                    {
                        targetEmail = targetName;
                    }

                    // Si no hay email asignado, saltar este puesto
                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        _logger.LogWarning("  ⚠️ Puesto {Puesto}: No se encontró email asignado, saltando", puesto);
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
                        CreatedDate = DateTime.UtcNow,
                        IsRead = false,
                        Status = "pending"
                    };

                    _context.Set<Alert>().Add(alert);
                    alertasCreadas++;
                    
                    _logger.LogInformation("  ✅ ALERTA CREADA para {Email} en puesto {Puesto}", targetEmail, puesto);

                    // 📧 ENVIAR EMAIL (en background para no bloquear)
                    _ = Task.Run(async () =>
                    {
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

                            await _emailService.SendAlertEmailAsync(targetEmail, emailSubject, emailBody);
                            _logger.LogInformation("  📧 EMAIL ENVIADO a {Email} ({Name})", targetEmail, targetName ?? "Sin nombre");
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "  ❌ Error al enviar email a {Email}", targetEmail);
                        }
                    });
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

