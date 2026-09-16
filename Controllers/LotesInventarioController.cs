using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Globalization;
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
        /// <summary>
        /// Lotes con saldo disponible. Se puede filtrar por el proceso que los generó,
        /// para que un proceso posterior vea solo lo que salió del anterior.
        /// Ej: PD-06 (corte) pide ?proceso=Fileteo para ver los lotes de PD-04.
        /// </summary>
        [HttpGet("disponibles")]
        public async Task<ActionResult<IEnumerable<LoteInventario>>> GetDisponibles([FromQuery] string? proceso)
        {
            var query = _context.LotesInventario
                .Where(l => (l.Estado == "disponible" || l.Estado == "parcial") && l.Saldo > 0);

            if (!string.IsNullOrWhiteSpace(proceso))
                query = query.Where(l => l.Proceso.Contains(proceso));

            return await query
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
        /// <summary>
        /// Devuelve el árbol completo de descendencia de un lote. Cada nodo trae,
        /// además de sus propios datos, el peso neto acumulado de sus hojas finales
        /// (para ver cuánto rindió esa materia prima en total) y el % de rendimiento
        /// acumulado respecto al peso de entrada de la raíz.
        /// </summary>
        [HttpGet("arbol/{numeroLote}")]
        public async Task<ActionResult<object>> GetArbol(string numeroLote)
        {
            // Traer todos los lotes de la BD para construir el árbol en memoria
            var todos = await _context.LotesInventario.ToListAsync();
            var nodo = BuildArbol(numeroLote, todos);
            if (nodo == null) return NotFound(new { message = $"Lote '{numeroLote}' no encontrado." });

            // El % se calcula sobre el peso de ENTRADA de la raíz (la materia prima
            // original); si la raíz no tiene peso de entrada registrado (lote
            // manual sin ese dato), se usa su peso neto como referencia.
            var pesoReferenciaRaiz = nodo.PesoEntrada > 0 ? nodo.PesoEntrada : nodo.PesoNeto;
            AplicarRendimientoAcumulado(nodo, pesoReferenciaRaiz);

            return Ok(nodo);
        }

        // ── GET /api/LotesInventario/raices ───────────────────────────────────
        /// <summary>
        /// Lotes "raíz" (materia prima): los que no tienen LotePadre. Sirve para
        /// poblar un selector y elegir la materia prima sin escribir el número a
        /// mano antes de ver su árbol de trazabilidad.
        /// </summary>
        [HttpGet("raices")]
        public async Task<ActionResult<object>> GetRaices([FromQuery] string? proceso)
        {
            var query = _context.LotesInventario
                .Where(l => string.IsNullOrEmpty(l.LotePadre));

            if (!string.IsNullOrWhiteSpace(proceso))
                query = query.Where(l => l.Proceso.Contains(proceso));

            var raices = await query
                .OrderByDescending(l => l.CreadoEn)
                .Select(l => new
                {
                    l.Id,
                    lote = l.NumeroLote,
                    l.Proceso,
                    l.Producto,
                    l.PesoEntrada,
                    l.PesoNeto,
                    l.Estado,
                    l.Fecha
                })
                .ToListAsync();

            return Ok(raices);
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
                Saldo = pesoNeto, // el saldo arranca igual al peso neto disponible
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

            // Movimiento de ENTRADA (queda registrado en el libro mayor)
            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                LoteInventarioId = lote.Id, NumeroLote = lote.NumeroLote, Tipo = "entrada",
                Cantidad = pesoNeto, SaldoResultante = pesoNeto, Proceso = lote.Proceso,
                FormId = lote.FormId, Notas = "Ingreso del lote", CreadoEn = DateTime.UtcNow
            });
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
                    Saldo = pesoNeto, // el saldo arranca igual al peso neto disponible
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

        // ── POST /api/LotesInventario/consumir-cantidad ───────────────────────
        /// <summary>
        /// Da SALIDA a una cantidad (Lbs) de un lote desde un proceso posterior.
        /// Baja el Saldo, registra el movimiento y ajusta el Estado:
        ///   saldo == peso neto  → disponible
        ///   0 < saldo < neto     → parcial
        ///   saldo == 0           → consumido (descartado del disponible)
        /// Rechaza si la cantidad supera el saldo (no deja el inventario en negativo).
        /// </summary>
        [HttpPost("consumir-cantidad")]
        public async Task<ActionResult<LoteInventario>> ConsumirCantidad([FromBody] ConsumirCantidadDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NumeroLote))
                return BadRequest(new { message = "Falta el número de lote." });
            if (dto.Cantidad <= 0)
                return BadRequest(new { message = "La cantidad a consumir debe ser mayor a 0." });

            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == dto.NumeroLote);
            if (lote == null)
                return NotFound(new { message = $"Lote '{dto.NumeroLote}' no encontrado en el inventario." });

            if (dto.Cantidad > lote.Saldo)
                return BadRequest(new
                {
                    message = $"No hay saldo suficiente en el lote '{lote.NumeroLote}'. " +
                              $"Saldo disponible: {lote.Saldo} Lbs, se intentó consumir: {dto.Cantidad} Lbs.",
                    saldoDisponible = lote.Saldo
                });

            lote.Saldo -= dto.Cantidad;
            lote.Estado = lote.Saldo <= 0 ? "consumido"
                        : lote.Saldo < lote.PesoNeto ? "parcial"
                        : "disponible";
            lote.ActualizadoEn = DateTime.UtcNow;

            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                LoteInventarioId = lote.Id,
                NumeroLote = lote.NumeroLote,
                Tipo = "salida",
                Cantidad = dto.Cantidad,
                SaldoResultante = lote.Saldo,
                Proceso = dto.Proceso,
                FormId = dto.FormId,
                Notas = dto.Notas,
                CreadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── POST /api/LotesInventario/registrar-traspaso ───────────────────────
        /// <summary>
        /// Deja constancia de que una cantidad pasó a otro proceso SIN tocar el saldo.
        ///
        /// Es para los formularios que solo declaran un movimiento ("pasaron 2000 Lbs
        /// a corte") pero donde el producto todavía no salió del inventario: el lote
        /// sigue estando disponible completo y el movimiento queda para la traza.
        /// A diferencia de consumir-cantidad, acá NO se resta ni cambia el estado.
        /// </summary>
        [HttpPost("registrar-traspaso")]
        public async Task<ActionResult<LoteInventario>> RegistrarTraspaso([FromBody] ConsumirCantidadDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NumeroLote))
                return BadRequest(new { message = "Falta el número de lote." });
            if (dto.Cantidad <= 0)
                return BadRequest(new { message = "La cantidad a registrar debe ser mayor a 0." });

            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == dto.NumeroLote);
            if (lote == null)
                return NotFound(new { message = $"Lote '{dto.NumeroLote}' no encontrado en el inventario." });

            // Sin validación de saldo a propósito: no se está sacando nada, así que
            // un traspaso mayor al saldo es legítimo (ej. producto que se glasea).
            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                LoteInventarioId = lote.Id,
                NumeroLote = lote.NumeroLote,
                Tipo = "traspaso",
                Cantidad = dto.Cantidad,
                SaldoResultante = lote.Saldo,   // queda igual: no se descontó
                Proceso = dto.Proceso,
                FormId = dto.FormId,
                Notas = dto.Notas,
                CreadoEn = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── GET /api/LotesInventario/movimientos/{numeroLote} ─────────────────
        /// <summary>Historial de entradas/salidas de un lote (kardex).</summary>
        [HttpGet("movimientos/{numeroLote}")]
        public async Task<ActionResult<IEnumerable<MovimientoInventario>>> GetMovimientos(string numeroLote)
        {
            return await _context.MovimientosInventario
                .Where(m => m.NumeroLote == numeroLote)
                .OrderBy(m => m.CreadoEn)
                .ToListAsync();
        }

        // ── GET /api/LotesInventario/movimientos ──────────────────────────────
        /// <summary>
        /// Kardex GLOBAL: todos los movimientos del período, ya cruzados con el
        /// lote para saber de qué producto y proceso son. Sin esto habría que
        /// pedir el kardex lote por lote (cientos de llamadas) para armar el
        /// movimiento de un producto.
        ///
        /// Filtros: ?desde=2026-01-01&amp;hasta=2026-12-31&amp;producto=Mahi&amp;tipo=salida&amp;lote=260511
        /// </summary>
        /// <remarks>
        /// Se sirve de a tandas (pagina/tamano): un período largo son decenas de
        /// miles de movimientos y traerlos todos de un saque ahoga al servidor.
        /// Con tamano = 0 devuelve todo junto, como venía haciéndose.
        /// </remarks>
        [HttpGet("movimientos")]
        public async Task<ActionResult> GetMovimientosGlobal(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] string? producto,
            [FromQuery] string? tipo,
            [FromQuery] string? lote,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamano = 0)
        {
            var movs = _context.MovimientosInventario.AsQueryable();

            if (desde.HasValue) movs = movs.Where(m => m.CreadoEn >= desde.Value);
            // El 'hasta' llega como fecha suelta: se incluye el día completo.
            if (hasta.HasValue) movs = movs.Where(m => m.CreadoEn < hasta.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(tipo))  movs = movs.Where(m => m.Tipo == tipo);
            if (!string.IsNullOrWhiteSpace(lote))  movs = movs.Where(m => m.NumeroLote.Contains(lote));

            // El producto vive en el lote, no en el movimiento: se cruza por número.
            var consulta = from m in movs
                           join l in _context.LotesInventario
                                on m.NumeroLote equals l.NumeroLote into ls
                           from l in ls.DefaultIfEmpty()
                           select new
                           {
                               m.Id,
                               m.NumeroLote,
                               m.Tipo,
                               m.Cantidad,
                               m.SaldoResultante,
                               m.Proceso,
                               m.FormId,
                               m.Notas,
                               m.CreadoEn,
                               Producto        = l != null ? l.Producto        : null,
                               Clasificacion   = l != null ? l.Clasificacion   : null,
                               ProcesoLote     = l != null ? l.Proceso         : null,
                               LotePadre       = l != null ? l.LotePadre       : null,
                               FechaLote       = l != null ? l.Fecha           : null
                           };

            if (!string.IsNullOrWhiteSpace(producto))
                consulta = consulta.Where(x => x.Producto != null && x.Producto.Contains(producto));

            // El kardex arma saldos corridos, así que el orden por fecha es parte
            // del dato: las tandas tienen que respetarlo.
            var ordenada = consulta.OrderBy(x => x.CreadoEn).ThenBy(x => x.Id);

            if (tamano <= 0) return Ok(await ordenada.ToListAsync());

            const int TAMANO_MAXIMO = 2000;
            if (tamano > TAMANO_MAXIMO) tamano = TAMANO_MAXIMO;
            if (pagina < 1) pagina = 1;

            var total = await consulta.CountAsync();
            var datos = await ordenada.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();

            return Ok(new
            {
                total,
                pagina,
                tamano,
                hayMas = pagina * tamano < total,
                datos,
            });
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

        // ── GET /api/LotesInventario/cambio-proceso-resumen ───────────────────
        /// <summary>
        /// Resumen de movimientos de "Cambio de Proceso" por lote.
        /// Devuelve un diccionario: { [numeroLote]: { totalLbs, destinos: [{proceso, lbs}] } }
        /// Solo incluye los movimientos cuyas notas contienen "#cambio_proceso:".
        /// </summary>
        [HttpGet("cambio-proceso-resumen")]
        public async Task<ActionResult> GetCambioProcesoResumen()
        {
            // Dos formas de mover un lote de proceso, y las dos cuentan acá:
            //   #cambio_proceso:  descuenta el saldo   (Tipo = "salida")
            //   #traspaso:        NO descuenta el saldo (Tipo = "traspaso")
            // Antes solo se miraba la primera, así que los traspasos quedaban
            // invisibles en la grilla aunque el movimiento estuviera registrado.
            var movimientos = await _context.MovimientosInventario
                .Where(m => m.Notas != null
                            && (m.Notas.Contains("#cambio_proceso:") || m.Notas.Contains("#traspaso:")))
                .Select(m => new { m.NumeroLote, m.Cantidad, m.Proceso, m.Notas, m.Tipo })
                .ToListAsync();

            var resumen = new Dictionary<string, object>();
            foreach (var m in movimientos)
            {
                // Extrae el destino de "#cambio_proceso:<dest>" o "#traspaso:<dest>"
                var destino = m.Proceso ?? "otro";
                var notas = m.Notas ?? "";
                var etiqueta = notas.Contains("#traspaso:") ? "#traspaso:" : "#cambio_proceso:";
                var idx = notas.IndexOf(etiqueta, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    var rest = notas.Substring(idx + etiqueta.Length);
                    var end = rest.IndexOf(' ');
                    destino = end >= 0 ? rest.Substring(0, end) : rest;
                    if (string.IsNullOrWhiteSpace(destino)) destino = m.Proceso ?? "otro";
                }

                if (!resumen.TryGetValue(m.NumeroLote, out var entryObj))
                {
                    var entry = new { totalLbs = (double)m.Cantidad, destinos = new Dictionary<string, double> { { destino, (double)m.Cantidad } } };
                    resumen[m.NumeroLote] = entry;
                }
                else
                {
                    // rebuild mutable
                    var entry = (dynamic)entryObj;
                    var destinos = (Dictionary<string, double>)entry.destinos;
                    destinos.TryGetValue(destino, out var prev);
                    destinos[destino] = prev + (double)m.Cantidad;
                    resumen[m.NumeroLote] = new { totalLbs = (double)entry.totalLbs + (double)m.Cantidad, destinos };
                }
            }

            return Ok(resumen);
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

            // ── Comparar contra el inventario. NO se escribe nada ─────────────
            //
            // Antes acá se daban de alta los lotes detectados, con peso 0 porque el
            // barrido nunca leyó una cantidad. Eso llenaba el inventario de filas
            // fantasma: el PD-05 solo MENCIONA lotes que ya existen desde fileteo, y
            // terminaban registrados como si él los hubiera producido.
            //
            // El inventario ahora se llena al guardar el formulario, según el rol que
            // la plantilla declara para cada tabla (consumir / traspasar / producir).
            // Este endpoint quedó como diagnóstico: dice qué lotes nombran los
            // formularios y cuáles de esos no están en inventario, para que alguien
            // decida qué hacer. No los crea.
            var faltantes = new List<object>();
            var yaExistentes = 0;

            foreach (var kvp in lotesDetectados)
            {
                if (existentes.Contains(kvp.Key))
                {
                    yaExistentes++;
                    continue;
                }

                var info = kvp.Value;
                faltantes.Add(new
                {
                    NumeroLote    = kvp.Key,
                    Proceso       = info.Proceso ?? "Sin proceso",
                    info.Producto,
                    info.Clasificacion,
                    Fecha         = info.Fecha ?? ahora.Date,
                    info.FormId,
                    TemplateId    = info.TemplateId.ToString()
                });
            }

            return Ok(new
            {
                soloLectura                = true,
                mensaje                    = "Diagnóstico: no se creó ni modificó ningún lote. " +
                                             "El inventario se alimenta al guardar cada formulario.",
                totalFormulariosEscaneados = forms.Count,
                lotesDetectadosEnJson      = lotesDetectados.Count,
                lotesYaExistentes          = yaExistentes,
                lotesSinRegistrar          = faltantes.Count,
                // Nombre viejo, en 0, para no romper pantallas que lo lean.
                lotesNuevosRegistrados     = 0,
                lotes                      = faltantes
            });
        }

        // ── GET /api/LotesInventario/resumen-pd04 ─────────────────────────────
        /// <summary>
        /// Devuelve las filas de la tabla "RESUMEN PRODUCCIÓN" de los formularios PD-04
        /// (Fileteo, templateId 101 por defecto). Cada fila trae Código Producto,
        /// Producto (tipo de producto), Peso Neto y la fecha del formulario.
        /// Si ese Código Producto ya tiene una clasificación guardada en LotesInventario,
        /// se incluye (loteInventarioId + clasificacion) para poder editarla.
        /// Filtros opcionales: ?desde=2026-01-01&hasta=2026-12-31&templateId=101
        /// </summary>
        [HttpGet("resumen-pd04")]
        public async Task<ActionResult<object>> GetResumenPD04(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] int templateId = 101)
        {
            var q = _context.FilledForms.AsNoTracking().Where(f => f.TemplateID == templateId);
            if (desde.HasValue) q = q.Where(f => f.CreatedAt >= desde.Value);
            if (hasta.HasValue) q = q.Where(f => f.CreatedAt < hasta.Value.AddDays(1)); // fin inclusivo

            var forms = await q
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new { f.FormID, f.HeaderData, f.BodyData, f.CreatedAt })
                .ToListAsync();

            // Clasificaciones ya guardadas en inventario, indexadas por NumeroLote (== Código Producto)
            var inv = await _context.LotesInventario
                .Select(l => new { l.Id, l.NumeroLote, l.Clasificacion })
                .ToListAsync();
            var invByNum = new Dictionary<string, (int Id, string? Clasificacion)>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in inv)
                if (!string.IsNullOrWhiteSpace(x.NumeroLote))
                    invByNum[x.NumeroLote] = (x.Id, x.Clasificacion);

            var filas = new List<object>();

            foreach (var f in forms)
            {
                if (string.IsNullOrWhiteSpace(f.BodyData)) continue;

                // Lote de proceso: campo "Lote" del encabezado del PD-04
                string loteProceso = "";
                if (!string.IsNullOrWhiteSpace(f.HeaderData))
                {
                    try
                    {
                        using var hdoc = JsonDocument.Parse(f.HeaderData);
                        if (hdoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var hp in hdoc.RootElement.EnumerateObject())
                            {
                                if (Norm(hp.Name).Contains("LOTE"))
                                {
                                    loteProceso = (hp.Value.ValueKind == JsonValueKind.String
                                        ? hp.Value.GetString()
                                        : hp.Value.ToString())?.Trim() ?? "";
                                    if (!string.IsNullOrWhiteSpace(loteProceso)) break;
                                }
                            }
                        }
                    }
                    catch { /* header inválido → sin lote de proceso */ }
                }

                JsonDocument doc;
                try { doc = JsonDocument.Parse(f.BodyData); } catch { continue; }
                using (doc)
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        if (!el.TryGetProperty("type", out var t) || t.GetString() != "table") continue;
                        if (!el.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) continue;

                        // ¿Es la tabla RESUMEN PRODUCCIÓN? Debe tener columnas "Producto" y "Código Producto".
                        bool esResumen = false;
                        foreach (var row0 in data.EnumerateArray())
                        {
                            if (row0.ValueKind != JsonValueKind.Object) continue;
                            bool tieneProd = false, tieneCod = false;
                            foreach (var p in row0.EnumerateObject())
                            {
                                var norm = Norm(p.Name);
                                if (norm == "PRODUCTO") tieneProd = true;
                                if (norm.Contains("CODIGO") && norm.Contains("PRODUCTO")) tieneCod = true;
                            }
                            esResumen = tieneProd && tieneCod;
                            break; // basta con inspeccionar la primera fila
                        }
                        if (!esResumen) continue;

                        foreach (var row in data.EnumerateArray())
                        {
                            if (row.ValueKind != JsonValueKind.Object) continue;

                            string? cod = null, prod = null, peso = null;
                            foreach (var p in row.EnumerateObject())
                            {
                                var norm = Norm(p.Name);
                                var val = p.Value.ValueKind == JsonValueKind.String
                                    ? p.Value.GetString()?.Trim()
                                    : p.Value.ToString()?.Trim();

                                if (norm.Contains("CODIGO") && norm.Contains("PRODUCTO")) cod = val;
                                else if (norm == "PRODUCTO") prod = val;
                                else if (norm.Contains("PESO") && norm.Contains("NETO")) peso = val;
                            }

                            // Saltar filas totalmente vacías
                            if (string.IsNullOrWhiteSpace(cod) && string.IsNullOrWhiteSpace(prod)) continue;

                            // Clave para buscar la clasificación guardada: código si existe, si no el producto
                            var claveInv = !string.IsNullOrWhiteSpace(cod) ? cod : (prod ?? "");
                            int? loteId = null;
                            string? clasif = null;
                            if (!string.IsNullOrWhiteSpace(claveInv) && invByNum.TryGetValue(claveInv, out var found))
                            {
                                loteId = found.Id;
                                clasif = found.Clasificacion;
                            }

                            filas.Add(new
                            {
                                formId = f.FormID,
                                fecha = f.CreatedAt.Date,
                                loteProceso = loteProceso,
                                codigoProducto = cod ?? "",
                                producto = prod ?? "",
                                pesoNeto = peso ?? "",
                                loteInventarioId = loteId,
                                clasificacion = clasif ?? ""
                            });
                        }
                    }
                }
            }

            return Ok(new { total = filas.Count, filas });
        }

        // ── GET /api/LotesInventario/resumen-produccion ───────────────────────
        /// <summary>
        /// Resumen de producción de CUALQUIER formulario, no solo del PD-04.
        /// Detecta la tabla de resumen por sus columnas (una de PRODUCTO más una de
        /// LOTE o de CÓDIGO PRODUCTO) en vez de exigir un formato fijo, así sirve
        /// igual para el PD-04 (templateId 101, resumen de fileteo) que para el
        /// PD-05 (templateId 3, liberación de túneles).
        /// Filtros: ?templateId=3&amp;desde=2026-01-01&amp;hasta=2026-12-31
        /// </summary>
        [HttpGet("resumen-produccion")]
        public async Task<ActionResult<object>> GetResumenProduccion(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] int templateId = 101)
        {
            var q = _context.FilledForms.AsNoTracking().Where(f => f.TemplateID == templateId);
            if (desde.HasValue) q = q.Where(f => f.CreatedAt >= desde.Value);
            if (hasta.HasValue) q = q.Where(f => f.CreatedAt < hasta.Value.AddDays(1)); // fin inclusivo

            var forms = await q
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new { f.FormID, f.HeaderData, f.BodyData, f.CreatedAt })
                .ToListAsync();

            // Clasificaciones ya guardadas en inventario, indexadas por NumeroLote
            var inv = await _context.LotesInventario
                .Select(l => new { l.Id, l.NumeroLote, l.Clasificacion })
                .ToListAsync();
            var invByNum = new Dictionary<string, (int Id, string? Clasificacion)>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in inv)
                if (!string.IsNullOrWhiteSpace(x.NumeroLote))
                    invByNum[x.NumeroLote] = (x.Id, x.Clasificacion);

            var filas = new List<object>();

            foreach (var f in forms)
            {
                if (string.IsNullOrWhiteSpace(f.BodyData)) continue;

                // Lote de proceso del encabezado (se usa si la tabla no trae columna de lote)
                string loteHeader = LoteDelHeader(f.HeaderData);

                JsonDocument doc;
                try { doc = JsonDocument.Parse(f.BodyData); } catch { continue; }
                using (doc)
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        if (!el.TryGetProperty("type", out var t) || t.GetString() != "table") continue;
                        if (!el.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) continue;

                        // ¿Es la tabla de resumen? Necesita una columna de PRODUCTO y
                        // además una de LOTE o de CÓDIGO PRODUCTO.
                        bool tieneProd = false, tieneCod = false, tieneLote = false;
                        foreach (var row0 in data.EnumerateArray())
                        {
                            if (row0.ValueKind != JsonValueKind.Object) continue;
                            foreach (var p in row0.EnumerateObject())
                            {
                                var n = Norm(p.Name);
                                if (EsCodigoProducto(n)) tieneCod = true;
                                else if (EsProducto(n))  tieneProd = true;
                                else if (EsLote(n))      tieneLote = true;
                            }
                            break; // basta con inspeccionar la primera fila
                        }
                        if (!tieneProd || (!tieneCod && !tieneLote)) continue;

                        foreach (var row in data.EnumerateArray())
                        {
                            if (row.ValueKind != JsonValueKind.Object) continue;

                            string cod = "", prod = "", clasif = "", peso = "", lote = "";
                            foreach (var p in row.EnumerateObject())
                            {
                                var n = Norm(p.Name);
                                var val = (p.Value.ValueKind == JsonValueKind.String
                                    ? p.Value.GetString()
                                    : p.Value.ToString())?.Trim() ?? "";

                                if (EsCodigoProducto(n))          { if (cod == "")    cod = val; }
                                else if (EsProducto(n))           { if (prod == "")   prod = val; }
                                else if (EsLote(n))               { if (lote == "")   lote = val; }
                                else if (n.Contains("CLASIF"))    { if (clasif == "") clasif = val; }
                                else if (EsPesoNeto(n))           { if (peso == "")   peso = val; }
                            }

                            if (string.IsNullOrWhiteSpace(cod) && string.IsNullOrWhiteSpace(prod)) continue;

                            // Si la fila no trae clasificación, se usa la guardada en inventario
                            var claveInv = !string.IsNullOrWhiteSpace(cod) ? cod : prod;
                            int? loteId = null;
                            if (!string.IsNullOrWhiteSpace(claveInv) && invByNum.TryGetValue(claveInv, out var found))
                            {
                                loteId = found.Id;
                                if (string.IsNullOrWhiteSpace(clasif)) clasif = found.Clasificacion ?? "";
                            }

                            filas.Add(new
                            {
                                formId = f.FormID,
                                fecha = f.CreatedAt.Date,
                                loteProceso = string.IsNullOrWhiteSpace(lote) ? loteHeader : lote,
                                codigoProducto = cod,
                                producto = prod,
                                pesoNeto = peso,
                                loteInventarioId = loteId,
                                clasificacion = clasif
                            });
                        }
                    }
                }
            }

            return Ok(new { total = filas.Count, filas });
        }

        // ── GET /api/LotesInventario/formularios-produccion ───────────────────
        /// <summary>
        /// Plantillas que sirven como origen de un desplegable de producción: las
        /// que tienen una tabla de resumen (columna de PRODUCTO más una de LOTE o
        /// de CÓDIGO PRODUCTO). Evita listar los formularios a mano en el frontend:
        /// un PD nuevo con esa estructura aparece solo en el selector.
        /// </summary>
        [HttpGet("formularios-produccion")]
        public async Task<ActionResult<object>> GetFormulariosProduccion()
        {
            var plantillas = await _context.Templates.AsNoTracking()
                .Where(t => !t.IsObsolete && !t.IsDraft)
                .Select(t => new { t.TemplateID, t.Codigo, t.Nombre, t.BodyElements })
                .ToListAsync();

            var encontrados = new List<(int TemplateId, string Codigo, string Nombre, string Tabla)>();

            foreach (var t in plantillas)
            {
                if (string.IsNullOrWhiteSpace(t.BodyElements)) continue;

                JsonDocument doc;
                try { doc = JsonDocument.Parse(t.BodyElements); } catch { continue; }
                using (doc)
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Object) continue;
                        if (!el.TryGetProperty("type", out var tp) || tp.GetString() != "table") continue;
                        if (!el.TryGetProperty("columns", out var cols) || cols.ValueKind != JsonValueKind.Array) continue;

                        bool tieneProd = false, tieneCod = false, tieneLote = false;
                        foreach (var c in cols.EnumerateArray())
                        {
                            if (c.ValueKind != JsonValueKind.Object) continue;
                            if (!c.TryGetProperty("label", out var lb)) continue;
                            var n = Norm(lb.GetString());
                            if (EsCodigoProducto(n)) tieneCod = true;
                            else if (EsProducto(n))  tieneProd = true;
                            else if (EsLote(n))      tieneLote = true;
                        }
                        if (!tieneProd || (!tieneCod && !tieneLote)) continue;

                        string tabla = el.TryGetProperty("title", out var ti) ? (ti.GetString() ?? "") : "";
                        encontrados.Add((t.TemplateID, t.Codigo ?? "", t.Nombre ?? "", tabla));
                        break; // una entrada por plantilla: la primera tabla de resumen
                    }
                }
            }

            // Los formularios de producción (FOR-PD-…) primero, luego el resto.
            var formularios = encontrados
                .OrderByDescending(x => x.Codigo.StartsWith("FOR-PD", StringComparison.OrdinalIgnoreCase))
                .ThenBy(x => x.Codigo, StringComparer.OrdinalIgnoreCase)
                .Select(x => new
                {
                    templateId = x.TemplateId,
                    codigo = x.Codigo,
                    nombre = x.Nombre,
                    tabla = x.Tabla
                })
                .ToList();

            return Ok(new { total = formularios.Count, formularios });
        }

        // ── Reconocimiento de columnas del resumen de producción ──────────────
        // "SUBPRODUCTO" no cuenta como producto (de ahí el chequeo de límite de
        // palabra) y "CAPACIDAD CAJAS-TINAS / LBS" no cuenta como peso neto.
        private static bool PalabraProducto(string n)
        {
            var i = n.IndexOf("PRODUCTO", StringComparison.Ordinal);
            while (i >= 0)
            {
                bool inicioOk = i == 0 || !char.IsLetterOrDigit(n[i - 1]);
                int fin = i + "PRODUCTO".Length;
                bool finOk = fin >= n.Length || !char.IsLetterOrDigit(n[fin]);
                if (inicioOk && finOk) return true;
                i = n.IndexOf("PRODUCTO", i + 1, StringComparison.Ordinal);
            }
            return false;
        }
        private static bool EsCodigoProducto(string n) => n.Contains("CODIGO") && PalabraProducto(n);

        // "PRODUCTO" también aparece en columnas que no nombran el producto:
        // temperaturas ("TEMP. (1) DEL PRODUCTO"), conteos ("CANTIDAD DE CARROS
        // CON PRODUCTO") o preguntas largas de los formularios de control. Se
        // descartan por palabra clave y por longitud: el nombre real es corto.
        private static readonly string[] RuidoProducto =
            { "TEMP", "CANTIDAD", "FECHA", "HORA", "TIEMPO", "DETECT", "OBSERV", "?" };
        private static bool EsProducto(string n) =>
            PalabraProducto(n)
            && !n.Contains("CODIGO")
            && n.Length <= 40
            && !RuidoProducto.Any(r => n.Contains(r));
        private static bool EsLote(string n) => n.Contains("LOTE") && !n.Contains("PADRE");
        private static bool EsPesoNeto(string n) =>
            (n.Contains("PESO") && n.Contains("NETO")) || n.Contains("NETA") || n.Contains("NETAS");

        /// <summary>Primer campo del encabezado cuyo nombre contiene "LOTE".</summary>
        private static string LoteDelHeader(string? headerData)
        {
            if (string.IsNullOrWhiteSpace(headerData)) return "";
            try
            {
                using var hdoc = JsonDocument.Parse(headerData);
                if (hdoc.RootElement.ValueKind != JsonValueKind.Object) return "";
                foreach (var hp in hdoc.RootElement.EnumerateObject())
                {
                    if (!Norm(hp.Name).Contains("LOTE")) continue;
                    var v = (hp.Value.ValueKind == JsonValueKind.String
                        ? hp.Value.GetString()
                        : hp.Value.ToString())?.Trim() ?? "";
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { /* header inválido → sin lote de proceso */ }
            return "";
        }

        // ── POST /api/LotesInventario/clasificar-producto ─────────────────────
        /// <summary>
        /// Guarda la clasificación de un Código Producto del resumen PD-04.
        /// Upsert por NumeroLote (== Código Producto): si ya existe en LotesInventario lo
        /// actualiza, si no lo crea. Devuelve el lote resultante.
        /// </summary>
        [HttpPost("clasificar-producto")]
        public async Task<ActionResult<LoteInventario>> ClasificarProducto([FromBody] ClasificarProductoDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.CodigoProducto))
                return BadRequest(new { message = "Falta el código de producto." });

            var codigo = dto.CodigoProducto.Trim();
            var ahora = DateTime.UtcNow;

            var lote = await _context.LotesInventario
                .FirstOrDefaultAsync(l => l.NumeroLote == codigo);

            if (lote == null)
            {
                lote = new LoteInventario
                {
                    NumeroLote   = codigo,
                    Proceso      = "PD-04 Resumen Producción",
                    Producto     = dto.Producto?.Trim(),
                    Clasificacion = dto.Clasificacion?.Trim(),
                    PesoEntrada  = 0,
                    Desperdicio  = 0,
                    PesoNeto     = 0,
                    Saldo        = 0,
                    Estado       = "disponible",
                    TemplateId   = "101",
                    Fecha        = dto.Fecha ?? ahora.Date,
                    Notas        = "Clasificación ingresada desde Resumen PD-04",
                    CreadoEn     = ahora,
                    ActualizadoEn = ahora
                };
                _context.LotesInventario.Add(lote);
            }
            else
            {
                lote.Clasificacion = dto.Clasificacion?.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Producto) && string.IsNullOrWhiteSpace(lote.Producto))
                    lote.Producto = dto.Producto.Trim();
                lote.ActualizadoEn = ahora;
            }

            await _context.SaveChangesAsync();
            return Ok(lote);
        }

        // ── Normaliza un nombre de columna: sin acentos, MAYÚSCULAS, sin espacios extremos ──
        private static string Norm(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            return sb.ToString().ToUpperInvariant().Trim();
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
        // Se mantiene la MISMA lógica de recorrido que antes (buscar hijos por
        // LotePadre == numeroLote, recursivo hasta el último nivel); lo único que
        // cambia es el tipo de retorno (antes un object anónimo) para poder leer
        // el resultado de los hijos y acumular el peso neto de las hojas.
        private static ArbolLoteDto? BuildArbol(string numeroLote, List<LoteInventario> todos)
        {
            var raiz = todos.FirstOrDefault(l => l.NumeroLote == numeroLote);
            if (raiz == null) return null;

            var hijos = todos
                .Where(l => l.LotePadre == numeroLote)
                .Select(h => BuildArbol(h.NumeroLote, todos))
                .Where(h => h != null)
                .Select(h => h!)
                .ToList();

            // Peso neto acumulado de HOJAS: si el nodo no tiene hijos, es su propio
            // peso neto; si tiene hijos, es la suma de lo acumulado de cada hijo (así
            // no se cuenta dos veces el peso de un lote intermedio que ya se repartió
            // en sus descendientes).
            var pesoNetoAcumuladoHojas = hijos.Count == 0
                ? raiz.PesoNeto
                : hijos.Sum(h => h.PesoNetoAcumuladoHojas);

            return new ArbolLoteDto
            {
                Id = raiz.Id,
                Lote = raiz.NumeroLote,
                Proceso = raiz.Proceso,
                Producto = raiz.Producto,
                Clasificacion = raiz.Clasificacion,
                PesoEntrada = raiz.PesoEntrada,
                Desperdicio = raiz.Desperdicio,
                PesoNeto = raiz.PesoNeto,
                Estado = raiz.Estado,
                LotePadre = raiz.LotePadre,
                Fecha = raiz.Fecha,
                FormId = raiz.FormId,
                TemplateId = raiz.TemplateId,
                Hijos = hijos,
                PesoNetoAcumuladoHojas = pesoNetoAcumuladoHojas
            };
        }

        /// <summary>
        /// Recorre el árbol ya construido y asigna, en cada nodo, el % que su peso
        /// neto acumulado de hojas representa sobre el peso de entrada de la raíz.
        /// </summary>
        private static void AplicarRendimientoAcumulado(ArbolLoteDto nodo, decimal pesoReferenciaRaiz)
        {
            nodo.RendimientoAcumuladoPct = pesoReferenciaRaiz > 0
                ? Math.Round(nodo.PesoNetoAcumuladoHojas / pesoReferenciaRaiz * 100m, 2)
                : (decimal?)null;

            foreach (var hijo in nodo.Hijos)
                AplicarRendimientoAcumulado(hijo, pesoReferenciaRaiz);
        }
    }

    // ── DTO del árbol de trazabilidad (GET /api/LotesInventario/arbol/{lote}) ──
    public class ArbolLoteDto
    {
        public int Id { get; set; }
        public string Lote { get; set; } = string.Empty;
        public string Proceso { get; set; } = string.Empty;
        public string? Producto { get; set; }
        public string? Clasificacion { get; set; }
        public decimal PesoEntrada { get; set; }
        public decimal Desperdicio { get; set; }
        public decimal PesoNeto { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? LotePadre { get; set; }
        public DateTime? Fecha { get; set; }
        public int? FormId { get; set; }
        public string? TemplateId { get; set; }
        public List<ArbolLoteDto> Hijos { get; set; } = new();

        /// <summary>Suma del peso neto de todos los descendientes finales (hojas) de este nodo.</summary>
        public decimal PesoNetoAcumuladoHojas { get; set; }

        /// <summary>% que PesoNetoAcumuladoHojas representa sobre el peso de entrada de la raíz del árbol.</summary>
        public decimal? RendimientoAcumuladoPct { get; set; }
    }

    // ── DTO para guardar la clasificación desde el resumen PD-04 ──────────────
    public class ClasificarProductoDto
    {
        public string CodigoProducto { get; set; } = "";
        public string? Producto { get; set; }
        public string? Clasificacion { get; set; }
        public DateTime? Fecha { get; set; }
    }
}
