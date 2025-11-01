// Contenido para: Controllers/FilledFormsController.cs

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
            // ANTERIOR: Se elimina el .Include() porque ya no existe la relación TableRows
            // return await _context.FilledForms.Include(f => f.TableRows).OrderByDescending(f => f.CreatedAt).ToListAsync();
            
            // NUEVO: Simplemente obtenemos los formularios. BodyData es parte del objeto principal.
            return await _context.FilledForms
                                 .OrderByDescending(f => f.CreatedAt)
                                 .ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<FilledForm>> GetFilledForm(int id)
        {
            // ANTERIOR: Se elimina el .Include()
            // var filledForm = await _context.FilledForms.Include(f => f.TableRows).FirstOrDefaultAsync(f => f.FormID == id);

            // NUEVO: Se busca el formulario directamente.
            var filledForm = await _context.FilledForms.FindAsync(id);

            if (filledForm == null)
            {
                return NotFound();
            }

            return filledForm;
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
    }
}