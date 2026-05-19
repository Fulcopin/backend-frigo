using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// API completa para el inventario de lotes de trazabilidad.
    /// Regla fundamental: PesoNeto = PesoEntrada - Desperdicio
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class LotesInventarioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LotesInventarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── GET /api/LotesInventario ──────────────────────────────────────────
        /// <summary>
        /// Lista todos los lotes. Soporta filtros opcionales:
        /// ?estado=disponible|consumido|parcial
        /// ?proceso=Fileteo
        /// ?lote=260511
        /// ?desde=2026-01-01&hasta=2026-12-31
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LoteInventario>>> GetAll(
            [FromQuery] string? estado,
            [FromQuery] string? proceso,
            [FromQuery] string? lote,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta)
        {
            var query = _context.LotesInventario.AsQueryable();

            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(l => l.Estado == estado);

            if (!string.IsNullOrWhiteSpace(proceso))
                query = query.Where(l => l.Proceso.Contains(proceso));

            if (!string.IsNullOrWhiteSpace(lote))
                query = query.Where(l => l.NumeroLote.Contains(lote));

            if (desde.HasValue)
                query = query.Where(l => l.Fecha >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(l => l.Fecha <= hasta.Value);

            return await query
                .OrderByDescending(l => l.CreadoEn)
                .ToListAsync();
        }

        // ── GET /api/LotesInventario/disponibles ──────────────────────────────
        [HttpGet("disponibles")]
        public async Task<ActionResult<IEnumerable<LoteInventario>>> GetDisponibles()
        {
            return await _context.LotesInventario
                .Where(l => l.Estado == "disponible" || l.Estado == "parcial")
                .OrderByDescending(l => l.CreadoEn)
                .ToListAsync();
        }

        // ── GET /api/LotesInventario/{id} ─────────────────────────────────────
        [HttpGet("{id:int}")]
        public async Task<ActionResult<LoteInventario>> GetById(int id)
        {
            var lote = await _context.LotesInventario.FindAsync(id);
            if (lote == null) return NotFound(new { message = $"Lote con ID {id} no encontrado." });
            return lote;
        }

        // ── GET /api/LotesInventario/numero/{numeroLote} ──────────────────────
        [HttpGet("numero/{numeroLote}")]
        public async Task<ActionResult<LoteInventario>> GetByNumero(string numeroLote)
        {
            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == numeroLote);
            if (lote == null) return NotFound(new { message = $"Lote '{numeroLote}' no encontrado." });
            return lote;
        }

        // ── GET /api/LotesInventario/arbol/{numeroLote} ───────────────────────
        /// <summary>Devuelve el árbol completo de descendencia de un lote.</summary>
        [HttpGet("arbol/{numeroLote}")]
        public async Task<ActionResult<object>> GetArbol(string numeroLote)
        {
            // Traer todos los lotes de la BD para construir el árbol en memoria
            var todos = await _context.LotesInventario.ToListAsync();
            var nodo = BuildArbol(numeroLote, todos);
            if (nodo == null) return NotFound(new { message = $"Lote '{numeroLote}' no encontrado." });
            return Ok(nodo);
        }

        // ── GET /api/LotesInventario/stats ───────────────────────────────────
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            var lotes = await _context.LotesInventario.ToListAsync();
            return Ok(new
            {
                total = lotes.Count,
                disponibles = lotes.Count(l => l.Estado == "disponible" || l.Estado == "parcial"),
                consumidos = lotes.Count(l => l.Estado == "consumido"),
                totalLbsDisponibles = lotes
                    .Where(l => l.Estado == "disponible" || l.Estado == "parcial")
                    .Sum(l => l.PesoNeto),
                totalLbsProcesadas = lotes.Sum(l => l.PesoEntrada)
            });
        }

        // ── POST /api/LotesInventario ─────────────────────────────────────────
        [HttpPost]
        public async Task<ActionResult<LoteInventario>> Create([FromBody] LoteInventarioCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validar que el numero de lote no exista
            var existe = await _context.LotesInventario
                .AnyAsync(l => l.NumeroLote == dto.NumeroLote);
            if (existe)
                return Conflict(new { message = $"Ya existe un lote con número '{dto.NumeroLote}'." });

            var pesoNeto = Math.Max(0, dto.PesoEntrada - dto.Desperdicio);

            var lote = new LoteInventario
            {
                NumeroLote = dto.NumeroLote.Trim(),
                Proceso = dto.Proceso.Trim(),
                Producto = dto.Producto?.Trim(),
                Clasificacion = dto.Clasificacion?.Trim(),
                PesoEntrada = dto.PesoEntrada,
                Desperdicio = dto.Desperdicio,
                TipoDesperdicio = dto.TipoDesperdicio?.Trim(),
                PesoNeto = pesoNeto,
                Estado = "disponible",
                LotePadre = dto.LotePadre?.Trim(),
                FormId = dto.FormId,
                TemplateId = dto.TemplateId?.Trim(),
                Fecha = dto.Fecha ?? DateTime.UtcNow.Date,
                Notas = dto.Notas?.Trim(),
                CreadoEn = DateTime.UtcNow,
                ActualizadoEn = DateTime.UtcNow
            };

            _context.LotesInventario.Add(lote);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = lote.Id }, lote);
        }

        // ── POST /api/LotesInventario/bulk ────────────────────────────────────
        /// <summary>
        /// Guarda múltiples lotes en una sola llamada.
        /// Opcionalmente marca el LoteOrigenConsumir como consumido.
        /// </summary>
        [HttpPost("bulk")]
        public async Task<ActionResult<IEnumerable<LoteInventario>>> CreateBulk([FromBody] LoteBulkCreateDto dto)
        {
            if (dto.Lotes == null || dto.Lotes.Count == 0)
                return BadRequest(new { message = "La lista de lotes está vacía." });

            var ahora = DateTime.UtcNow;
            var creados = new List<LoteInventario>();

            foreach (var d in dto.Lotes)
            {
                if (string.IsNullOrWhiteSpace(d.NumeroLote) || string.IsNullOrWhiteSpace(d.Proceso))
                    continue;

                // Saltar duplicados silenciosamente
                var existe = await _context.LotesInventario
                    .AnyAsync(l => l.NumeroLote == d.NumeroLote);
                if (existe) continue;

                var pesoNeto = Math.Max(0, d.PesoEntrada - d.Desperdicio);
                var lote = new LoteInventario
                {
                    NumeroLote = d.NumeroLote.Trim(),
                    Proceso = d.Proceso.Trim(),
                    Producto = d.Producto?.Trim(),
                    Clasificacion = d.Clasificacion?.Trim(),
                    PesoEntrada = d.PesoEntrada,
                    Desperdicio = d.Desperdicio,
                    TipoDesperdicio = d.TipoDesperdicio?.Trim(),
                    PesoNeto = pesoNeto,
                    Estado = "disponible",
                    LotePadre = d.LotePadre?.Trim(),
                    FormId = d.FormId,
                    TemplateId = d.TemplateId?.Trim(),
                    Fecha = d.Fecha ?? ahora.Date,
                    Notas = d.Notas?.Trim(),
                    CreadoEn = ahora,
                    ActualizadoEn = ahora
                };
                _context.LotesInventario.Add(lote);
                creados.Add(lote);
            }

            // Consumir el lote de origen si se especificó
            if (!string.IsNullOrWhiteSpace(dto.LoteOrigenConsumir))
            {
                var origen = await _context.LotesInventario
                    .FirstOrDefaultAsync(l => l.NumeroLote == dto.LoteOrigenConsumir);
                if (origen != null)
                {
                    origen.Estado = "consumido";
                    origen.ActualizadoEn = ahora;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { creados = creados.Count, lotes = creados });
        }

        // ── PUT /api/LotesInventario/{id} ──────────────────────────────────────
        [HttpPut("{id:int}")]
        public async Task<ActionResult<LoteInventario>> Update(int id, [FromBody] LoteInventarioUpdateDto dto)
        {
            var lote = await _context.LotesInventario.FindAsync(id);
            if (lote == null) return NotFound(new { message = $"Lote con ID {id} no encontrado." });

            if (dto.Producto != null)         lote.Producto = dto.Producto.Trim();
            if (dto.Clasificacion != null)    lote.Clasificacion = dto.Clasificacion.Trim();
            if (dto.TipoDesperdicio != null)  lote.TipoDesperdicio = dto.TipoDesperdicio.Trim();
            if (dto.Notas != null)            lote.Notas = dto.Notas.Trim();

            if (dto.Estado != null)
            {
                var estadosValidos = new[] { "disponible", "consumido", "parcial" };
                if (!estadosValidos.Contains(dto.Estado))
                    return BadRequest(new { message = $"Estado inválido: '{dto.Estado}'. Use: disponible, consumido, parcial." });
                lote.Estado = dto.Estado;
            }

            if (dto.PesoEntrada.HasValue || dto.Desperdicio.HasValue)
            {
                if (dto.PesoEntrada.HasValue) lote.PesoEntrada = dto.PesoEntrada.Value;
                if (dto.Desperdicio.HasValue) lote.Desperdicio = dto.Desperdicio.Value;
                lote.PesoNeto = Math.Max(0, lote.PesoEntrada - lote.Desperdicio);
            }

            lote.ActualizadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── PUT /api/LotesInventario/{id}/consumir ────────────────────────────
        [HttpPut("{id:int}/consumir")]
        public async Task<ActionResult<LoteInventario>> Consumir(int id)
        {
            var lote = await _context.LotesInventario.FindAsync(id);
            if (lote == null) return NotFound(new { message = $"Lote ID {id} no encontrado." });
            lote.Estado = "consumido";
            lote.ActualizadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── PUT /api/LotesInventario/consumir-por-numero/{numeroLote} ─────────
        [HttpPut("consumir-por-numero/{numeroLote}")]
        public async Task<ActionResult<LoteInventario>> ConsumirPorNumero(string numeroLote)
        {
            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == numeroLote);
            if (lote == null) return NotFound(new { message = $"Lote '{numeroLote}' no encontrado." });
            lote.Estado = "consumido";
            lote.ActualizadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── PUT /api/LotesInventario/{id}/liberar ─────────────────────────────
        [HttpPut("{id:int}/liberar")]
        public async Task<ActionResult<LoteInventario>> Liberar(int id)
        {
            var lote = await _context.LotesInventario.FindAsync(id);
            if (lote == null) return NotFound(new { message = $"Lote ID {id} no encontrado." });
            lote.Estado = "disponible";
            lote.ActualizadoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── DELETE /api/LotesInventario/{id} ──────────────────────────────────
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var lote = await _context.LotesInventario.FindAsync(id);
            if (lote == null) return NotFound(new { message = $"Lote ID {id} no encontrado." });
            _context.LotesInventario.Remove(lote);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── DELETE /api/LotesInventario/numero/{numeroLote} ───────────────────
        [HttpDelete("numero/{numeroLote}")]
        public async Task<IActionResult> DeleteByNumero(string numeroLote)
        {
            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == numeroLote);
            if (lote == null) return NotFound(new { message = $"Lote '{numeroLote}' no encontrado." });
            _context.LotesInventario.Remove(lote);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static object? BuildArbol(string numeroLote, List<LoteInventario> todos)
        {
            var raiz = todos.FirstOrDefault(l => l.NumeroLote == numeroLote);
            if (raiz == null) return null;

            var hijos = todos
                .Where(l => l.LotePadre == numeroLote)
                .Select(h => BuildArbol(h.NumeroLote, todos))
                .Where(h => h != null)
                .ToList();

            return new
            {
                raiz.Id,
                lote = raiz.NumeroLote,
                raiz.Proceso,
                raiz.Producto,
                raiz.Clasificacion,
                raiz.PesoEntrada,
                raiz.Desperdicio,
                raiz.PesoNeto,
                raiz.Estado,
                raiz.LotePadre,
                raiz.Fecha,
                hijos
            };
        }
    }
}
