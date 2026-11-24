using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// 📚 Biblioteca de bloques reutilizables (compartida por toda la planta).
    ///
    /// El editor de plantillas guarda acá un bloque completo (sección, tabla,
    /// Control del Personal, procesos, materia prima…) y después lo inserta en
    /// cualquier otra plantilla sin rearmarlo a mano.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class BloquesReutilizablesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BloquesReutilizablesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/BloquesReutilizables  (lista liviana, sin el JSON)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var lista = await _context.BloquesReutilizables
                .OrderByDescending(b => b.CreadoEn)
                .Select(b => new
                {
                    b.Id,
                    b.Nombre,
                    b.Tipo,
                    b.CreadoPor,
                    b.CreadoEn,
                })
                .ToListAsync();

            return Ok(lista);
        }

        // GET: api/BloquesReutilizables/5  (con el JSON completo)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var bloque = await _context.BloquesReutilizables.FindAsync(id);
            if (bloque == null) return NotFound(new { mensaje = "El bloque no existe." });
            return Ok(bloque);
        }

        // POST: api/BloquesReutilizables
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BloqueReutilizable dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Contenido))
            {
                return BadRequest(new { mensaje = "El nombre y el contenido son obligatorios." });
            }

            var bloque = new BloqueReutilizable
            {
                Nombre = dto.Nombre.Trim(),
                Tipo = string.IsNullOrWhiteSpace(dto.Tipo) ? "section" : dto.Tipo.Trim(),
                Contenido = dto.Contenido,
                CreadoPor = string.IsNullOrWhiteSpace(dto.CreadoPor) ? null : dto.CreadoPor.Trim(),
                CreadoEn = DateTime.Now,
            };

            _context.BloquesReutilizables.Add(bloque);
            await _context.SaveChangesAsync();

            return Ok(bloque);
        }

        // DELETE: api/BloquesReutilizables/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var bloque = await _context.BloquesReutilizables.FindAsync(id);
            if (bloque == null) return NotFound(new { mensaje = "El bloque no existe." });

            _context.BloquesReutilizables.Remove(bloque);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Bloque eliminado." });
        }
    }
}
