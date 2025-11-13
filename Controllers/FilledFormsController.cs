using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

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

            // Crear una respuesta que incluya tanto el formulario como el template
            var response = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                HeaderData = filledForm.HeaderData,
                BodyData = filledForm.BodyData,
                FirmasData = filledForm.FirmasData,
                Observaciones = filledForm.Observaciones,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = filledForm.Template != null ? new
                {
                    TemplateID = filledForm.Template.TemplateID,
                    Codigo = filledForm.Template.Codigo,
                    Nombre = filledForm.Template.Nombre,
                    Version = filledForm.Template.Version,
                    Objetivo = filledForm.Template.Objetivo,
                    Proceso = filledForm.Template.Proceso,
                    CuandoSeUsa = filledForm.Template.CuandoSeUsa,
                    QuienLoLlena = filledForm.Template.QuienLoLlena,
                    HeaderFields = filledForm.Template.HeaderFields,
                    BodyElements = filledForm.Template.BodyElements,
                    Firmas = filledForm.Template.Firmas,
                    CreatedAt = filledForm.Template.CreatedAt,
                    UpdatedAt = filledForm.Template.UpdatedAt
                } : null
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

            // Preparar datos para edición - asegurar que los JSON strings estén correctos
            var editData = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                HeaderData = !string.IsNullOrEmpty(filledForm.HeaderData) ? filledForm.HeaderData : "{}",
                BodyData = !string.IsNullOrEmpty(filledForm.BodyData) ? filledForm.BodyData : "{}",
                FirmasData = !string.IsNullOrEmpty(filledForm.FirmasData) ? filledForm.FirmasData : "{}",
                Observaciones = filledForm.Observaciones ?? "",
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = filledForm.Template != null ? new
                {
                    TemplateID = filledForm.Template.TemplateID,
                    Codigo = filledForm.Template.Codigo,
                    Nombre = filledForm.Template.Nombre,
                    Version = filledForm.Template.Version,
                    HeaderFields = !string.IsNullOrEmpty(filledForm.Template.HeaderFields) ? filledForm.Template.HeaderFields : "{}",
                    BodyElements = !string.IsNullOrEmpty(filledForm.Template.BodyElements) ? filledForm.Template.BodyElements : "{}",
                    Firmas = !string.IsNullOrEmpty(filledForm.Template.Firmas) ? filledForm.Template.Firmas : "{}"
                } : null
            };

            return Ok(editData);
        }

       [HttpPost]
        public async Task<ActionResult<FilledForm>> PostFilledForm([FromBody] FilledFormInputDto dto)
        {
            var templateExists = await _context.Templates.AnyAsync(t => t.TemplateID == dto.TemplateID);
            if (!templateExists)
            {
                return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
            }
            
            // Mapeo actualizado para usar BodyData
            var filledForm = new FilledForm
            {
                TemplateID = dto.TemplateID,
                HeaderData = dto.HeaderData,
                BodyData = dto.BodyData, // NUEVO
                FirmasData = dto.FirmasData,
                Observaciones = dto.Observaciones,
                CreatedAt = DateTime.UtcNow
                // ANTERIOR: La lógica para mapear TableRows se elimina.
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
