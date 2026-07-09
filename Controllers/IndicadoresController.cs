using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IndicadoresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IndicadoresController> _logger;

        public IndicadoresController(ApplicationDbContext context, ILogger<IndicadoresController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET /api/Indicadores — tablero compartido (todos los indicadores guardados)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Indicador>>> GetIndicadores()
        {
            try
            {
                var list = await _context.Indicadores
                    .OrderBy(i => i.Orden).ThenBy(i => i.Id)
                    .ToListAsync();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener indicadores");
                return StatusCode(500, new { message = "Error al obtener indicadores" });
            }
        }

        // POST /api/Indicadores — crea un indicador
        [HttpPost]
        public async Task<ActionResult<Indicador>> CreateIndicador([FromBody] Indicador indicador)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(indicador.ConfigJson))
                    return BadRequest(new { message = "La configuración del indicador es requerida" });

                indicador.Id = 0;
                indicador.CreadoEn = DateTime.Now;
                if (string.IsNullOrWhiteSpace(indicador.Titulo)) indicador.Titulo = "Indicador";

                _context.Indicadores.Add(indicador);
                await _context.SaveChangesAsync();
                return Ok(indicador);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear indicador");
                return StatusCode(500, new { message = "Error al crear indicador" });
            }
        }

        // DELETE /api/Indicadores/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteIndicador(int id)
        {
            try
            {
                var ind = await _context.Indicadores.FindAsync(id);
                if (ind == null) return NotFound();
                _context.Indicadores.Remove(ind);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar indicador {Id}", id);
                return StatusCode(500, new { message = "Error al eliminar indicador" });
            }
        }
    }
}
