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

        //==============================================================
        // GET: /api/FilledForms
        // Obtiene todos los formularios llenos para la vista "ViewForms".
        //==============================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FilledForm>>> GetFilledForms()
        {
            // Usamos .Include() para cargar también las filas de la tabla asociadas.
            // Si no lo hacemos, la propiedad "TableRows" llegará vacía.
            return await _context.FilledForms
                                 .Include(f => f.TableRows) 
                                 .OrderByDescending(f => f.CreatedAt)
                                 .ToListAsync();
        }

        //==============================================================
        // GET: /api/FilledForms/5
        // Obtiene un formulario específico por su ID.
        //==============================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<FilledForm>> GetFilledForm(int id)
        {
            // También incluimos las filas de la tabla al pedir un solo formulario.
            var filledForm = await _context.FilledForms
                                           .Include(f => f.TableRows)
                                           .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound(); // Error 404 si el ID no existe.
            }

            return filledForm;
        }

        //==============================================================
        // POST: /api/FilledForms
        // Guarda un nuevo formulario lleno enviado desde "FillForm.jsx".
        //==============================================================
       [HttpPost]
public async Task<ActionResult<FilledForm>> PostFilledForm([FromBody] FilledFormInputDto dto)
{
    // 1. Verificación de seguridad: ¿Existe la plantilla que nos están pasando?
    var templateExists = await _context.Templates.AnyAsync(t => t.TemplateID == dto.TemplateID);
    if (!templateExists)
    {
        return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
    }

    // 2. Mapeo manual: Convertimos los datos del DTO (lo que llega de la red)
    //    a los modelos de la base de datos (lo que guardamos).
    var filledForm = new FilledForm
    {
        TemplateID = dto.TemplateID,
        HeaderData = dto.HeaderData,
        FirmasData = dto.FirmasData,
        Observaciones = dto.Observaciones,
        CreatedAt = DateTime.UtcNow,
        
        // Aquí está la magia: convertimos cada TableRowInputDto en un TableRow
        TableRows = dto.TableRows.Select(trDto => new TableRow
        {
            RowData = trDto.RowData
        }).ToList()
    };

    // 3. Añadimos el objeto `FilledForm` ya construido al contexto de la base de datos.
    //    Entity Framework automáticamente sabrá que también debe guardar los `TableRows` asociados.
    _context.FilledForms.Add(filledForm);
    
    // 4. Guardamos todo en una sola transacción.
    await _context.SaveChangesAsync();
    
    // 5. Devolvemos la respuesta estándar "201 Created".
    return CreatedAtAction(nameof(GetFilledForm), new { id = filledForm.FormID }, filledForm);
}

        //==============================================================
        // DELETE: /api/FilledForms/5
        // Borra un formulario llenado.
        //==============================================================
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

            return NoContent(); // Respuesta estándar para un borrado exitoso.
        }
    }
}