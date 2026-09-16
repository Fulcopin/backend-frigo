using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Catálogo de consultas fijas del Comparativo Plan vs Producción.
    ///
    /// Cada entrada es un cruce actividad × bloque × columna con su receta:
    /// "Fileteo (PESCADO) · PROD·Libras = suma del Peso Neto Total del PD-04".
    /// Vive en la base para que se configure UNA vez y lo vea toda la planta
    /// desde cualquier computadora — antes estaba en el localStorage de cada
    /// navegador y no se compartía.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ConsultasPlanController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConsultasPlanController> _logger;

        public ConsultasPlanController(ApplicationDbContext context, ILogger<ConsultasPlanController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>Normaliza la clave: sin espacios y el bloque en mayúsculas.</summary>
        private static (string actividad, string grupo, string clave) Clave(string? actividad, string? grupo, string? claveCol)
            => ((actividad ?? "").Trim(), (grupo ?? "").Trim().ToUpperInvariant(), (claveCol ?? "").Trim());

        /// <summary>Todo el catálogo. El frontend lo arma en su estructura y lo cachea.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTodas()
        {
            try
            {
                var consultas = await _context.ConsultasPlan
                    .AsNoTracking()
                    .OrderBy(c => c.Grupo).ThenBy(c => c.Actividad).ThenBy(c => c.Clave)
                    .Select(c => new
                    {
                        c.Id, c.Actividad, c.Grupo, c.Clave, c.Receta,
                        c.ActualizadoPor, c.UpdatedAt,
                    })
                    .ToListAsync();

                return Ok(consultas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar las consultas del plan");
                return StatusCode(500, new { message = "Error al listar las consultas" });
            }
        }

        /// <summary>
        /// Guarda una consulta (la crea o pisa la que hubiera para ese cruce).
        /// </summary>
        [HttpPut]
        public async Task<ActionResult> Guardar([FromBody] ConsultaPlanDto dto)
        {
            var (actividad, grupo, clave) = Clave(dto.Actividad, dto.Grupo, dto.Clave);
            if (string.IsNullOrWhiteSpace(clave))
                return BadRequest(new { message = "Falta la columna (clave) de la consulta." });

            try
            {
                var consulta = await _context.ConsultasPlan
                    .FirstOrDefaultAsync(c => c.Actividad == actividad && c.Grupo == grupo && c.Clave == clave);

                bool esNueva = consulta == null;
                if (consulta == null)
                {
                    consulta = new ConsultaPlan
                    {
                        Actividad = actividad, Grupo = grupo, Clave = clave,
                        CreatedAt = DateTime.Now,
                    };
                    _context.ConsultasPlan.Add(consulta);
                }

                consulta.Receta = string.IsNullOrWhiteSpace(dto.Receta) ? "{}" : dto.Receta;
                consulta.ActualizadoPor = dto.ActualizadoPor;
                consulta.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Consulta del plan {Accion}: {Actividad} [{Grupo}] x {Clave} por {Usuario}",
                    esNueva ? "creada" : "actualizada", actividad, grupo, clave, dto.ActualizadoPor ?? "desconocido");

                return Ok(new { success = true, id = consulta.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la consulta {Actividad} x {Clave}", actividad, clave);
                return StatusCode(500, new { message = "Error al guardar la consulta" });
            }
        }

        /// <summary>
        /// Sube varias de una (lo que cada navegador tenía en localStorage).
        /// No pisa lo que ya está en el servidor: la base manda.
        /// </summary>
        [HttpPost("importar")]
        public async Task<ActionResult> Importar([FromBody] List<ConsultaPlanDto> dtos)
        {
            if (dtos == null || dtos.Count == 0)
                return Ok(new { success = true, importadas = 0, message = "No había nada que importar" });

            try
            {
                var existentes = await _context.ConsultasPlan
                    .Select(c => new { c.Actividad, c.Grupo, c.Clave })
                    .ToListAsync();

                int importadas = 0;
                foreach (var dto in dtos)
                {
                    var (actividad, grupo, clave) = Clave(dto.Actividad, dto.Grupo, dto.Clave);
                    if (string.IsNullOrWhiteSpace(clave)) continue;
                    if (existentes.Any(e => e.Actividad == actividad && e.Grupo == grupo && e.Clave == clave)) continue;

                    _context.ConsultasPlan.Add(new ConsultaPlan
                    {
                        Actividad = actividad, Grupo = grupo, Clave = clave,
                        Receta = string.IsNullOrWhiteSpace(dto.Receta) ? "{}" : dto.Receta,
                        ActualizadoPor = dto.ActualizadoPor,
                        CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now,
                    });
                    importadas++;
                }

                if (importadas > 0) await _context.SaveChangesAsync();

                _logger.LogInformation("Importadas {N} consultas del plan (de {Total} enviadas)", importadas, dtos.Count);
                return Ok(new { success = true, importadas });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al importar consultas del plan");
                return StatusCode(500, new { message = "Error al importar las consultas" });
            }
        }

        /// <summary>Quita una consulta del catálogo.</summary>
        [HttpDelete]
        public async Task<ActionResult> Borrar(
            [FromQuery] string? actividad, [FromQuery] string? grupo, [FromQuery] string clave)
        {
            var (act, grp, cl) = Clave(actividad, grupo, clave);
            try
            {
                var consulta = await _context.ConsultasPlan
                    .FirstOrDefaultAsync(c => c.Actividad == act && c.Grupo == grp && c.Clave == cl);

                // Que no exista no es un error: la pantalla ya la había sacado.
                if (consulta == null) return Ok(new { success = true, message = "No había consulta guardada" });

                _context.ConsultasPlan.Remove(consulta);
                await _context.SaveChangesAsync();

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar la consulta {Actividad} x {Clave}", act, cl);
                return StatusCode(500, new { message = "Error al borrar la consulta" });
            }
        }
    }

    public class ConsultaPlanDto
    {
        /// <summary>Actividad; vacío = consulta genérica de la columna.</summary>
        public string? Actividad { get; set; }
        /// <summary>"PESCADO" | "CAMARON" | vacío (sin bloque).</summary>
        public string? Grupo { get; set; }
        /// <summary>Columna, como "prod.libras".</summary>
        public string? Clave { get; set; }
        /// <summary>Receta serializada en JSON.</summary>
        public string? Receta { get; set; }
        public string? ActualizadoPor { get; set; }
    }
}
