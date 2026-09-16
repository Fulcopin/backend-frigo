using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Detector de plantillas SIN configurar para el inventario de lotes.
    ///
    /// El panel de configuración (verde = crea saldo, naranja = resta saldo) ya
    /// existe en Crear/Editar Plantilla, pero solo se activó en 2 de 57
    /// plantillas. Este controlador revisa el BodyElements de cada una, detecta
    /// las tablas que TIENEN pinta de mover inventario (columna de lote +
    /// columna de cantidad) y avisa cuáles quedaron sin marcar.
    ///
    /// Es solo de lectura: no escribe nada. Sirve para saber qué configurar
    /// y con qué columnas, en vez de revisar las 57 a ciegas.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticoPlantillasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiagnosticoPlantillasController> _logger;

        public DiagnosticoPlantillasController(
            ApplicationDbContext context,
            ILogger<DiagnosticoPlantillasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Reconocimiento de columnas por su nombre ─────────────────────────
        // No se busca coincidencia exacta a propósito: los encabezados reales
        // traen tildes, barras, unidades y paréntesis ("CANTIDAD / LBS (Lbs)").

        /// <summary>
        /// El lote de ORIGEN, no el que la tabla crea. Se detecta aparte porque
        /// confundirlos hace que el lote nuevo nazca con el número del padre.
        /// </summary>
        private static readonly Regex RxLotePadre = new(
            @"c[oó]digo\s*padre|lote\s*padre|lote\s*(de\s*)?origen|lote\s*mp|lote\s*materia",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxLote = new(
            @"lote|c[oó]digo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Entre varias columnas de lote gana la más explícita: "LOTE DE
        /// PROCESO" antes que "Codigo", que en las tablas de insumos es el
        /// código del material y no un lote.
        /// </summary>
        private static readonly Regex RxLotePreferido = new(
            @"lote", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxCantidad = new(
            @"cantidad|lbs|libras|peso|kg|total\s*neto|neta?s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxProducto = new(
            @"producto|especie|item|art[ií]culo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxClasificacion = new(
            @"clasificaci[oó]n|calibre|talla|clase",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Títulos que delatan el rol de la tabla dentro del proceso.
        private static readonly Regex RxTituloEntrada = new(
            @"materia\s*prima|ingreso|recepci[oó]n|entrada|insumo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxTituloSalida = new(
            @"producci[oó]n|proceso|empaque|producto\s*(final|terminado)|salida",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxTituloMerma = new(
            @"subproducto|desperdicio|merma|residuo|aserr[ií]n",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Material de empaque e insumos: plástico, etiquetas, fundas. NO son
        /// lotes de producto y no deben tocar el inventario de trazabilidad,
        /// aunque el título contenga la palabra "empaque" y tenga una columna
        /// de código y otra de cantidad.
        /// </summary>
        private static readonly Regex RxTituloInsumo = new(
            @"material(es)?\s*(de\s*)?empaque|insumo|empaque\s*(e|y)\s*insumo",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// GET /api/DiagnosticoPlantillas
        ///
        /// ?soloPendientes=true  → devuelve únicamente las que faltan configurar
        /// ?incluirVacias=false  → omite plantillas sin tablas candidatas
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> Diagnosticar(
            [FromQuery] bool soloPendientes = false,
            [FromQuery] bool incluirVacias = false)
        {
            try
            {
                var plantillas = await _context.Templates
                    .AsNoTracking()
                    .Where(t => !t.IsObsolete)
                    .Select(t => new
                    {
                        t.TemplateID,
                        t.Codigo,
                        t.Nombre,
                        t.Proceso,
                        t.BodyElements,
                        t.UpdatedAt
                    })
                    .ToListAsync();

                // Cuántos formularios llenados tiene cada plantilla: una sin
                // configurar con 200 formularios urge mucho más que una con 2.
                var usos = await _context.FilledForms
                    .GroupBy(f => f.TemplateID)
                    .Select(g => new { TemplateID = g.Key, Total = g.Count() })
                    .ToDictionaryAsync(x => x.TemplateID, x => x.Total);

                var resultado = new List<PlantillaDiagnostico>();

                foreach (var p in plantillas)
                {
                    var tablas = AnalizarTablas(p.BodyElements);
                    if (tablas.Count == 0 && !incluirVacias) continue;

                    var pendientes = tablas.Count(t => t.Candidata && !t.Configurada);
                    var configuradas = tablas.Count(t => t.Configurada);
                    var conAvisos = tablas.Count(t => t.Avisos.Count > 0);

                    string estado;
                    if (tablas.Count(t => t.Candidata) == 0) estado = "sin_tablas";
                    else if (pendientes == 0 && conAvisos == 0) estado = "ok";
                    else if (configuradas > 0) estado = "parcial";
                    else estado = "pendiente";

                    if (soloPendientes && estado == "ok") continue;

                    resultado.Add(new PlantillaDiagnostico
                    {
                        TemplateID = p.TemplateID,
                        Codigo = p.Codigo,
                        Nombre = p.Nombre,
                        Proceso = p.Proceso,
                        UpdatedAt = p.UpdatedAt,
                        FormulariosLlenados = usos.TryGetValue(p.TemplateID, out var n) ? n : 0,
                        Estado = estado,
                        TablasCandidatas = tablas.Count(t => t.Candidata),
                        TablasConfiguradas = configuradas,
                        TablasPendientes = pendientes,
                        Tablas = tablas
                    });
                }

                // Primero lo que más duele: pendiente, y dentro de eso, la que
                // más formularios llenados tiene sin registrar movimiento.
                var orden = new Dictionary<string, int>
                {
                    ["pendiente"] = 0, ["parcial"] = 1, ["ok"] = 2, ["sin_tablas"] = 3
                };

                var lista = resultado
                    .OrderBy(r => orden[r.Estado])
                    .ThenByDescending(r => r.FormulariosLlenados)
                    .ToList();

                return Ok(new
                {
                    Total = lista.Count,
                    Resumen = new
                    {
                        Pendientes = lista.Count(r => r.Estado == "pendiente"),
                        Parciales  = lista.Count(r => r.Estado == "parcial"),
                        Ok         = lista.Count(r => r.Estado == "ok"),
                        FormulariosEnRiesgo = lista.Where(r => r.Estado != "ok").Sum(r => r.FormulariosLlenados)
                    },
                    Plantillas = lista
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al diagnosticar las plantillas");
                return StatusCode(500, new { message = "Error al analizar las plantillas." });
            }
        }

        // ── Análisis de una plantilla ────────────────────────────────────────

        private List<TablaDiagnostico> AnalizarTablas(string? bodyElements)
        {
            var salida = new List<TablaDiagnostico>();
            if (string.IsNullOrWhiteSpace(bodyElements)) return salida;

            JsonElement raiz;
            try
            {
                raiz = JsonSerializer.Deserialize<JsonElement>(bodyElements);
            }
            catch
            {
                return salida; // JSON inválido: la plantilla se salta, no se rompe el diagnóstico
            }

            if (raiz.ValueKind != JsonValueKind.Array) return salida;

            var indice = 0;

            foreach (var el in raiz.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) { indice++; continue; }
                if (Texto(el, "type") != "table") { indice++; continue; }

                var t = AnalizarTabla(el);
                if (t != null)
                {
                    t.Indice = indice;
                    // El "id" del elemento es lo que identifica la tabla de forma
                    // estable: el índice cambia si se agrega un elemento arriba.
                    t.TablaId = el.TryGetProperty("id", out var idEl)
                        ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetRawText() : idEl.ToString())
                        : null;
                    salida.Add(t);
                }
                indice++;
            }

            return salida;
        }

        private TablaDiagnostico? AnalizarTabla(JsonElement tabla)
        {
            var titulo = Texto(tabla, "title") ?? Texto(tabla, "label") ?? "(tabla sin título)";

            var columnas = new List<string>();
            if (tabla.TryGetProperty("columns", out var cols) && cols.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in cols.EnumerateArray())
                {
                    var label = Texto(c, "label") ?? Texto(c, "header");
                    if (!string.IsNullOrWhiteSpace(label)) columnas.Add(label!);
                }
            }
            if (columnas.Count == 0) return null;

            var usables      = columnas.Where(EsEncabezadoUsable).ToList();

            // El lote padre se saca primero y se quita del juego: si no, en la
            // tabla de Producción del PD-06 gana "CÓDIGO PADRE" por estar antes
            // que "LOTE DE PROCESO", y el lote nuevo nacería con el número del
            // padre.
            var colLotePadre = usables.FirstOrDefault(c => RxLotePadre.IsMatch(c));
            var candidatasLote = usables.Where(c => c != colLotePadre && RxLote.IsMatch(c)).ToList();
            var colLote      = candidatasLote.FirstOrDefault(c => RxLotePreferido.IsMatch(c))
                            ?? candidatasLote.FirstOrDefault();

            var colCantidad  = ElegirCantidad(columnas);
            var colProducto  = usables.FirstOrDefault(c => RxProducto.IsMatch(c));
            var colClasif    = usables.FirstOrDefault(c => RxClasificacion.IsMatch(c));

            // Es candidata si podría mover inventario: necesita lote y cantidad.
            var candidata = colLote != null && colCantidad != null;

            var descuenta = Bool(tabla, "descuentaInventario");
            var guarda    = Bool(tabla, "guardaInventario");
            var cambio    = Bool(tabla, "cambioProceso");
            var traspaso  = Bool(tabla, "registraSinDescontar");
            var configurada = descuenta || guarda || cambio || traspaso;

            // Rol sugerido a partir del título; si el título no dice nada, se
            // deja en "revisar" en vez de inventar una respuesta.
            // El orden importa: "MATERIALES DE EMPAQUE E INSUMOS" contiene
            // "empaque" y caería en "salida" si no se revisa insumo primero.
            string rolSugerido =
                RxTituloInsumo.IsMatch(titulo)  ? "insumo"  :
                RxTituloMerma.IsMatch(titulo)   ? "merma"   :
                RxTituloEntrada.IsMatch(titulo) ? "entrada" :
                RxTituloSalida.IsMatch(titulo)  ? "salida"  : "revisar";

            // Los insumos nunca son candidatos a mover el inventario de lotes:
            // ese "Codigo" es EM-PLT-059, no un lote de pescado, y la cantidad
            // puede venir en unidades o Kg mezclados.
            if (rolSugerido is "insumo" or "merma") candidata = false;

            var avisos = new List<string>();

            // ── Aviso 1: candidata sin configurar ──
            if (candidata && !configurada)
            {
                avisos.Add($"Tiene «{colLote}» y «{colCantidad}» pero no mueve inventario. " +
                           (rolSugerido == "entrada"
                                ? "Por el título parece MATERIA PRIMA: activá el panel naranja (restar saldo)."
                                : rolSugerido == "salida"
                                    ? "Por el título parece PRODUCCIÓN: activá el panel verde (crear saldo)."
                                    : "Revisá si debe crear saldo (verde) o restarlo (naranja)."));
            }

            if (rolSugerido == "insumo" && !configurada)
            {
                avisos.Add("Es material de empaque / insumo, no producto: no debería mover el inventario de lotes.");
            }

            // ── Aviso 2: configurada pero sin columna de cantidad ──
            if (descuenta && string.IsNullOrWhiteSpace(Texto(tabla, "descuentaCantidadCol")))
                avisos.Add("Resta inventario pero no tiene columna de cantidad: no descuenta nada.");

            if (guarda && string.IsNullOrWhiteSpace(Texto(tabla, "guardaCantidadCol")))
                avisos.Add("Crea lotes pero no tiene columna de peso: entran en 0.00 Lbs.");

            // ── Aviso 3: columna de cantidad sospechosa ──
            // El caso real del FOR-PD-06: se eligió "CAPACIDAD CAJAS / Lbs"
            // (libras POR CAJA) en vez de "TOTAL Lbs NETAS". Coinciden solo
            // cuando hay 1 caja; con varias, el lote nace con el peso mal.
            var guardaCant = Texto(tabla, "guardaCantidadCol");
            if (!string.IsNullOrWhiteSpace(guardaCant) && EsCantidadUnitaria(guardaCant!))
            {
                var mejor = columnas.FirstOrDefault(c => EsCantidadTotal(c));
                avisos.Add($"«{guardaCant}» parece ser cantidad POR UNIDAD, no el total." +
                           (mejor != null ? $" Probablemente debería ser «{mejor}»." : ""));
            }

            var descCant = Texto(tabla, "descuentaCantidadCol");
            if (!string.IsNullOrWhiteSpace(descCant) && EsCantidadUnitaria(descCant!))
            {
                var mejor = columnas.FirstOrDefault(c => EsCantidadTotal(c));
                avisos.Add($"«{descCant}» parece ser cantidad POR UNIDAD, no el total." +
                           (mejor != null ? $" Probablemente debería ser «{mejor}»." : ""));
            }

            // ── Aviso 4: las dos casillas a la vez ──
            if (descuenta && traspaso)
                avisos.Add("Están activos «restar saldo» y «solo registrar»: manda el descuento y el registro no se aplica.");

            return new TablaDiagnostico
            {
                Titulo = titulo,
                Columnas = columnas,
                Candidata = candidata,
                Configurada = configurada,
                RolSugerido = rolSugerido,
                Acciones = new
                {
                    Descuenta = descuenta,
                    Guarda = guarda,
                    CambioProceso = cambio,
                    SoloRegistra = traspaso
                },
                Sugerencia = new
                {
                    ColumnaLote = colLote,
                    ColumnaCantidad = colCantidad,
                    ColumnaProducto = colProducto,
                    ColumnaClasificacion = colClasif,
                    ColumnaLotePadre = colLotePadre
                },
                Avisos = avisos
            };
        }

        /// <summary>
        /// Un encabezado real es corto. Los formularios traen columnas que son
        /// párrafos de instrucciones ("Describa el proceso: (AGUJAS CO=...)") y
        /// esas contienen la palabra "lote" y la palabra "cantidad" sin ser ni
        /// una cosa ni la otra. Se descartan por largo.
        /// </summary>
        private const int LargoMaximoEncabezado = 60;

        private static bool EsEncabezadoUsable(string label) =>
            !string.IsNullOrWhiteSpace(label) && label.Trim().Length <= LargoMaximoEncabezado;

        /// <summary>
        /// Entre varias columnas de cantidad gana la que representa el TOTAL en
        /// PESO de la fila. Sin esto se elige la primera que aparezca, que en el
        /// PD-06 es la capacidad por caja.
        /// </summary>
        private static string? ElegirCantidad(List<string> columnas)
        {
            var candidatas = columnas
                .Where(EsEncabezadoUsable)
                .Where(c => RxCantidad.IsMatch(c))
                .ToList();
            if (candidatas.Count == 0) return null;

            return candidatas.FirstOrDefault(EsCantidadTotal)
                ?? candidatas.FirstOrDefault(c => TieneUnidadDePeso(c) && !EsCantidadUnitaria(c))
                ?? candidatas.FirstOrDefault(c => !EsCantidadUnitaria(c))
                ?? candidatas[0];
        }

        /// <summary>
        /// "TOTAL Lbs NETAS" sí; "TOTAL CAJAS DE EMPAQUE FINAL" no — son cajas,
        /// no libras, y descontar cajas del saldo dejaría el inventario mal.
        /// </summary>
        private static bool EsCantidadTotal(string label) =>
            Regex.IsMatch(label, @"total|neta?s?\b", RegexOptions.IgnoreCase)
            && TieneUnidadDePeso(label)
            && !EsCantidadUnitaria(label);

        private static bool TieneUnidadDePeso(string label) =>
            Regex.IsMatch(label, @"lbs?\b|libras|peso|kg\b|kilo|neta?s?\b", RegexOptions.IgnoreCase)
            && !Regex.IsMatch(label, @"cajas|fundas|sacos|gavetas|carros|unidades|bultos", RegexOptions.IgnoreCase);

        private static bool EsCantidadUnitaria(string label) =>
            Regex.IsMatch(label, @"capacidad|por\s*caja|unitario|c/u|x\s*caja", RegexOptions.IgnoreCase);

        // ── Helpers de lectura JSON ──────────────────────────────────────────

        private static string? Texto(JsonElement el, string prop) =>
            el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty(prop, out var v)
            && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static bool Bool(JsonElement el, string prop) =>
            el.ValueKind == JsonValueKind.Object
            && el.TryGetProperty(prop, out var v)
            && v.ValueKind == JsonValueKind.True;

        private class PlantillaDiagnostico
        {
            public int TemplateID { get; set; }
            public string Codigo { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string? Proceso { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public int FormulariosLlenados { get; set; }
            public string Estado { get; set; } = "";
            public int TablasCandidatas { get; set; }
            public int TablasConfiguradas { get; set; }
            public int TablasPendientes { get; set; }
            public List<TablaDiagnostico> Tablas { get; set; } = new();
        }

        // ── POST /api/DiagnosticoPlantillas/aplicar ──────────────────────────
        /// <summary>
        /// Escribe la configuración de inventario en el BodyElements de una
        /// plantilla. Sirve para CUALQUIER formulario: no hay nada específico
        /// del PD-06 acá — se indica qué tabla, qué rol y qué columnas.
        ///
        /// Escribe exactamente las mismas propiedades que el panel de Crear/
        /// Editar Plantilla (descuentaInventario / guardaInventario y sus
        /// columnas), así que lo aplicado acá se ve y se puede corregir desde
        /// el formulario de siempre.
        ///
        /// Con soloPrevisualizar=true no guarda nada: devuelve qué cambiaría.
        /// </summary>
        [HttpPost("aplicar")]
        public async Task<ActionResult<object>> Aplicar([FromBody] AplicarConfigDto dto)
        {
            if (dto == null || dto.Tablas == null || dto.Tablas.Count == 0)
                return BadRequest(new { message = "No se indicó ninguna tabla a configurar." });

            var plantilla = await _context.Templates.FirstOrDefaultAsync(t => t.TemplateID == dto.TemplateID);
            if (plantilla == null)
                return NotFound(new { message = $"Plantilla {dto.TemplateID} no encontrada." });

            if (string.IsNullOrWhiteSpace(plantilla.BodyElements))
                return BadRequest(new { message = "La plantilla no tiene elementos en el cuerpo." });

            JsonNode? raiz;
            try
            {
                raiz = JsonNode.Parse(plantilla.BodyElements);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BodyElements inválido en la plantilla {Id}", dto.TemplateID);
                return BadRequest(new { message = "El cuerpo de la plantilla no es JSON válido." });
            }

            if (raiz is not JsonArray elementos)
                return BadRequest(new { message = "El cuerpo de la plantilla no es una lista de elementos." });

            var cambios = new List<object>();
            var errores = new List<string>();

            foreach (var t in dto.Tablas)
            {
                var nodo = UbicarTabla(elementos, t.TablaId, t.Indice);
                if (nodo == null)
                {
                    errores.Add($"No se encontró la tabla «{t.TablaId ?? t.Indice.ToString()}».");
                    continue;
                }

                var titulo = nodo["title"]?.GetValue<string>() ?? nodo["label"]?.GetValue<string>() ?? "(sin título)";
                var columnas = ColumnasDe(nodo);

                // Que la columna elegida exista de verdad: un nombre mal escrito
                // deja la tabla configurada pero sin descontar nada, que es el
                // fallo más difícil de notar.
                foreach (var (valor, campo) in new[]
                {
                    (t.LoteCol, "columna de lote"),
                    (t.CantidadCol, "columna de cantidad"),
                    (t.ProductoCol, "columna de producto"),
                    (t.ClasificacionCol, "columna de clasificación"),
                })
                {
                    if (!string.IsNullOrWhiteSpace(valor) && !columnas.Contains(valor!))
                        errores.Add($"«{titulo}»: la {campo} «{valor}» no existe en esa tabla.");
                }

                var accion = (t.Accion ?? "").Trim().ToLowerInvariant();
                if (accion is not ("restar" or "crear" or "ninguna"))
                {
                    errores.Add($"«{titulo}»: acción «{t.Accion}» no válida (usar restar, crear o ninguna).");
                    continue;
                }

                if (accion != "ninguna")
                {
                    if (string.IsNullOrWhiteSpace(t.LoteCol))
                        errores.Add($"«{titulo}»: falta la columna de lote.");
                    if (string.IsNullOrWhiteSpace(t.CantidadCol))
                        errores.Add($"«{titulo}»: falta la columna de cantidad — sin ella no se mueve nada.");
                }

                if (errores.Count > 0) continue;

                var antes = new
                {
                    Descuenta = nodo["descuentaInventario"]?.GetValue<bool>() ?? false,
                    Guarda = nodo["guardaInventario"]?.GetValue<bool>() ?? false,
                };

                AplicarEnNodo(nodo, accion, t);

                cambios.Add(new
                {
                    Tabla = titulo,
                    Accion = accion,
                    Antes = antes,
                    Ahora = new
                    {
                        Descuenta = accion == "restar",
                        Guarda = accion == "crear",
                    },
                    t.LoteCol,
                    t.CantidadCol,
                    t.ProductoCol,
                    t.ClasificacionCol
                });
            }

            if (errores.Count > 0)
                return BadRequest(new { message = "La configuración tiene errores.", errores });

            if (dto.SoloPrevisualizar)
                return Ok(new { previsualizacion = true, cambios });

            plantilla.BodyElements = elementos.ToJsonString();
            plantilla.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Configuración de inventario aplicada a la plantilla {Codigo} ({Id}): {N} tabla(s)",
                plantilla.Codigo, plantilla.TemplateID, cambios.Count);

            return Ok(new
            {
                message = $"Configuración guardada en {plantilla.Codigo}.",
                plantilla.TemplateID,
                plantilla.Codigo,
                cambios,
                // Los formularios ya llenados no generan movimientos solos: el
                // descuento corre al guardar. Se avisa para que no se espere
                // ver el inventario cambiar de inmediato.
                nota = "A partir de ahora los formularios NUEVOS mueven inventario. Los ya llenados no se recalculan."
            });
        }

        /// <summary>Busca la tabla por su id; si no aparece, cae al índice.</summary>
        private static JsonObject? UbicarTabla(JsonArray elementos, string? tablaId, int indice)
        {
            if (!string.IsNullOrWhiteSpace(tablaId))
            {
                foreach (var el in elementos)
                {
                    if (el is not JsonObject o) continue;
                    if (o["type"]?.GetValue<string>() != "table") continue;
                    var id = o["id"];
                    if (id != null && id.ToJsonString().Trim('"') == tablaId!.Trim('"')) return o;
                }
            }

            if (indice >= 0 && indice < elementos.Count && elementos[indice] is JsonObject porIndice
                && porIndice["type"]?.GetValue<string>() == "table")
                return porIndice;

            return null;
        }

        private static HashSet<string> ColumnasDe(JsonObject tabla)
        {
            var set = new HashSet<string>();
            if (tabla["columns"] is JsonArray cols)
            {
                foreach (var c in cols)
                {
                    var label = c?["label"]?.GetValue<string>() ?? c?["header"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(label)) set.Add(label!);
                }
            }
            return set;
        }

        /// <summary>
        /// Escribe las propiedades sobre el nodo de la tabla sin tocar nada más:
        /// las columnas, el título y cualquier ajuste previo se conservan.
        /// </summary>
        private static void AplicarEnNodo(JsonObject nodo, string accion, TablaConfigDto t)
        {
            // Las dos casillas son excluyentes: si estuvieran las dos, el
            // servicio del frontend aplica el descuento y la entrada se pierde.
            nodo["descuentaInventario"] = accion == "restar";
            nodo["guardaInventario"] = accion == "crear";

            if (accion == "restar")
            {
                nodo["descuentaLoteCol"] = t.LoteCol;
                nodo["descuentaCantidadCol"] = t.CantidadCol;
                if (!string.IsNullOrWhiteSpace(t.ProductoCol)) nodo["descuentaProductoCol"] = t.ProductoCol;
                if (!string.IsNullOrWhiteSpace(t.Proceso)) nodo["descuentaProceso"] = t.Proceso;
            }
            else if (accion == "crear")
            {
                nodo["guardaLoteCol"] = t.LoteCol;
                nodo["guardaCantidadCol"] = t.CantidadCol;
                if (!string.IsNullOrWhiteSpace(t.ProductoCol)) nodo["guardaProductoCol"] = t.ProductoCol;
                if (!string.IsNullOrWhiteSpace(t.ClasificacionCol)) nodo["guardaClasificacionCol"] = t.ClasificacionCol;
                if (!string.IsNullOrWhiteSpace(t.LotePadreCol)) nodo["guardaLotePadreCol"] = t.LotePadreCol;
                if (!string.IsNullOrWhiteSpace(t.Proceso)) nodo["guardaProceso"] = t.Proceso;
            }
        }

        private class TablaDiagnostico
        {
            /// <summary>El "id" del elemento en BodyElements. Identifica la tabla de forma estable.</summary>
            public string? TablaId { get; set; }
            /// <summary>Posición dentro del array de BodyElements. Respaldo si no hay id.</summary>
            public int Indice { get; set; }
            public string Titulo { get; set; } = "";
            public List<string> Columnas { get; set; } = new();
            public bool Candidata { get; set; }
            public bool Configurada { get; set; }
            public string RolSugerido { get; set; } = "revisar";
            public object Acciones { get; set; } = new { };
            public object Sugerencia { get; set; } = new { };
            public List<string> Avisos { get; set; } = new();
        }
    }

    // ── DTOs de entrada ──────────────────────────────────────────────────────

    public class AplicarConfigDto
    {
        public int TemplateID { get; set; }
        public List<TablaConfigDto> Tablas { get; set; } = new();
        /// <summary>true = no guarda, solo devuelve qué cambiaría.</summary>
        public bool SoloPrevisualizar { get; set; }
    }

    public class TablaConfigDto
    {
        /// <summary>El "id" del elemento en BodyElements (lo devuelve el diagnóstico).</summary>
        public string? TablaId { get; set; }
        /// <summary>Respaldo por posición si la tabla no tiene id.</summary>
        public int Indice { get; set; } = -1;

        /// <summary>restar | crear | ninguna</summary>
        public string? Accion { get; set; }

        public string? LoteCol { get; set; }
        public string? CantidadCol { get; set; }
        public string? ProductoCol { get; set; }
        public string? ClasificacionCol { get; set; }
        public string? LotePadreCol { get; set; }
        public string? Proceso { get; set; }
    }
}