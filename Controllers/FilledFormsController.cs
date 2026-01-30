using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilledFormsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FilledFormsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetFilledForms()
        {
            var forms = await _context.FilledForms
                                 .Include(f => f.Template)
                                 .OrderByDescending(f => f.CreatedAt)
                                 .ToListAsync();
            
            // Mapear a objeto anónimo con el nombre del template
            var result = forms.Select(f => new
            {
                f.FormID,
                f.TemplateID,
                TemplateName = f.Template?.Nombre ?? "Sin nombre",
                f.TemplateVersion,
                f.FechaVersion,
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
                Objetivo = template.Objetivo,
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
                HeaderData = dto.HeaderData,
                BodyData = dto.BodyData,
                FirmasData = dto.FirmasData,
                Observaciones = dto.Observaciones,
                CreatedAt = DateTime.UtcNow
            };

            _context.FilledForms.Add(filledForm);
            await _context.SaveChangesAsync();
            
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
                Objetivo = template.Objetivo,
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
                    Objetivo = templateToUse.Objetivo,
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
                        Objetivo = templateToUse.Objetivo,
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
    }

    // DTO para recibir datos de entrada
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? Observaciones { get; set; }
    }

    // DTO para autoguardado (campos opcionales)
    public class AutosaveDto
    {
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? Observaciones { get; set; }
    }
}
