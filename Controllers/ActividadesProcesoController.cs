using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// 🏭 CATÁLOGO DE ACTIVIDADES DE PROCESO
    ///
    ///   GET    api/ActividadesProceso              activas (para los combos)
    ///   GET    api/ActividadesProceso?todas=true   incluye las inactivas
    ///   POST   api/ActividadesProceso              agregar
    ///   PUT    api/ActividadesProceso/{id}         editar (nombre, ámbito, activa)
    ///   DELETE api/ActividadesProceso/{id}         dar de baja (NO borra)
    ///
    /// Dar de baja en vez de borrar: los formularios ya guardados tienen el
    /// nombre de la actividad adentro, y el plan de producción cruza por ese
    /// nombre. Si se borrara, esos registros quedarían sin cruzar.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ActividadesProcesoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ActividadesProcesoController> _logger;

        public ActividadesProcesoController(ApplicationDbContext context, ILogger<ActividadesProcesoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public class ActividadInput
        {
            public string? Nombre { get; set; }
            public bool Pescado { get; set; } = true;
            public bool Camaron { get; set; }
            public bool Activa { get; set; } = true;
            public int Orden { get; set; }
            public string? Usuario { get; set; }
        }

        private static string Limpio(string? t) =>
            System.Text.RegularExpressions.Regex.Replace(t ?? "", @"\s+", " ").Trim();

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] bool todas = false)
        {
            try
            {
                var lista = await _context.ActividadesProceso.AsNoTracking()
                    .Where(a => todas || a.Activa)
                    .OrderBy(a => a.Orden).ThenBy(a => a.Nombre)
                    .Select(a => new { a.Id, a.Nombre, a.Pescado, a.Camaron, a.Activa, a.Orden,
                                       a.ActualizadoPor, a.UpdatedAt })
                    .ToListAsync();
                return Ok(lista);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar actividades de proceso");
                return StatusCode(500, new { message = "No se pudieron cargar las actividades.", detalle = ex.GetBaseException().Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] ActividadInput dto)
        {
            var nombre = Limpio(dto?.Nombre);
            if (nombre.Length < 2)
                return BadRequest(new { message = "Escribe el nombre de la actividad." });
            if (!dto!.Pescado && !dto.Camaron)
                return BadRequest(new { message = "Elige al menos un catálogo: pescado o camarón." });

            var yaEsta = await _context.ActividadesProceso
                .FirstOrDefaultAsync(a => a.Nombre.ToLower() == nombre.ToLower());
            if (yaEsta != null)
            {
                // Si estaba dada de baja, se reactiva en vez de rechazar.
                if (!yaEsta.Activa)
                {
                    yaEsta.Activa = true;
                    yaEsta.Pescado = dto.Pescado;
                    yaEsta.Camaron = dto.Camaron;
                    yaEsta.ActualizadoPor = Limpio(dto.Usuario);
                    yaEsta.UpdatedAt = DateTime.Now;
                    await _context.SaveChangesAsync();
                    return Ok(yaEsta);
                }
                return Conflict(new { message = $"\"{yaEsta.Nombre}\" ya está en el catálogo." });
            }

            var actividad = new ActividadProceso
            {
                Nombre = nombre,
                Pescado = dto.Pescado,
                Camaron = dto.Camaron,
                Activa = dto.Activa,
                Orden = dto.Orden,
                ActualizadoPor = Limpio(dto.Usuario),
            };

            try
            {
                _context.ActividadesProceso.Add(actividad);
                await _context.SaveChangesAsync();
                _logger.LogInformation("🏭 Actividad agregada: {Nombre}", actividad.Nombre);
                return Ok(actividad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear actividad");
                return StatusCode(500, new { message = "No se pudo guardar.", detalle = ex.GetBaseException().Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] ActividadInput dto)
        {
            var actividad = await _context.ActividadesProceso.FindAsync(id);
            if (actividad == null) return NotFound(new { message = "La actividad ya no existe." });

            var nombre = Limpio(dto?.Nombre);
            if (nombre.Length < 2) return BadRequest(new { message = "Escribe el nombre de la actividad." });
            if (!dto!.Pescado && !dto.Camaron)
                return BadRequest(new { message = "Elige al menos un catálogo: pescado o camarón." });

            var repetida = await _context.ActividadesProceso
                .AnyAsync(a => a.Id != id && a.Nombre.ToLower() == nombre.ToLower());
            if (repetida) return Conflict(new { message = $"Ya existe otra actividad llamada \"{nombre}\"." });

            var nombreAnterior = actividad.Nombre;
            actividad.Nombre = nombre;
            actividad.Pescado = dto.Pescado;
            actividad.Camaron = dto.Camaron;
            actividad.Activa = dto.Activa;
            actividad.Orden = dto.Orden;
            actividad.ActualizadoPor = Limpio(dto.Usuario);
            actividad.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Renombrar no cambia lo ya guardado: los formularios viejos siguen
            // con el nombre anterior y el plan los cruza por ese texto.
            var aviso = nombreAnterior != nombre
                ? $"Los formularios ya guardados siguen diciendo \"{nombreAnterior}\"."
                : null;
            return Ok(new { actividad.Id, actividad.Nombre, actividad.Pescado, actividad.Camaron,
                            actividad.Activa, actividad.Orden, Aviso = aviso });
        }

        /// <summary>Da de baja la actividad. No borra: deja de ofrecerse y nada más.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DarDeBaja(int id, [FromQuery] string? usuario)
        {
            var actividad = await _context.ActividadesProceso.FindAsync(id);
            if (actividad == null) return NotFound(new { message = "La actividad ya no existe." });

            actividad.Activa = false;
            actividad.ActualizadoPor = Limpio(usuario);
            actividad.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { actividad.Id, actividad.Nombre, actividad.Activa });
        }
    }
}
