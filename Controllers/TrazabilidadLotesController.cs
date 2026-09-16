using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Trazabilidad por aristas: un lote hijo puede venir de N lotes padre, y
    /// cada relación lleva su cantidad real, así que el porcentaje de aporte se
    /// calcula en vez de escribirse.
    ///
    /// Reemplaza al campo LotePadre (un solo string) sin borrarlo: LotePadre se
    /// sigue llenando con el padre que más aportó, para que lo viejo siga
    /// dibujándose igual.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class TrazabilidadLotesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TrazabilidadLotesController> _logger;

        /// <summary>Marca en Notas para reconocer los movimientos de un cierre y poder deshacerlos.</summary>
        private const string TagCierre = "#cierre";

        /// <summary>Tolerancia de redondeo al comparar entrada contra salida.</summary>
        private const decimal Epsilon = 0.01m;

        /// <summary>
        /// Tipo de movimiento que SOLO deja constancia de la relación padre→hijo.
        /// No mueve saldo: el descuento ya lo hizo la tabla de materia prima.
        /// </summary>
        private const string TipoArista = "arista";
        private const string TagArista = "#arista";

        public TrazabilidadLotesController(
            ApplicationDbContext context,
            ILogger<TrazabilidadLotesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CIERRE: escribe las dos puntas de una vez
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/TrazabilidadLotes/cierre
        ///
        /// Toma lo que declara un formulario —qué lotes entraron con cuántas
        /// libras, qué lote(s) salieron, cuánta merma— y lo aplica completo:
        /// resta el saldo de cada padre, crea el hijo con su peso medido, y
        /// deja una arista por cada par padre→hijo con su cantidad.
        ///
        /// Todo dentro de una transacción: si algo falla no queda medio
        /// aplicado, que es como se descuadra un inventario.
        ///
        /// Es idempotente: volver a guardar el mismo formulario deshace el
        /// cierre anterior y lo rehace, en vez de descontar dos veces.
        /// </summary>
        [HttpPost("cierre")]
        public async Task<ActionResult<object>> Cerrar([FromBody] CierreDto dto)
        {
            var validacion = Validar(dto);
            if (validacion != null) return BadRequest(validacion);

            var totalEntrada = dto.Padres.Sum(p => p.Cantidad);
            var totalSalida = dto.Hijos.Sum(h => h.PesoSalida);
            var diferencia = totalEntrada - totalSalida - dto.Merma;

            // El balance se informa siempre, pero solo bloquea si se produjo
            // MÁS de lo que entró: eso es un error de digitación seguro.
            // Una diferencia positiva es merma no declarada, que es normal.
            if (diferencia < -Epsilon && !dto.PermitirExcedente)
            {
                return BadRequest(new
                {
                    message = $"La salida ({totalSalida + dto.Merma:0.##} Lbs) supera a la entrada " +
                              $"({totalEntrada:0.##} Lbs) por {Math.Abs(diferencia):0.##} Lbs. " +
                              "Revisá las cantidades o marcá PermitirExcedente si el proceso agrega peso (glaseo).",
                    totalEntrada,
                    totalSalida,
                    dto.Merma,
                    diferencia
                });
            }

            await using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // ── 1. Deshacer un cierre anterior del mismo formulario ──
                var deshechos = dto.FormId.HasValue
                    ? await DeshacerCierre(dto.FormId.Value)
                    : 0;

                // ── 2. Verificar saldo de todos los padres ANTES de tocar nada ──
                var numerosPadre = dto.Padres.Select(p => p.NumeroLote).ToList();
                var lotesPadre = await _context.LotesInventario
                    .Where(l => numerosPadre.Contains(l.NumeroLote))
                    .ToListAsync();

                var faltantes = numerosPadre
                    .Where(n => !lotesPadre.Any(l => l.NumeroLote == n))
                    .ToList();
                if (faltantes.Count > 0)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new
                    {
                        message = "Hay lotes de materia prima que no existen en el inventario.",
                        lotesFaltantes = faltantes
                    });
                }

                var sinSaldo = new List<object>();
                foreach (var p in dto.Padres)
                {
                    var lote = lotesPadre.First(l => l.NumeroLote == p.NumeroLote);
                    if (p.Cantidad > lote.Saldo + Epsilon)
                        sinSaldo.Add(new { p.NumeroLote, pide = p.Cantidad, saldo = lote.Saldo });
                }
                if (sinSaldo.Count > 0 && !dto.PermitirSobregiro)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new
                    {
                        message = "No hay saldo suficiente en uno o más lotes de materia prima.",
                        lotes = sinSaldo
                    });
                }

                var ahora = DateTime.UtcNow;
                var proceso = string.IsNullOrWhiteSpace(dto.Proceso) ? "Sin proceso" : dto.Proceso!.Trim();

                // ── 3. Crear o actualizar los lotes hijo ──
                // Se hace antes de las salidas para que el movimiento de salida
                // pueda apuntar a un lote que ya existe.
                var padrePrincipal = dto.Padres.OrderByDescending(p => p.Cantidad).First().NumeroLote;
                var hijosCreados = new List<LoteInventario>();

                foreach (var h in dto.Hijos)
                {
                    var hijo = await _context.LotesInventario
                        .FirstOrDefaultAsync(l => l.NumeroLote == h.NumeroLote);

                    if (hijo == null)
                    {
                        hijo = new LoteInventario
                        {
                            NumeroLote = h.NumeroLote.Trim(),
                            Proceso = proceso,
                            Producto = h.Producto?.Trim(),
                            Clasificacion = h.Clasificacion?.Trim(),
                            // El peso de salida es MEDIDO, no PesoEntrada − Desperdicio:
                            // en el PD-06 la fórmula daba 2786 y la balanza 2691.
                            PesoEntrada = h.PesoSalida,
                            Desperdicio = 0,
                            PesoNeto = h.PesoSalida,
                            Saldo = h.PesoSalida,
                            Estado = "disponible",
                            LotePadre = padrePrincipal,
                            FormId = dto.FormId,
                            TemplateId = dto.TemplateId,
                            Fecha = dto.Fecha ?? ahora.Date,
                            Notas = $"Cierre de trazabilidad · {dto.Padres.Count} lote(s) de origen",
                            CreadoEn = ahora,
                            ActualizadoEn = ahora
                        };
                        _context.LotesInventario.Add(hijo);
                    }
                    else
                    {
                        // Re-guardado: el lote ya existía de un cierre anterior.
                        // El saldo se reemplaza, no se suma, porque las salidas
                        // previas ya se revirtieron en el paso 1.
                        hijo.PesoEntrada = h.PesoSalida;
                        hijo.PesoNeto = h.PesoSalida;
                        hijo.Saldo = h.PesoSalida;
                        hijo.Estado = h.PesoSalida > 0 ? "disponible" : "consumido";
                        hijo.LotePadre = padrePrincipal;
                        hijo.ActualizadoEn = ahora;
                        if (!string.IsNullOrWhiteSpace(h.Producto)) hijo.Producto = h.Producto!.Trim();
                        if (!string.IsNullOrWhiteSpace(h.Clasificacion)) hijo.Clasificacion = h.Clasificacion!.Trim();
                    }

                    hijosCreados.Add(hijo);

                    _context.MovimientosInventario.Add(new MovimientoInventario
                    {
                        LoteInventarioId = hijo.Id,
                        NumeroLote = hijo.NumeroLote,
                        Tipo = "entrada",
                        Cantidad = h.PesoSalida,
                        SaldoResultante = h.PesoSalida,
                        Proceso = proceso,
                        FormId = dto.FormId,
                        Notas = $"{TagCierre} Generado desde {dto.Padres.Count} lote(s) de materia prima",
                        CreadoEn = ahora
                    });
                }

                await _context.SaveChangesAsync();   // asigna los Id de los hijos nuevos

                // ── 4. Restar de cada padre y escribir las aristas ──
                // Si hay varios hijos, la cantidad de cada padre se reparte
                // entre ellos en proporción al peso de cada hijo: es la mezcla
                // homogénea del tanque, no una atribución inventada.
                var aristas = new List<object>();
                var totalHijos = dto.Hijos.Sum(h => h.PesoSalida);

                foreach (var p in dto.Padres)
                {
                    var lote = lotesPadre.First(l => l.NumeroLote == p.NumeroLote);

                    lote.Saldo = Math.Max(0, lote.Saldo - p.Cantidad);
                    lote.Estado = lote.Saldo <= Epsilon ? "consumido"
                                : lote.Saldo < lote.PesoNeto ? "parcial"
                                : "disponible";
                    lote.ActualizadoEn = ahora;

                    // Un movimiento por cada destino: así el hijo puede tener
                    // N padres y el padre N hijos, sin límite.
                    var repartido = 0m;
                    for (var i = 0; i < hijosCreados.Count; i++)
                    {
                        var hijo = hijosCreados[i];
                        var pesoHijo = dto.Hijos[i].PesoSalida;

                        // El último se lleva el resto, para que la suma cierre
                        // exacta y no se pierdan centésimas por redondeo.
                        var cantidad = (i == hijosCreados.Count - 1)
                            ? p.Cantidad - repartido
                            : (totalHijos > 0
                                ? Math.Round(p.Cantidad * (pesoHijo / totalHijos), 4)
                                : Math.Round(p.Cantidad / hijosCreados.Count, 4));
                        repartido += cantidad;

                        if (cantidad <= 0) continue;

                        _context.MovimientosInventario.Add(new MovimientoInventario
                        {
                            LoteInventarioId = lote.Id,
                            NumeroLote = lote.NumeroLote,
                            Tipo = "salida",
                            Cantidad = cantidad,
                            SaldoResultante = lote.Saldo,
                            LoteDestino = hijo.NumeroLote,
                            Proceso = proceso,
                            FormId = dto.FormId,
                            Notas = $"{TagCierre} Hacia {hijo.NumeroLote}",
                            CreadoEn = ahora
                        });

                        aristas.Add(new
                        {
                            padre = lote.NumeroLote,
                            hijo = hijo.NumeroLote,
                            cantidad,
                            porcentajeDelHijo = totalEntrada > 0
                                ? Math.Round(p.Cantidad / totalEntrada * 100m, 2)
                                : 0m
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation(
                    "Cierre aplicado (form {FormId}): {P} padre(s) → {H} hijo(s), {A} arista(s)",
                    dto.FormId, dto.Padres.Count, dto.Hijos.Count, aristas.Count);

                return Ok(new
                {
                    message = "Cierre de trazabilidad aplicado.",
                    reaplicado = deshechos > 0,
                    movimientosRevertidos = deshechos,
                    balance = new
                    {
                        totalEntrada,
                        totalSalida,
                        dto.Merma,
                        diferencia,
                        rendimientoPct = totalEntrada > 0
                            ? Math.Round(totalSalida / totalEntrada * 100m, 2)
                            : 0m,
                        // Se informa en vez de esconderse: en el PD-06 real
                        // eran 95 Lbs que no estaban en ninguna columna.
                        advertencia = diferencia > Epsilon
                            ? $"Quedan {diferencia:0.##} Lbs sin explicar ({diferencia / totalEntrada * 100:0.##}% de la entrada)."
                            : null
                    },
                    aristas
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error en el cierre de trazabilidad del formulario {FormId}", dto.FormId);
                return StatusCode(500, new { message = "Error al aplicar el cierre. No se guardó nada." });
            }
        }

        private static object? Validar(CierreDto? dto)
        {
            if (dto == null) return new { message = "Cuerpo vacío." };
            if (dto.Padres == null || dto.Padres.Count == 0)
                return new { message = "No se indicó ningún lote de materia prima." };
            if (dto.Hijos == null || dto.Hijos.Count == 0)
                return new { message = "No se indicó ningún lote generado." };

            if (dto.Padres.Any(p => string.IsNullOrWhiteSpace(p.NumeroLote)))
                return new { message = "Hay un lote de materia prima sin número." };
            if (dto.Hijos.Any(h => string.IsNullOrWhiteSpace(h.NumeroLote)))
                return new { message = "Hay un lote generado sin número." };
            if (dto.Padres.Any(p => p.Cantidad <= 0))
                return new { message = "Todas las cantidades de materia prima deben ser mayores a 0." };
            if (dto.Hijos.Any(h => h.PesoSalida < 0))
                return new { message = "El peso de salida no puede ser negativo." };

            // Un lote que se consume a sí mismo genera un ciclo en el árbol.
            var hijos = dto.Hijos.Select(h => h.NumeroLote.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var choque = dto.Padres.FirstOrDefault(p => hijos.Contains(p.NumeroLote.Trim()));
            if (choque != null)
                return new { message = $"El lote «{choque.NumeroLote}» aparece como origen y como resultado. Eso crearía un ciclo." };

            var dupPadres = dto.Padres.GroupBy(p => p.NumeroLote.Trim(), StringComparer.OrdinalIgnoreCase)
                                      .FirstOrDefault(g => g.Count() > 1);
            if (dupPadres != null)
                return new { message = $"El lote «{dupPadres.Key}» está repetido en la materia prima. Sumá las cantidades en una sola línea." };

            return null;
        }

        /// <summary>
        /// Revierte el cierre anterior de un formulario: devuelve el saldo a
        /// los padres y borra los movimientos. Sin esto, re-guardar un
        /// formulario descontaría dos veces.
        /// </summary>
        private async Task<int> DeshacerCierre(int formId)
        {
            var previos = await _context.MovimientosInventario
                .Where(m => m.FormId == formId && m.Notas != null && m.Notas.Contains(TagCierre))
                .ToListAsync();

            if (previos.Count == 0) return 0;

            var numeros = previos.Select(m => m.NumeroLote).Distinct().ToList();
            var lotes = await _context.LotesInventario
                .Where(l => numeros.Contains(l.NumeroLote))
                .ToListAsync();

            foreach (var m in previos)
            {
                var lote = lotes.FirstOrDefault(l => l.NumeroLote == m.NumeroLote);
                if (lote == null) continue;

                // Salida revertida devuelve saldo; entrada revertida lo quita.
                if (m.Tipo == "salida") lote.Saldo += m.Cantidad;
                else if (m.Tipo == "entrada") lote.Saldo = Math.Max(0, lote.Saldo - m.Cantidad);

                lote.Estado = lote.Saldo <= Epsilon ? "consumido"
                            : lote.Saldo < lote.PesoNeto ? "parcial"
                            : "disponible";
                lote.ActualizadoEn = DateTime.UtcNow;
            }

            _context.MovimientosInventario.RemoveRange(previos);
            await _context.SaveChangesAsync();
            return previos.Count;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ARISTAS: relación padre → hijo SIN mover saldo
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// POST /api/TrazabilidadLotes/aristas
        ///
        /// Registra de qué lote viene cada lote generado, con su cantidad.
        /// NO toca el saldo, y esa es la diferencia con /cierre.
        ///
        /// Es para el caso del PD-06: la tabla MATERIA PRIMA ya descontó el
        /// saldo al guardar el formulario. Si acá se volviera a descontar, el
        /// lote perdería las libras dos veces. Lo único que falta es dejar
        /// escrito el vínculo, que es lo que permite calcular los porcentajes.
        ///
        /// Idempotente por formulario: vuelve a escribirlas desde cero.
        /// </summary>
        [HttpPost("aristas")]
        public async Task<ActionResult<object>> RegistrarAristas([FromBody] AristasDto dto)
        {
            if (dto?.Aristas == null || dto.Aristas.Count == 0)
                return BadRequest(new { message = "No se indicó ninguna relación." });

            var invalidas = dto.Aristas
                .Where(a => string.IsNullOrWhiteSpace(a.LotePadre)
                         || string.IsNullOrWhiteSpace(a.LoteHijo)
                         || a.Cantidad <= 0)
                .ToList();
            if (invalidas.Count > 0)
                return BadRequest(new { message = "Hay relaciones sin lote padre, sin lote hijo o con cantidad en cero." });

            // Un lote que se apunta a sí mismo crea un ciclo en el grafo.
            var ciclo = dto.Aristas.FirstOrDefault(a =>
                string.Equals(a.LotePadre.Trim(), a.LoteHijo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ciclo != null)
                return BadRequest(new { message = $"El lote «{ciclo.LotePadre}» aparece como padre de sí mismo." });

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahora = DateTime.UtcNow;

                // Re-guardado del formulario: se borran las aristas anteriores
                // en vez de duplicarlas. No afecta saldos porque nunca los tocó.
                var borradas = 0;
                if (dto.FormId.HasValue)
                {
                    var previas = await _context.MovimientosInventario
                        .Where(m => m.FormId == dto.FormId && m.Tipo == TipoArista)
                        .ToListAsync();
                    borradas = previas.Count;
                    if (borradas > 0) _context.MovimientosInventario.RemoveRange(previas);
                }

                var numeros = dto.Aristas.Select(a => a.LotePadre.Trim()).Distinct().ToList();
                var padres = await _context.LotesInventario
                    .Where(l => numeros.Contains(l.NumeroLote))
                    .ToListAsync();

                var escritas = new List<object>();
                var omitidas = new List<string>();

                foreach (var a in dto.Aristas)
                {
                    var padre = padres.FirstOrDefault(l => l.NumeroLote == a.LotePadre.Trim());
                    if (padre == null)
                    {
                        // No se inventa el lote: se avisa y se sigue con las demás.
                        omitidas.Add($"«{a.LotePadre}» no existe en el inventario.");
                        continue;
                    }

                    _context.MovimientosInventario.Add(new MovimientoInventario
                    {
                        LoteInventarioId = padre.Id,
                        NumeroLote = padre.NumeroLote,
                        Tipo = TipoArista,
                        Cantidad = a.Cantidad,
                        SaldoResultante = padre.Saldo,   // sin cambios: no se descontó
                        LoteDestino = a.LoteHijo.Trim(),
                        Proceso = dto.Proceso,
                        FormId = dto.FormId,
                        Notas = $"{TagArista} {a.LotePadre.Trim()} -> {a.LoteHijo.Trim()}",
                        CreadoEn = ahora
                    });

                    escritas.Add(new { padre = padre.NumeroLote, hijo = a.LoteHijo.Trim(), a.Cantidad });
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Ok(new
                {
                    message = $"{escritas.Count} relación(es) registrada(s).",
                    reemplazadas = borradas,
                    aristas = escritas,
                    omitidas
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error al registrar aristas del formulario {FormId}", dto.FormId);
                return StatusCode(500, new { message = "Error al registrar las relaciones. No se guardó nada." });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  COMPOSICIÓN: de dónde viene y a dónde fue, en porcentaje
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// GET /api/TrazabilidadLotes/composicion/{numeroLote}
        ///
        /// Responde las dos preguntas de una auditoría:
        ///   · ¿de qué lotes está hecho este, y en qué porcentaje?
        ///   · si uno de sus orígenes sale con problema, ¿cuánto de este lote
        ///     está comprometido?
        ///
        /// Los orígenes raíz se calculan multiplicando porcentajes nivel por
        /// nivel, así que sirve aunque el producto haya pasado por cuatro
        /// procesos.
        /// </summary>
        [HttpGet("composicion/{numeroLote}")]
        public async Task<ActionResult<object>> Composicion(string numeroLote, [FromQuery] int profundidad = 6)
        {
            var lote = await _context.LotesInventario
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.NumeroLote == numeroLote);

            if (lote == null)
                return NotFound(new { message = $"Lote «{numeroLote}» no encontrado." });

            var padres = await PadresDe(numeroLote);
            var hijos = await HijosDe(numeroLote);

            var origenes = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            await AcumularOrigenes(numeroLote, 1m, Math.Clamp(profundidad, 1, 12),
                                   new HashSet<string>(StringComparer.OrdinalIgnoreCase), origenes);

            return Ok(new
            {
                lote = new
                {
                    lote.NumeroLote, lote.Proceso, lote.Producto, lote.Clasificacion,
                    lote.PesoEntrada, lote.PesoNeto, lote.Saldo, lote.Estado, lote.Fecha, lote.LotePadre
                },
                padresDirectos = padres,
                hijosDirectos = hijos,
                // Materia prima original, ya multiplicada por todos los niveles.
                origenesRaiz = origenes
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new { lote = kv.Key, porcentaje = Math.Round(kv.Value * 100m, 2) })
                    .ToList(),
                // Si no hay aristas es que el formulario nunca aplicó el cierre.
                sinAristas = padres.Count == 0 && hijos.Count == 0
            });
        }

        /// <summary>
        /// GET /api/TrazabilidadLotes/mapa/{numeroLote}
        ///
        /// El árbol completo hacia arriba: de qué lotes viene, de qué lotes
        /// vienen esos, y así hasta la materia prima original. Cada nodo trae
        /// dos porcentajes:
        ///   · PorcentajeLocal — cuánto aporta a su padre inmediato
        ///   · PorcentajeTotal — cuánto aporta al lote raíz, ya multiplicado
        ///     por todos los niveles
        ///
        /// El segundo es el que sirve para un retiro: dice qué fracción del
        /// producto final está comprometida si ese origen sale con problema.
        /// </summary>
        [HttpGet("mapa/{numeroLote}")]
        public async Task<ActionResult<object>> Mapa(string numeroLote, [FromQuery] int profundidad = 6)
        {
            var lote = await _context.LotesInventario
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.NumeroLote == numeroLote);

            if (lote == null)
                return NotFound(new { message = $"Lote «{numeroLote}» no encontrado." });

            var visitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodo = await ConstruirMapa(numeroLote, 100m, Math.Clamp(profundidad, 1, 12), visitados);

            return Ok(new
            {
                lote = new
                {
                    lote.NumeroLote, lote.Proceso, lote.Producto, lote.Clasificacion,
                    lote.PesoNeto, lote.Saldo, lote.Estado, lote.Fecha
                },
                mapa = nodo,
                niveles = ContarNiveles(nodo)
            });
        }

        private async Task<MapaNodoDto> ConstruirMapa(
            string numeroLote, decimal porcentajeTotal, int restante, HashSet<string> visitados)
        {
            var info = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => l.NumeroLote == numeroLote)
                .Select(l => new { l.Producto, l.Clasificacion, l.Proceso, l.PesoNeto })
                .FirstOrDefaultAsync();

            var nodo = new MapaNodoDto
            {
                Lote = numeroLote,
                Producto = info?.Producto,
                Clasificacion = info?.Clasificacion,
                Proceso = info?.Proceso,
                PesoNeto = info?.PesoNeto ?? 0,
                PorcentajeTotal = Math.Round(porcentajeTotal, 2),
                EsRaiz = false
            };

            // Sin la guarda, un ciclo o un ancestro compartido por dos caminos
            // recorre para siempre: con varios padres esto es un grafo, no un árbol.
            if (restante <= 0 || !visitados.Add(numeroLote))
            {
                nodo.Truncado = true;
                return nodo;
            }

            var padres = await PadresDe(numeroLote);

            if (padres.Count == 0)
            {
                nodo.EsRaiz = true;   // materia prima original: acá termina el camino
            }
            else
            {
                foreach (var p in padres)
                {
                    var hijo = await ConstruirMapa(
                        p.Lote,
                        porcentajeTotal * (p.Porcentaje / 100m),
                        restante - 1,
                        visitados);

                    hijo.PorcentajeLocal = p.Porcentaje;
                    hijo.Cantidad = p.Cantidad;
                    nodo.Padres.Add(hijo);
                }
            }

            visitados.Remove(numeroLote);
            return nodo;
        }

        private static int ContarNiveles(MapaNodoDto nodo) =>
            nodo.Padres.Count == 0 ? 1 : 1 + nodo.Padres.Max(ContarNiveles);

        public class MapaNodoDto
        {
            public string Lote { get; set; } = "";
            public string? Producto { get; set; }
            public string? Clasificacion { get; set; }
            public string? Proceso { get; set; }
            public decimal PesoNeto { get; set; }
            public decimal Cantidad { get; set; }
            /// <summary>% que aporta a su padre inmediato.</summary>
            public decimal PorcentajeLocal { get; set; }
            /// <summary>% que aporta al lote del que se pidió el mapa.</summary>
            public decimal PorcentajeTotal { get; set; }
            /// <summary>Materia prima original: no tiene padres.</summary>
            public bool EsRaiz { get; set; }
            /// <summary>Se cortó por profundidad o por ciclo.</summary>
            public bool Truncado { get; set; }
            public List<MapaNodoDto> Padres { get; set; } = new();
        }

        private async Task<List<AristaDto>> PadresDe(string numeroLote)
        {
            var movs = await _context.MovimientosInventario
                .AsNoTracking()
                .Where(m => (m.Tipo == "salida" || m.Tipo == TipoArista) && m.LoteDestino == numeroLote)
                .ToListAsync();

            var total = movs.Sum(m => m.Cantidad);
            if (total <= 0) return new List<AristaDto>();

            // Peso real del lote de destino: sobre él se reparten las libras que
            // se ven en pantalla, para que sumen exactamente ese peso y no el de
            // la materia prima que entró (que incluye la merma).
            var pesoDestino = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => l.NumeroLote == numeroLote)
                .Select(l => l.PesoNeto)
                .FirstOrDefaultAsync();

            // Peso de cada lote padre, para saber qué fracción de él vino acá.
            var numeros = movs.Select(m => m.NumeroLote).Distinct().ToList();
            var pesos = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => numeros.Contains(l.NumeroLote))
                .Select(l => new { l.NumeroLote, l.PesoNeto, l.Producto, l.Clasificacion })
                .ToListAsync();

            return movs
                .GroupBy(m => m.NumeroLote)
                .Select(g =>
                {
                    var aporte = g.Sum(x => x.Cantidad);
                    var info = pesos.FirstOrDefault(p => p.NumeroLote == g.Key);
                    var pesoOrigen = info?.PesoNeto ?? 0m;
                    var fraccion = aporte / total;
                    return new AristaDto
                    {
                        Lote = g.Key,
                        Producto = info?.Producto,
                        Clasificacion = info?.Clasificacion,
                        Cantidad = aporte,
                        Porcentaje = Math.Round(fraccion * 100m, 2),
                        // Mismo porcentaje, aplicado al peso real del destino.
                        CantidadEnDestino = Math.Round(pesoDestino * fraccion, 2),
                        PesoOrigen = pesoOrigen,
                        PorcentajeDelOrigen = pesoOrigen > 0
                            ? Math.Round(aporte / pesoOrigen * 100m, 2)
                            : 0m,
                        FormId = g.First().FormId
                    };
                })
                .OrderByDescending(a => a.Cantidad)
                .ToList();
        }

        private async Task<List<AristaDto>> HijosDe(string numeroLote)
        {
            var movs = await _context.MovimientosInventario
                .AsNoTracking()
                .Where(m => (m.Tipo == "salida" || m.Tipo == TipoArista) && m.NumeroLote == numeroLote && m.LoteDestino != null)
                .ToListAsync();

            if (movs.Count == 0) return new List<AristaDto>();

            var destinos = movs.Select(m => m.LoteDestino!).Distinct().ToList();
            var datos = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => destinos.Contains(l.NumeroLote))
                .Select(l => new { l.NumeroLote, l.Producto, l.Clasificacion })
                .ToListAsync();

            // Peso real de cada hijo, para repartir sobre él y no sobre lo que entró.
            var pesosHijos = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => destinos.Contains(l.NumeroLote))
                .Select(l => new { l.NumeroLote, l.PesoNeto })
                .ToListAsync();

            // El % es sobre el hijo: cuánto de ESE lote vino de acá. Es el dato
            // que dice cuánto producto retirar si este lote sale con problema.
            var totalesPorHijo = await _context.MovimientosInventario
                .AsNoTracking()
                .Where(m => (m.Tipo == "salida" || m.Tipo == TipoArista) && m.LoteDestino != null && destinos.Contains(m.LoteDestino))
                .GroupBy(m => m.LoteDestino!)
                .Select(g => new { Hijo = g.Key, Total = g.Sum(x => x.Cantidad) })
                .ToListAsync();

            // Peso de ESTE lote: base para saber qué fracción se fue a cada hijo.
            var pesoPropio = await _context.LotesInventario
                .AsNoTracking()
                .Where(l => l.NumeroLote == numeroLote)
                .Select(l => l.PesoNeto)
                .FirstOrDefaultAsync();

            return movs
                .GroupBy(m => m.LoteDestino!)
                .Select(g =>
                {
                    var info = datos.FirstOrDefault(d => d.NumeroLote == g.Key);
                    var total = totalesPorHijo.FirstOrDefault(t => t.Hijo == g.Key)?.Total ?? 0m;
                    var aporte = g.Sum(x => x.Cantidad);
                    var pesoHijo = pesosHijos.FirstOrDefault(x => x.NumeroLote == g.Key)?.PesoNeto ?? 0m;
                    return new AristaDto
                    {
                        Lote = g.Key,
                        Producto = info?.Producto,
                        Clasificacion = info?.Clasificacion,
                        Cantidad = aporte,
                        Porcentaje = total > 0 ? Math.Round(aporte / total * 100m, 2) : 0m,
                        CantidadEnDestino = total > 0 ? Math.Round(pesoHijo * (aporte / total), 2) : 0m,
                        PesoOrigen = pesoPropio,
                        // Cuánto de este lote se usó para ese producto.
                        PorcentajeDelOrigen = pesoPropio > 0
                            ? Math.Round(aporte / pesoPropio * 100m, 2)
                            : 0m,
                        FormId = g.First().FormId
                    };
                })
                .OrderByDescending(a => a.Cantidad)
                .ToList();
        }

        /// <summary>
        /// Sube por el grafo multiplicando porcentajes. El HashSet de visitados
        /// es obligatorio: con varios padres esto es un grafo, no un árbol, y
        /// sin él un ciclo o un ancestro compartido no termina nunca.
        /// </summary>
        private async Task AcumularOrigenes(
            string lote, decimal fraccion, int restante,
            HashSet<string> visitados, Dictionary<string, decimal> acumulado)
        {
            if (restante <= 0 || fraccion <= 0.0001m) return;
            if (!visitados.Add(lote)) return;

            var padres = await PadresDe(lote);

            if (padres.Count == 0)
            {
                // Sin padres es materia prima original: acá termina el camino.
                acumulado[lote] = acumulado.GetValueOrDefault(lote) + fraccion;
                visitados.Remove(lote);
                return;
            }

            foreach (var p in padres)
                await AcumularOrigenes(p.Lote, fraccion * (p.Porcentaje / 100m), restante - 1, visitados, acumulado);

            visitados.Remove(lote);
        }

        public class AristaDto
        {
            public string Lote { get; set; } = "";
            public string? Producto { get; set; }
            public string? Clasificacion { get; set; }
            public decimal Cantidad { get; set; }

            /// <summary>
            /// Qué parte del lote HIJO vino de acá. Es la composición: si da
            /// 30.81%, ese producto es 30.81% de este origen.
            /// </summary>
            public decimal Porcentaje { get; set; }

            /// <summary>
            /// Qué parte de ESTE lote se fue hacia allá. Es el reparto: si el
            /// lote tenía 872 Lbs y 829.87 fueron a un producto, da 95.2%.
            /// Responde "cuánto de este lote se usó para ese producto".
            /// </summary>
            public decimal PorcentajeDelOrigen { get; set; }

            /// <summary>Peso total del lote de origen, para poder leer el % anterior.</summary>
            public decimal PesoOrigen { get; set; }

            /// <summary>
            /// Cuántas libras de este origen HAY en el lote de destino, ya
            /// descontada la merma del proceso.
            ///
            /// Cantidad dice cuánto ENTRÓ para producirlo; esto dice cuánto
            /// QUEDÓ adentro. Con 20% de merma, de 250 Lbs que entraron quedan
            /// 200 en el producto. Es lo que hay que mirar para responder "¿qué
            /// hay en estos 400 Lbs?", porque estas sí suman el peso del lote.
            /// </summary>
            public decimal CantidadEnDestino { get; set; }

            public int? FormId { get; set; }
        }
    }

    // ── DTOs de entrada ─────────────────────────────────────────────────────

    public class CierreDto
    {
        public int? FormId { get; set; }
        public string? TemplateId { get; set; }
        public string? Proceso { get; set; }
        public DateTime? Fecha { get; set; }

        /// <summary>Lotes de materia prima con las libras que se usaron de cada uno.</summary>
        public List<PadreDto> Padres { get; set; } = new();

        /// <summary>Lote(s) generados, con su peso MEDIDO de salida.</summary>
        public List<HijoDto> Hijos { get; set; } = new();

        /// <summary>Subproductos y desperdicio, solo para el balance.</summary>
        public decimal Merma { get; set; }

        /// <summary>Permite salida &gt; entrada (procesos que agregan peso, ej. glaseo).</summary>
        public bool PermitirExcedente { get; set; }

        /// <summary>Permite consumir más de lo que hay en el lote. Usar con cuidado.</summary>
        public bool PermitirSobregiro { get; set; }
    }

    public class PadreDto
    {
        public string NumeroLote { get; set; } = "";
        public decimal Cantidad { get; set; }
    }

    public class HijoDto
    {
        public string NumeroLote { get; set; } = "";
        public string? Producto { get; set; }
        public string? Clasificacion { get; set; }
        /// <summary>Peso medido en balanza, no calculado.</summary>
        public decimal PesoSalida { get; set; }
    }

    public class AristasDto
    {
        public int? FormId { get; set; }
        public string? Proceso { get; set; }
        public List<AristaEntradaDto> Aristas { get; set; } = new();
    }

    public class AristaEntradaDto
    {
        public string LotePadre { get; set; } = "";
        public string LoteHijo { get; set; } = "";
        public decimal Cantidad { get; set; }
    }
}