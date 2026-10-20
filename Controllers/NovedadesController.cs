using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// 📢 NOVEDADES: avisos que un administrador publica para todo el personal.
    ///
    ///   GET    api/Novedades?usuario=X             vigentes, con "leida" para X
    ///   GET    api/Novedades/pendientes?usuario=X  vigentes que X todavía no leyó
    ///          (&soloPantalla=true: solo las GRANDES, las que se muestran al entrar)
    ///   POST   api/Novedades/leidas                { usuario, nombre, ids } → marca leídas
    ///   GET    api/Novedades/admin?rol=admin       todas (también archivadas y vencidas)
    ///   GET    api/Novedades/{id}/lecturas?rol=admin  quién la leyó y cuándo
    ///   POST   api/Novedades                       crear       (solo admin)
    ///   PUT    api/Novedades/{id}                  editar      (solo admin)
    ///   DELETE api/Novedades/{id}?rol=admin        eliminar    (solo admin)
    ///
    /// ⚠️ Igual que el resto de la API, el rol llega desde el frontend (no hay
    /// token que lo certifique). Alcanza para que la pantalla no deje publicar a
    /// quien no es administrador, pero no es una barrera de seguridad real.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class NovedadesController : ControllerBase
    {
        /// <summary>Roles que pueden publicar. Agregar "supervisor" si se decide.</summary>
        private static readonly HashSet<string> RolesQuePublican =
            new(StringComparer.OrdinalIgnoreCase) { "admin" };

        private static readonly HashSet<string> Importancias =
            new(StringComparer.OrdinalIgnoreCase) { "info", "importante", "urgente" };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<NovedadesController> _logger;

        public NovedadesController(ApplicationDbContext context, ILogger<NovedadesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── DTOs ────────────────────────────────────────────────────────

        public class NovedadInputDto
        {
            public string? Titulo { get; set; }
            public string? Mensaje { get; set; }
            public string? Importancia { get; set; }
            public bool Activa { get; set; } = true;
            /// <summary>📺 Novedad grande: pantalla completa al entrar.</summary>
            public bool MostrarAlEntrar { get; set; } = true;
            public DateTime? VisibleDesde { get; set; }
            public DateTime? VisibleHasta { get; set; }
            public string? ImagenBase64 { get; set; }

            /// <summary>Quién publica: usuario de sesión, nombre y rol.</summary>
            public string? Usuario { get; set; }
            public string? Nombre { get; set; }
            public string? Rol { get; set; }

            /// <summary>Al editar: borrar las lecturas para que todos la vean de nuevo.</summary>
            public bool VolverAMostrar { get; set; }
        }

        public class MarcarLeidasDto
        {
            public string? Usuario { get; set; }
            public string? Nombre { get; set; }
            public List<int>? Ids { get; set; }
        }

        // ── Auxiliares ──────────────────────────────────────────────────

        private string? RolDeLaPeticion(string? rolBody = null) =>
            rolBody
            ?? (Request.Headers.TryGetValue("X-Rol", out var h) ? h.ToString() : null)
            ?? (Request.Query.TryGetValue("rol", out var q) ? q.ToString() : null);

        private static bool PuedePublicar(string? rol) =>
            !string.IsNullOrWhiteSpace(rol) && RolesQuePublican.Contains(rol.Trim());

        private static string Usuario(string? u) => (u ?? "").Trim().ToLowerInvariant();

        /// <summary>Activa y dentro de su rango de fechas (las fechas son días completos).</summary>
        private IQueryable<Novedad> Vigentes()
        {
            var ahora = DateTime.Now;
            var hoy = DateTime.Today;
            return _context.Novedades.AsNoTracking()
                .Where(n => n.Activa
                            && (n.VisibleDesde == null || n.VisibleDesde <= ahora)
                            && (n.VisibleHasta == null || n.VisibleHasta >= hoy));
        }

        private static int Orden(string? importancia) => (importancia ?? "").ToLowerInvariant() switch
        {
            "urgente" => 0,
            "importante" => 1,
            _ => 2
        };

        private static object Vista(Novedad n, bool? leida = null, int? lecturas = null) => new
        {
            n.Id,
            n.Titulo,
            n.Mensaje,
            n.Importancia,
            n.Activa,
            n.MostrarAlEntrar,
            n.VisibleDesde,
            n.VisibleHasta,
            n.ImagenBase64,
            n.CreadoPor,
            n.CreadoPorNombre,
            n.ActualizadoPor,
            n.CreatedAt,
            n.UpdatedAt,
            Leida = leida,
            Lecturas = lecturas
        };

        private static string? Validar(NovedadInputDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Titulo)) return "Escribe un título.";
            if (dto.Titulo.Trim().Length > 200) return "El título puede tener hasta 200 caracteres.";
            if (string.IsNullOrWhiteSpace(dto.Mensaje)) return "Escribe el mensaje.";
            if (!string.IsNullOrWhiteSpace(dto.Importancia) && !Importancias.Contains(dto.Importancia.Trim()))
                return "Importancia no válida (info, importante o urgente).";
            if (dto.VisibleDesde.HasValue && dto.VisibleHasta.HasValue
                && dto.VisibleHasta.Value.Date < dto.VisibleDesde.Value.Date)
                return "La fecha de fin es anterior a la de inicio.";
            if (!string.IsNullOrWhiteSpace(dto.ImagenBase64)
                && !dto.ImagenBase64.TrimStart().StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return "La imagen debe enviarse como data URL de imagen válida.";
            return null;
        }

        // ── Lectura (todos) ─────────────────────────────────────────────

        /// <summary>Novedades vigentes, de la más importante y nueva a la más vieja.</summary>
        [HttpGet]
        public async Task<IActionResult> GetVigentes([FromQuery] string? usuario)
        {
            try
            {
                var lista = await Vigentes().ToListAsync();
                var u = Usuario(usuario);
                var leidas = u.Length == 0
                    ? new HashSet<int>()
                    : (await _context.NovedadesLecturas.AsNoTracking()
                        .Where(l => l.Usuario == u)
                        .Select(l => l.NovedadId)
                        .ToListAsync()).ToHashSet();

                return Ok(lista
                    .OrderBy(n => Orden(n.Importancia))
                    .ThenByDescending(n => n.CreatedAt)
                    .Select(n => Vista(n, leida: u.Length == 0 ? null : leidas.Contains(n.Id))));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar novedades");
                return StatusCode(500, new { message = "No se pudieron cargar las novedades." });
            }
        }

        /// <summary>Lo que el usuario tiene que ver antes de entrar.</summary>
        [HttpGet("pendientes")]
        public async Task<IActionResult> GetPendientes([FromQuery] string? usuario, [FromQuery] bool soloPantalla = false)
        {
            var u = Usuario(usuario);
            if (u.Length == 0) return BadRequest(new { message = "Falta el usuario." });

            try
            {
                var consulta = Vigentes()
                    .Where(n => !_context.NovedadesLecturas.Any(l => l.NovedadId == n.Id && l.Usuario == u));
                if (soloPantalla) consulta = consulta.Where(n => n.MostrarAlEntrar);
                var pendientes = await consulta.ToListAsync();

                return Ok(new
                {
                    Total = pendientes.Count,
                    Novedades = pendientes
                        .OrderBy(n => Orden(n.Importancia))
                        .ThenByDescending(n => n.CreatedAt)
                        .Select(n => Vista(n, leida: false))
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar novedades pendientes de {Usuario}", u);
                return StatusCode(500, new { message = "No se pudieron cargar las novedades." });
            }
        }

        /// <summary>Marca como leídas (una constancia por usuario; repetir no duplica).</summary>
        [HttpPost("leidas")]
        public async Task<IActionResult> MarcarLeidas([FromBody] MarcarLeidasDto dto)
        {
            var u = Usuario(dto?.Usuario);
            var ids = (dto?.Ids ?? new List<int>()).Distinct().ToList();
            if (u.Length == 0) return BadRequest(new { message = "Falta el usuario." });
            if (ids.Count == 0) return Ok(new { Marcadas = 0 });

            try
            {
                var existentes = await _context.Novedades.Where(n => ids.Contains(n.Id)).Select(n => n.Id).ToListAsync();
                var yaLeidas = await _context.NovedadesLecturas
                    .Where(l => l.Usuario == u && ids.Contains(l.NovedadId))
                    .Select(l => l.NovedadId)
                    .ToListAsync();

                var nuevas = existentes.Except(yaLeidas).ToList();
                var nombre = string.IsNullOrWhiteSpace(dto!.Nombre) ? null : dto.Nombre.Trim();
                foreach (var id in nuevas)
                    _context.NovedadesLecturas.Add(new NovedadLectura
                    {
                        NovedadId = id, Usuario = u, NombreUsuario = nombre, LeidaEn = DateTime.Now
                    });

                if (nuevas.Count > 0)
                {
                    try
                    {
                        await _context.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Dos pestañas marcando a la vez: el índice único rechaza la
                        // repetida. Ya quedó leída, que es lo que importa.
                        _logger.LogInformation("Lectura de novedad duplicada ignorada ({Usuario})", u);
                    }
                }

                return Ok(new { Marcadas = nuevas.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al marcar novedades leídas de {Usuario}", u);
                return StatusCode(500, new { message = "No se pudo registrar la lectura." });
            }
        }

        // ── Administración ──────────────────────────────────────────────

        /// <summary>Todas, también archivadas y vencidas, con cuántos las leyeron.</summary>
        [HttpGet("admin")]
        public async Task<IActionResult> GetTodas()
        {
            if (!PuedePublicar(RolDeLaPeticion())) return StatusCode(403, new { message = "Solo un administrador puede ver esto." });

            try
            {
                var lista = await _context.Novedades.AsNoTracking()
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();
                var conteo = await _context.NovedadesLecturas.AsNoTracking()
                    .GroupBy(l => l.NovedadId)
                    .Select(g => new { NovedadId = g.Key, Cantidad = g.Count() })
                    .ToDictionaryAsync(x => x.NovedadId, x => x.Cantidad);

                return Ok(lista.Select(n => Vista(n, lecturas: conteo.TryGetValue(n.Id, out var c) ? c : 0)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar novedades (admin)");
                return StatusCode(500, new { message = "No se pudieron cargar las novedades." });
            }
        }

        /// <summary>Quién leyó una novedad y cuándo.</summary>
        [HttpGet("{id:int}/lecturas")]
        public async Task<IActionResult> GetLecturas(int id)
        {
            if (!PuedePublicar(RolDeLaPeticion())) return StatusCode(403, new { message = "Solo un administrador puede ver esto." });

            var lecturas = await _context.NovedadesLecturas.AsNoTracking()
                .Where(l => l.NovedadId == id)
                .OrderBy(l => l.LeidaEn)
                .Select(l => new { l.Usuario, l.NombreUsuario, l.LeidaEn })
                .ToListAsync();
            return Ok(lecturas);
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] NovedadInputDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Faltan los datos." });
            if (!PuedePublicar(RolDeLaPeticion(dto.Rol)))
                return StatusCode(403, new { message = "Solo un administrador puede publicar novedades." });

            var error = Validar(dto);
            if (error != null) return BadRequest(new { message = error });

            var novedad = new Novedad
            {
                Titulo = dto.Titulo!.Trim(),
                Mensaje = dto.Mensaje!.Trim(),
                Importancia = (dto.Importancia ?? "info").Trim().ToLowerInvariant(),
                Activa = dto.Activa,
                MostrarAlEntrar = dto.MostrarAlEntrar,
                VisibleDesde = dto.VisibleDesde?.Date,
                VisibleHasta = dto.VisibleHasta?.Date,
                ImagenBase64 = string.IsNullOrWhiteSpace(dto.ImagenBase64) ? null : dto.ImagenBase64.Trim(),
                CreadoPor = Usuario(dto.Usuario),
                CreadoPorNombre = dto.Nombre?.Trim(),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            try
            {
                _context.Novedades.Add(novedad);
                await _context.SaveChangesAsync();
                _logger.LogInformation("📢 Novedad {Id} publicada por {Usuario}", novedad.Id, novedad.CreadoPor);
                return Ok(Vista(novedad, lecturas: 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear novedad");
                return StatusCode(500, new { message = "No se pudo publicar la novedad." });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Editar(int id, [FromBody] NovedadInputDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Faltan los datos." });
            if (!PuedePublicar(RolDeLaPeticion(dto.Rol)))
                return StatusCode(403, new { message = "Solo un administrador puede editar novedades." });

            var error = Validar(dto);
            if (error != null) return BadRequest(new { message = error });

            var novedad = await _context.Novedades.FindAsync(id);
            if (novedad == null) return NotFound(new { message = "La novedad ya no existe." });

            novedad.Titulo = dto.Titulo!.Trim();
            novedad.Mensaje = dto.Mensaje!.Trim();
            novedad.Importancia = (dto.Importancia ?? "info").Trim().ToLowerInvariant();
            novedad.Activa = dto.Activa;
            novedad.MostrarAlEntrar = dto.MostrarAlEntrar;
            novedad.VisibleDesde = dto.VisibleDesde?.Date;
            novedad.VisibleHasta = dto.VisibleHasta?.Date;
            novedad.ImagenBase64 = string.IsNullOrWhiteSpace(dto.ImagenBase64) ? null : dto.ImagenBase64.Trim();
            novedad.ActualizadoPor = string.IsNullOrWhiteSpace(dto.Nombre) ? Usuario(dto.Usuario) : dto.Nombre.Trim();
            novedad.UpdatedAt = DateTime.Now;

            try
            {
                if (dto.VolverAMostrar)
                {
                    var lecturas = _context.NovedadesLecturas.Where(l => l.NovedadId == id);
                    _context.NovedadesLecturas.RemoveRange(lecturas);
                }
                await _context.SaveChangesAsync();
                var total = await _context.NovedadesLecturas.CountAsync(l => l.NovedadId == id);
                return Ok(Vista(novedad, lecturas: total));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar novedad {Id}", id);
                return StatusCode(500, new { message = "No se pudo guardar el cambio." });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id)
        {
            if (!PuedePublicar(RolDeLaPeticion()))
                return StatusCode(403, new { message = "Solo un administrador puede eliminar novedades." });

            var novedad = await _context.Novedades.FindAsync(id);
            if (novedad == null) return NotFound(new { message = "La novedad ya no existe." });

            try
            {
                // Las lecturas se borran en cascada (clave foránea).
                _context.NovedadesLecturas.RemoveRange(_context.NovedadesLecturas.Where(l => l.NovedadId == id));
                _context.Novedades.Remove(novedad);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar novedad {Id}", id);
                return StatusCode(500, new { message = "No se pudo eliminar la novedad." });
            }
        }
    }
}
