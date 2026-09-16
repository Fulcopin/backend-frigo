using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TablerosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TablerosController> _logger;

        private static readonly string[] ScopesValidos = { "todos", "registro", "proceso" };

        public TablerosController(ApplicationDbContext context, ILogger<TablerosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET /api/Tableros — todas las pestañas del tablero compartido
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Tablero>>> GetTableros()
        {
            try
            {
                var list = await _context.Tableros
                    .OrderBy(t => t.Orden).ThenBy(t => t.Id)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tableros");
                return StatusCode(500, new { message = "Error al obtener las pestañas", detalle = ex.GetBaseException().Message });
            }
        }

        // POST /api/Tableros — crea una pestaña
        [HttpPost]
        public async Task<ActionResult<Tablero>> CreateTablero([FromBody] Tablero tablero)
        {
            try
            {
                var error = Validar(tablero);
                if (error != null) return BadRequest(new { message = error });

                tablero.Id = 0;
                tablero.CreadoEn = DateTime.Now;
                if (tablero.Orden == 0)
                    tablero.Orden = (await _context.Tableros.MaxAsync(t => (int?)t.Orden) ?? 0) + 1;

                _context.Tableros.Add(tablero);
                await _context.SaveChangesAsync();
                return Ok(tablero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tablero");
                return StatusCode(500, new { message = "Error al crear la pestaña" });
            }
        }

        // PUT /api/Tableros/{id} — renombra / cambia el alcance / reordena
        [HttpPut("{id}")]
        public async Task<ActionResult<Tablero>> UpdateTablero(int id, [FromBody] Tablero cambios)
        {
            try
            {
                var tablero = await _context.Tableros.FindAsync(id);
                if (tablero == null) return NotFound();

                var error = Validar(cambios);
                if (error != null) return BadRequest(new { message = error });

                tablero.Nombre = cambios.Nombre;
                tablero.ScopeTipo = cambios.ScopeTipo;
                tablero.ScopeValor = cambios.ScopeTipo == "todos" ? null : cambios.ScopeValor;
                tablero.Orden = cambios.Orden;

                await _context.SaveChangesAsync();
                return Ok(tablero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar tablero {Id}", id);
                return StatusCode(500, new { message = "Error al actualizar la pestaña" });
            }
        }

        // DELETE /api/Tableros/{id} — borra la pestaña y los indicadores que contiene
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTablero(int id)
        {
            try
            {
                var tablero = await _context.Tableros.FindAsync(id);
                if (tablero == null) return NotFound();

                var indicadores = await _context.Indicadores.Where(i => i.TableroId == id).ToListAsync();
                if (indicadores.Count > 0) _context.Indicadores.RemoveRange(indicadores);

                _context.Tableros.Remove(tablero);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tablero {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar la pestaña" });
            }
        }

        private static string? Validar(Tablero t)
        {
            if (t == null) return "Datos de la pestaña requeridos";
            if (string.IsNullOrWhiteSpace(t.Nombre)) return "El nombre de la pestaña es requerido";
            if (!ScopesValidos.Contains(t.ScopeTipo))
                return "El alcance debe ser 'todos', 'registro' o 'proceso'";
            if (t.ScopeTipo != "todos" && string.IsNullOrWhiteSpace(t.ScopeValor))
                return "Debes elegir el registro o el proceso de la pestaña";
            return null;
        }
    }
}
