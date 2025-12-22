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
        public async Task<ActionResult<IEnumerable<FilledForm>>> GetFilledForms()
        {
            return await _context.FilledForms
                                 .OrderByDescending(f => f.CreatedAt)
                                 .ToListAsync();
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

            // VERSIONAMIENTO: Usar el snapshot guardado si existe, si no, usar el template actual
            object? templateData = null;
            
            if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
            {
                // Usar el snapshot histórico (formato con el que se creó el formulario)
                try
                {
                    templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
                }
                catch
                {
                    // Si falla la deserialización, usar el template actual como fallback
                    templateData = GetCurrentTemplateData(filledForm.Template);
                }
            }
            else
            {
                // Fallback al template actual (para datos antiguos sin snapshot)
                templateData = GetCurrentTemplateData(filledForm.Template);
            }

            // Crear una respuesta que incluya tanto el formulario como el template
            var response = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion, // Versión del template usada
                HeaderData = filledForm.HeaderData,
                BodyData = filledForm.BodyData,
                FirmasData = filledForm.FirmasData,
                Observaciones = filledForm.Observaciones,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = templateData,
                IsHistorical = !string.IsNullOrEmpty(filledForm.TemplateSnapshot) // Indica si usa versión histórica
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
