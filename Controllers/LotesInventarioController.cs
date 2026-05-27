using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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

        // ── POST /api/LotesInventario/sincronizar-desde-formularios ──────────
        /// <summary>
        /// Escanea todos los FilledForms guardados en la BD, extrae los números de lote
        /// que aparecen en HeaderData y en BodyData (tablas), y los registra en LotesInventario
        /// si todavía no existen (evita duplicados). Devuelve un resumen del proceso.
        /// </summary>
        [HttpPost("sincronizar-desde-formularios")]
        public async Task<ActionResult<object>> SincronizarDesdeFormularios()
        {
            var ahora = DateTime.UtcNow;

            // Cargar formularios junto con su template (nombre, proceso)
            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .AsNoTracking()
                .Select(f => new
                {
                    f.FormID,
                    f.TemplateID,
                    TemplateNombre = f.Template != null ? f.Template.Nombre : "",
                    TemplateProceso = f.Template != null ? f.Template.Proceso : "",
                    f.HeaderData,
                    f.BodyData,
                    f.TipoProducto,
                    f.CreatedAt
                })
                .ToListAsync();

            // Lotes ya existentes en inventario (para omitirlos)
            var existentes = await _context.LotesInventario
                .Select(l => l.NumeroLote)
                .ToHashSetAsync();

            // Mapa: numeroLote → info contextual para crear el registro
            var lotesDetectados = new Dictionary<string, LoteDetectadoInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var form in forms)
            {
                var proceso = form.TemplateProceso ?? form.TemplateNombre ?? "Sin proceso";
                var fechaForm = form.CreatedAt.Date;

                // ── Escanear HeaderData ──────────────────────────────────────
                if (!string.IsNullOrWhiteSpace(form.HeaderData))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(form.HeaderData);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (!prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var val = prop.Value.ValueKind == JsonValueKind.String
                                ? prop.Value.GetString()?.Trim()
                                : prop.Value.ToString()?.Trim();

                            if (string.IsNullOrWhiteSpace(val)) continue;

                            // Puede ser un array de entradas (lote_entrante)
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var entry in prop.Value.EnumerateArray())
                                {
                                    if (entry.ValueKind == JsonValueKind.Object)
                                    {
                                        foreach (var ep in entry.EnumerateObject())
                                        {
                                            if (ep.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var lv = ep.Value.ToString()?.Trim();
                                                if (!string.IsNullOrWhiteSpace(lv))
                                                    RegistrarDetectado(lotesDetectados, lv, proceso, form.TipoProducto, fechaForm, form.FormID, form.TemplateID);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                RegistrarDetectado(lotesDetectados, val, proceso, form.TipoProducto, fechaForm, form.FormID, form.TemplateID);
                            }
                        }
                    }
                    catch { /* JSON inválido → ignorar */ }
                }

                // ── Escanear BodyData (tablas) ───────────────────────────────
                if (!string.IsNullOrWhiteSpace(form.BodyData))
                {
                    try
                    {
                        using var doc2 = JsonDocument.Parse(form.BodyData);
                        if (doc2.RootElement.ValueKind != JsonValueKind.Array) continue;

                        foreach (var element in doc2.RootElement.EnumerateArray())
                        {
                            // Elementos con data (tablas, lotes por fila, etc.)
                            JsonElement dataArr = default;
                            bool hasData = element.TryGetProperty("data", out dataArr)
                                          && dataArr.ValueKind == JsonValueKind.Array;
                            if (!hasData) continue;

                            foreach (var row in dataArr.EnumerateArray())
                            {
                                if (row.ValueKind != JsonValueKind.Object) continue;

                                string? productoFila = null;
                                string? clasificacionFila = null;
                                var lotesFila = new List<string>();

                                foreach (var prop in row.EnumerateObject())
                                {
                                    var keyUp = prop.Name.ToUpperInvariant();
                                    var valStr = prop.Value.ValueKind == JsonValueKind.String
                                        ? prop.Value.GetString()?.Trim()
                                        : prop.Value.ToString()?.Trim();

                                    if (string.IsNullOrWhiteSpace(valStr)) continue;

                                    if (keyUp.Contains("LOTE"))
                                        lotesFila.Add(valStr);
                                    else if (keyUp.Contains("PRODUCTO") && productoFila == null)
                                        productoFila = valStr;
                                    else if (keyUp.Contains("CLASIF") && clasificacionFila == null)
                                        clasificacionFila = valStr;
                                }

                                foreach (var lv in lotesFila)
                                {
                                    if (!string.IsNullOrWhiteSpace(lv))
                                    {
                                        RegistrarDetectado(lotesDetectados, lv, proceso,
                                            productoFila ?? form.TipoProducto,
                                            fechaForm, form.FormID, form.TemplateID,
                                            clasificacionFila);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* JSON inválido → ignorar */ }
                }
            }

            // ── Filtrar los ya existentes y crear los nuevos ─────────────────
            var nuevos = new List<LoteInventario>();
            var yaExistentes = 0;

            foreach (var kvp in lotesDetectados)
            {
                if (existentes.Contains(kvp.Key))
                {
                    yaExistentes++;
                    continue;
                }

                var info = kvp.Value;
                nuevos.Add(new LoteInventario
                {
                    NumeroLote = kvp.Key,
                    Proceso    = info.Proceso ?? "Sin proceso",
                    Producto   = info.Producto,
                    Clasificacion = info.Clasificacion,
                    PesoEntrada = 0,
                    Desperdicio = 0,
                    PesoNeto    = 0,
                    Estado      = "disponible",
                    FormId      = info.FormId,
                    TemplateId  = info.TemplateId.ToString(),
                    Fecha       = info.Fecha ?? ahora.Date,
                    Notas       = "Importado automáticamente desde formularios",
                    CreadoEn    = ahora,
                    ActualizadoEn = ahora
                });

                // Agregar al set para evitar crear duplicados dentro del mismo batch
                existentes.Add(kvp.Key);
            }

            if (nuevos.Count > 0)
            {
                _context.LotesInventario.AddRange(nuevos);
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                totalFormulariosEscaneados = forms.Count,
                lotesDetectadosEnJson      = lotesDetectados.Count,
                lotesNuevosRegistrados     = nuevos.Count,
                lotesYaExistentes          = yaExistentes,
                lotes = nuevos.Select(l => new
                {
                    l.NumeroLote,
                    l.Proceso,
                    l.Producto,
                    l.Clasificacion,
                    l.Fecha,
                    l.FormId
                })
            });
        }

        // ── Helper interno: registrar o enriquecer un lote detectado ──────────
        private static void RegistrarDetectado(
            Dictionary<string, LoteDetectadoInfo> dict,
            string numeroLote,
            string? proceso,
            string? producto,
            DateTime? fecha,
            int formId,
            int templateId,
            string? clasificacion = null)
        {
            // Rechazar valores que claramente no son números de lote
            if (numeroLote.Length < 2 || numeroLote.Length > 100) return;

            if (!dict.ContainsKey(numeroLote))
            {
                dict[numeroLote] = new LoteDetectadoInfo
                {
                    Proceso       = proceso,
                    Producto      = producto,
                    Clasificacion = clasificacion,
                    Fecha         = fecha,
                    FormId        = formId,
                    TemplateId    = templateId
                };
            }
            else
            {
                // Enriquecer con datos adicionales si llegan después
                var info = dict[numeroLote];
                if (string.IsNullOrWhiteSpace(info.Producto) && !string.IsNullOrWhiteSpace(producto))
                    info.Producto = producto;
                if (string.IsNullOrWhiteSpace(info.Clasificacion) && !string.IsNullOrWhiteSpace(clasificacion))
                    info.Clasificacion = clasificacion;
            }
        }

        // ── Clase auxiliar (interna al controller) ────────────────────────────
        private class LoteDetectadoInfo
        {
            public string? Proceso       { get; set; }
            public string? Producto      { get; set; }
            public string? Clasificacion { get; set; }
            public DateTime? Fecha       { get; set; }
            public int FormId            { get; set; }
            public int TemplateId        { get; set; }
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
