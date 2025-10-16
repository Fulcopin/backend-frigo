// Estos 'using' le dicen al archivo qué herramientas necesita usar.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    // [Route] define la URL base para este controlador. Todas las peticiones
    // a "/api/Templates" llegarán aquí.
    [Route("api/[controller]")]
    [ApiController]
    public class TemplatesController : ControllerBase
    {
        // Esta variable privada guardará la conexión a la base de datos.
        private readonly ApplicationDbContext _context;

        // Este es el "constructor". Cuando se crea el controlador para manejar
        // una petición, .NET automáticamente le pasa la conexión a la base de datos
        // (esto se configuró en Program.cs).
        public TemplatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        //==============================================================
        // MÉTODO GET - Para obtener todas las plantillas
        // URL: GET /api/Templates
        //==============================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Template>>> GetTemplates()
        {
            // Usa Entity Framework para ir a la base de datos, tomar todas
            // las filas de la tabla "Templates" y devolverlas como una lista.
            return await _context.Templates.ToListAsync();
        }

        //==============================================================
        // MÉTODO GET - Para obtener UNA plantilla por su ID
        // URL: GET /api/Templates/5  (el 5 es un ejemplo de ID)
        //==============================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<Template>> GetTemplate(int id)
        {
            // Busca en la tabla "Templates" una fila cuya clave primaria (TemplateID)
            // coincida con el 'id' que vino en la URL.
            var template = await _context.Templates.FindAsync(id);

            // Si no se encuentra ninguna plantilla con ese ID, devuelve
            // un error estándar 404 (No Encontrado).
            if (template == null)
            {
                return NotFound();
            }

            // Si se encuentra, devuelve los datos de la plantilla.
            return template;
        }

        //==============================================================
        // MÉTODO POST - Para crear una NUEVA plantilla
        // URL: POST /api/Templates
        //==============================================================
        [HttpPost]
        public async Task<ActionResult<Template>> PostTemplate([FromBody] Template template)
        {
            // Recibe los datos de la nueva plantilla desde el cuerpo (body) de la petición del frontend.
            
            // Añade este nuevo objeto 'template' a la colección de Entity Framework.
            // Todavía no se guarda en la base de datos.
            _context.Templates.Add(template);
            
            // Este comando toma todos los cambios pendientes (en este caso, añadir
            // la nueva plantilla) y los ejecuta en la base de datos de Azure.
            await _context.SaveChangesAsync();

            // Devuelve una respuesta HTTP estándar "201 Created".
            // También incluye la URL para obtener el recurso recién creado y el
            // propio objeto 'template' (que ahora tendrá un 'templateID' asignado por la BD).
            return CreatedAtAction(nameof(GetTemplate), new { id = template.TemplateID }, template);
        }

        //==============================================================
        // MÉTODO PUT - Para actualizar una plantilla existente
        // URL: PUT /api/Templates/5
        //==============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTemplate(int id, [FromBody] Template template)
        {
            // Comprobación de seguridad: si el ID en la URL no coincide con el
            // ID del objeto que se está enviando, es una petición incorrecta.
            if (id != template.TemplateID)
            {
                return BadRequest();
            }

            // Le dice a Entity Framework que este objeto 'template' no es nuevo,
            // sino que representa una versión modificada de una fila que ya existe.
            _context.Entry(template).State = EntityState.Modified;

            try
            {
                // Intenta guardar los cambios en la base de datos.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Este error puede ocurrir si alguien borró la plantilla justo
                // mientras intentábamos actualizarla.
                if (!TemplateExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Devuelve una respuesta "204 No Content", que es el estándar para
            // indicar que la actualización se realizó con éxito.
            return NoContent();
        }
        
        //==============================================================
        // MÉTODO DELETE - Para borrar una plantilla
        // URL: DELETE /api/Templates/5
        //==============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            // Primero, busca la plantilla que se va a borrar.
            var template = await _context.Templates.FindAsync(id);
            if (template == null)
            {
                // Si no existe, no se puede borrar. Devuelve 404.
                return NotFound();
            }

            // Le dice a Entity Framework que esta plantilla debe ser eliminada.
            _context.Templates.Remove(template);
            // Ejecuta el comando DELETE en la base de datos.
            await _context.SaveChangesAsync();

            // Devuelve "204 No Content" para indicar el éxito.
            return NoContent();
        }


        // Función de ayuda interna. No es un endpoint de la API.
        private bool TemplateExists(int id)
        {
            return _context.Templates.Any(e => e.TemplateID == id);
        }
    }
}