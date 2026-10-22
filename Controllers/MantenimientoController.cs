using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FormBuilder.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    // Endpoints de mantenimiento puntual (correcciones de datos que se corren una sola vez).
    [ApiController]
    [Route("api/[controller]")]
    public class MantenimientoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MantenimientoController> _logger;

        // Texto exacto a quitar del encabezado de columna (tal como está guardado).
        private const string Buscar = "2.5mm Fe, ";

        public MantenimientoController(ApplicationDbContext context, ILogger<MantenimientoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Quita el texto ignorando mayúsculas/minúsculas (por si en la base quedó con otra
        // combinación, ej. "2.5MM FE, "). Devuelve el mismo texto si no hay nada que quitar.
        private static string? QuitarTexto(string? origen)
        {
            if (string.IsNullOrEmpty(origen)) return origen;
            return origen.Replace(Buscar, "", System.StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------------
        // GET /api/Mantenimiento/diagnostico-bd
        // SOLO LEE. Dice a qué base está conectada la API, qué migraciones faltan
        // por aplicar y prueba cada tabla crítica devolviendo el error SQL real
        // (por ejemplo "Invalid object name 'Tableros'"), que de otra forma queda
        // escondido detrás de un 500 genérico.
        // ---------------------------------------------------------------------
        [HttpGet("diagnostico-bd")]
        public async Task<ActionResult> DiagnosticoBd()
        {
            var conn = _context.Database.GetDbConnection();

            string[] aplicadas, pendientes;
            string? errorMigraciones = null;
            try
            {
                aplicadas = (await _context.Database.GetAppliedMigrationsAsync()).ToArray();
                pendientes = (await _context.Database.GetPendingMigrationsAsync()).ToArray();
            }
            catch (Exception ex)
            {
                aplicadas = Array.Empty<string>();
                pendientes = Array.Empty<string>();
                errorMigraciones = ex.GetBaseException().Message;
            }

            // Cada tabla se prueba por separado: si una falla, las demás igual se reportan.
            var tablas = new (string Nombre, Func<Task<int>> Contar)[]
            {
                ("Templates",           () => _context.Templates.CountAsync()),
                ("FilledForms",         () => _context.FilledForms.CountAsync()),
                ("Indicadores",         () => _context.Indicadores.CountAsync()),
                ("Tableros",            () => _context.Tableros.CountAsync()),
                ("DocumentosManuales",  () => _context.DocumentosManuales.CountAsync()),
                ("LotesInventario",     () => _context.LotesInventario.CountAsync()),
            };

            var resultado = new List<object>();
            foreach (var (nombre, contar) in tablas)
            {
                try
                {
                    resultado.Add(new { tabla = nombre, ok = true, filas = await contar(), error = (string?)null });
                }
                catch (Exception ex)
                {
                    resultado.Add(new { tabla = nombre, ok = false, filas = (int?)null, error = ex.GetBaseException().Message });
                }
            }

            return Ok(new
            {
                servidor = conn.DataSource,
                baseDeDatos = conn.Database,
                migracionesAplicadas = aplicadas,
                migracionesPendientes = pendientes,
                errorMigraciones,
                tablas = resultado
            });
        }

        // ---------------------------------------------------------------------
        // GET /api/Mantenimiento/preview-quitar-25mmfe?codigo=FOR-CC-16
        // SOLO CUENTA. No cambia nada. Seguro para abrir en el navegador.
        // ---------------------------------------------------------------------
        [HttpGet("preview-quitar-25mmfe")]
        public async Task<ActionResult> Preview([FromQuery] string codigo = "FOR-CC-16")
        {
            var templateIds = await _context.Templates
                .Where(t => t.Codigo == codigo)
                .Select(t => t.TemplateID)
                .ToListAsync();

            var plantillas = await _context.Templates
                .Where(t => t.Codigo == codigo && t.BodyElements != null && t.BodyElements.Contains(Buscar))
                .CountAsync();

            var registros = await _context.FilledForms
                .Where(f => templateIds.Contains(f.TemplateID) &&
                    ((f.BodyData != null && f.BodyData.Contains(Buscar)) ||
                     (f.TemplateSnapshot != null && f.TemplateSnapshot.Contains(Buscar))))
                .CountAsync();

            var versiones = await _context.TemplateVersions
                .Where(v => templateIds.Contains(v.TemplateID) && v.BodyElements != null && v.BodyElements.Contains(Buscar))
                .CountAsync();

            return Ok(new
            {
                codigo,
                textoABuscar = Buscar,
                templateIds,
                plantillasConTexto = plantillas,
                registrosConTexto = registros,
                versionesConTexto = versiones,
                mensaje = "Vista previa: NO se cambió nada. Para aplicar, haz POST a /api/Mantenimiento/quitar-25mmfe?codigo=" + codigo
            });
        }

        // ---------------------------------------------------------------------
        // POST /api/Mantenimiento/quitar-25mmfe?codigo=FOR-CC-16
        // APLICA el cambio: quita "2.5mm Fe, " de la plantilla, de cada registro
        // (BodyData) y de su copia congelada (TemplateSnapshot), y del historial
        // de versiones. Todo en una sola transacción.
        // ---------------------------------------------------------------------
        [HttpPost("quitar-25mmfe")]
        public async Task<ActionResult> Aplicar([FromQuery] string codigo = "FOR-CC-16")
        {
            try
            {
                var templates = await _context.Templates.Where(t => t.Codigo == codigo).ToListAsync();
                if (templates.Count == 0)
                    return NotFound(new { message = $"No se encontró ninguna plantilla con código '{codigo}'." });

                var templateIds = templates.Select(t => t.TemplateID).ToList();

                int plantillasCambiadas = 0;
                foreach (var t in templates)
                {
                    var nuevo = QuitarTexto(t.BodyElements);
                    if (nuevo != t.BodyElements) { t.BodyElements = nuevo; plantillasCambiadas++; }
                }

                var forms = await _context.FilledForms
                    .Where(f => templateIds.Contains(f.TemplateID))
                    .ToListAsync();
                int registrosCambiados = 0;
                foreach (var f in forms)
                {
                    bool cambiado = false;
                    var nuevoBody = QuitarTexto(f.BodyData);
                    if (nuevoBody != f.BodyData) { f.BodyData = nuevoBody; cambiado = true; }
                    var nuevoSnap = QuitarTexto(f.TemplateSnapshot);
                    if (nuevoSnap != f.TemplateSnapshot) { f.TemplateSnapshot = nuevoSnap; cambiado = true; }
                    if (cambiado) registrosCambiados++;
                }

                var versions = await _context.TemplateVersions
                    .Where(v => templateIds.Contains(v.TemplateID))
                    .ToListAsync();
                int versionesCambiadas = 0;
                foreach (var v in versions)
                {
                    var nuevo = QuitarTexto(v.BodyElements);
                    if (nuevo != v.BodyElements) { v.BodyElements = nuevo; versionesCambiadas++; }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Mantenimiento quitar-25mmfe aplicado a {Codigo}: {P} plantillas, {R} registros, {V} versiones",
                    codigo, plantillasCambiadas, registrosCambiados, versionesCambiadas);

                return Ok(new
                {
                    success = true,
                    codigo,
                    plantillasCambiadas,
                    registrosCambiados,
                    versionesCambiadas,
                    mensaje = "Listo. Se quitó '2.5mm Fe, ' de la plantilla, los registros y sus copias congeladas."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar mantenimiento quitar-25mmfe");
                return StatusCode(500, new { message = "Error al aplicar el cambio", detalle = ex.Message });
            }
        }

        // =====================================================================
        // NOTAS ESTÁTICAS EN VERSIONES ANTERIORES
        // =====================================================================
        // Las notas estáticas son bloques de solo texto (referencias legales,
        // declaraciones de etiqueta, etc.). Cuando se agregan a la plantilla,
        // los registros viejos NO las muestran, porque cada uno guarda su copia
        // congelada (TemplateSnapshot) y cada versión histórica tiene su propio
        // BodyElements.
        //
        // Estos endpoints copian las notas de la versión actual hacia las
        // versiones anteriores y hacia las copias congeladas de los registros.
        // Nunca borran ni modifican datos capturados: solo agregan texto.
        // =====================================================================

        private static readonly JsonSerializerOptions JsonSinEscape = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>Lee un BodyElements que puede venir como arreglo o como texto con JSON adentro.</summary>
        private static List<JsonElement> ListaElementos(string? json)
        {
            var lista = new List<JsonElement>();
            if (string.IsNullOrWhiteSpace(json)) return lista;

            var texto = json;
            for (int intento = 0; intento < 2; intento++)
            {
                try
                {
                    using var doc = JsonDocument.Parse(texto);
                    var raiz = doc.RootElement;

                    if (raiz.ValueKind == JsonValueKind.String)
                    {
                        texto = raiz.GetString() ?? string.Empty;
                        continue;
                    }

                    if (raiz.ValueKind == JsonValueKind.Array)
                        foreach (var el in raiz.EnumerateArray())
                            lista.Add(el.Clone());

                    return lista;
                }
                catch (JsonException)
                {
                    return lista;
                }
            }

            return lista;
        }

        private static bool EsNotaEstatica(JsonElement el) =>
            el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("type", out var tipo) &&
            tipo.ValueKind == JsonValueKind.String &&
            string.Equals(tipo.GetString(), "nota_estatica", StringComparison.OrdinalIgnoreCase);

        private static string? IdDe(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("id", out var id)) return null;
            return id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number => id.GetRawText(),
                _ => null
            };
        }

        private static string? TituloDe(JsonElement el)
        {
            if (el.ValueKind != JsonValueKind.Object) return null;
            foreach (var prop in new[] { "title", "titulo" })
                if (el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            return null;
        }

        /// <summary>Agrega al BodyElements las notas que todavía no estén (por id).</summary>
        private static string? AgregarNotas(string? bodyJson, IReadOnlyList<JsonElement> notas, out int agregadas)
        {
            agregadas = 0;
            if (string.IsNullOrWhiteSpace(bodyJson) || notas.Count == 0) return bodyJson;

            var lista = ListaElementos(bodyJson);
            if (lista.Count == 0) return bodyJson; // JSON dañado: no se toca

            var existentes = new HashSet<string>();
            foreach (var el in lista)
            {
                var id = IdDe(el);
                if (id != null) existentes.Add(id);
            }

            foreach (var nota in notas)
            {
                var id = IdDe(nota);
                if (id == null || !existentes.Add(id)) continue;
                lista.Add(nota);
                agregadas++;
            }

            return agregadas == 0 ? bodyJson : JsonSerializer.Serialize(lista, JsonSinEscape);
        }

        /// <summary>Agrega las notas dentro de la propiedad BodyElements de un TemplateSnapshot.</summary>
        private static string? AgregarNotasASnapshot(string? snapshot, IReadOnlyList<JsonElement> notas, out int agregadas)
        {
            agregadas = 0;
            if (string.IsNullOrWhiteSpace(snapshot) || notas.Count == 0) return snapshot;

            JsonNode? nodo;
            try
            {
                nodo = JsonNode.Parse(snapshot);
            }
            catch (JsonException)
            {
                return snapshot;
            }

            if (nodo is not JsonObject obj) return snapshot;

            var clave = obj.Select(kv => kv.Key)
                .FirstOrDefault(k => string.Equals(k, "BodyElements", StringComparison.OrdinalIgnoreCase));
            if (clave == null) return snapshot;

            var valor = obj[clave];
            var texto = valor is JsonValue v && v.TryGetValue<string>(out var s)
                ? s
                : valor?.ToJsonString(JsonSinEscape);
            if (string.IsNullOrWhiteSpace(texto)) return snapshot;

            var nuevo = AgregarNotas(texto, notas, out agregadas);
            if (agregadas == 0 || nuevo == null) return snapshot;

            obj[clave] = JsonValue.Create(nuevo);
            return obj.ToJsonString(JsonSinEscape);
        }

        private static List<JsonElement> NotasDe(string? bodyElements) =>
            ListaElementos(bodyElements).Where(EsNotaEstatica).ToList();

        /// <summary>Ids de las notas que le faltan a un BodyElements.</summary>
        private static List<string> NotasFaltantes(string? bodyJson, IReadOnlyList<JsonElement> notas)
        {
            var existentes = new HashSet<string>();
            foreach (var el in ListaElementos(bodyJson))
            {
                var id = IdDe(el);
                if (id != null) existentes.Add(id);
            }

            return notas
                .Select(IdDe)
                .Where(id => id != null && !existentes.Contains(id))
                .Select(id => id!)
                .ToList();
        }

        // ---------------------------------------------------------------------
        // GET /api/Mantenimiento/notas-faltantes?codigo=FOR-CC-07
        // SOLO LEE. Muestra qué notas le faltan a cada versión y a los registros.
        // ---------------------------------------------------------------------
        [HttpGet("notas-faltantes")]
        public async Task<ActionResult> NotasFaltantesPreview([FromQuery] string codigo = "FOR-CC-07")
        {
            var template = await _context.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Codigo == codigo);
            if (template == null)
                return NotFound(new { message = $"No se encontró ninguna plantilla con código '{codigo}'." });

            var notas = NotasDe(template.BodyElements);

            var versiones = await _context.TemplateVersions.AsNoTracking()
                .Where(v => v.TemplateID == template.TemplateID)
                .OrderBy(v => v.Version)
                .ToListAsync();

            var detalleVersiones = versiones.Select(v => new
            {
                version = v.Version,
                esActual = v.Version == template.Version,
                elementos = ListaElementos(v.BodyElements).Count,
                notasFaltantes = NotasFaltantes(v.BodyElements, notas).Count
            }).ToList();

            var forms = await _context.FilledForms.AsNoTracking()
                .Where(f => f.TemplateID == template.TemplateID &&
                            f.TemplateSnapshot != null && f.TemplateSnapshot != "")
                .ToListAsync();

            var sinNotas = 0;
            var porVersion = new Dictionary<string, int>();
            foreach (var f in forms)
            {
                AgregarNotasASnapshot(f.TemplateSnapshot, notas, out var agregadas);
                if (agregadas == 0) continue;
                sinNotas++;
                var clave = string.IsNullOrWhiteSpace(f.TemplateVersion) ? "(sin versión)" : f.TemplateVersion!;
                porVersion[clave] = porVersion.GetValueOrDefault(clave) + 1;
            }

            return Ok(new
            {
                codigo,
                templateId = template.TemplateID,
                versionActual = template.Version,
                notas = notas.Select(n => new { id = IdDe(n), titulo = TituloDe(n) }),
                versiones = detalleVersiones,
                registros = new
                {
                    total = forms.Count,
                    sinNotas,
                    porVersion
                },
                mensaje = "Vista previa: NO se cambió nada. Para aplicar, haz POST a /api/Mantenimiento/copiar-notas-versiones?codigo=" + codigo
            });
        }

        // ---------------------------------------------------------------------
        // POST /api/Mantenimiento/copiar-notas-versiones?codigo=FOR-CC-07
        // APLICA el cambio: copia las notas estáticas de la versión actual a las
        // versiones históricas y a las copias congeladas de los registros.
        // ---------------------------------------------------------------------
        [HttpPost("copiar-notas-versiones")]
        public async Task<ActionResult> CopiarNotasVersiones([FromQuery] string codigo = "FOR-CC-07")
        {
            try
            {
                var template = await _context.Templates
                    .FirstOrDefaultAsync(t => t.Codigo == codigo);
                if (template == null)
                    return NotFound(new { message = $"No se encontró ninguna plantilla con código '{codigo}'." });

                var notas = NotasDe(template.BodyElements);
                if (notas.Count == 0)
                    return BadRequest(new { message = $"La plantilla '{codigo}' no tiene notas estáticas para copiar." });

                var detalleVersiones = new List<object>();
                int versionesCambiadas = 0;
                int notasEnVersiones = 0;

                var versiones = await _context.TemplateVersions
                    .Where(v => v.TemplateID == template.TemplateID)
                    .ToListAsync();

                foreach (var v in versiones)
                {
                    // Idempotente: si la versión ya tiene todas las notas, no se toca.
                    var nuevo = AgregarNotas(v.BodyElements, notas, out var agregadas);
                    if (agregadas == 0) continue;

                    v.BodyElements = nuevo;
                    versionesCambiadas++;
                    notasEnVersiones += agregadas;
                    detalleVersiones.Add(new { version = v.Version, notasAgregadas = agregadas });
                }

                int registrosCambiados = 0;
                int notasEnRegistros = 0;
                var porVersion = new Dictionary<string, int>();

                var forms = await _context.FilledForms
                    .Where(f => f.TemplateID == template.TemplateID &&
                                f.TemplateSnapshot != null && f.TemplateSnapshot != "")
                    .ToListAsync();

                foreach (var f in forms)
                {
                    var nuevo = AgregarNotasASnapshot(f.TemplateSnapshot, notas, out var agregadas);
                    if (agregadas == 0) continue;

                    f.TemplateSnapshot = nuevo;
                    registrosCambiados++;
                    notasEnRegistros += agregadas;

                    var clave = string.IsNullOrWhiteSpace(f.TemplateVersion) ? "(sin versión)" : f.TemplateVersion!;
                    porVersion[clave] = porVersion.GetValueOrDefault(clave) + 1;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Mantenimiento copiar-notas-versiones {Codigo}: {V} versiones (+{NV} notas), {R} registros (+{NR} notas)",
                    codigo, versionesCambiadas, notasEnVersiones, registrosCambiados, notasEnRegistros);

                return Ok(new
                {
                    success = true,
                    codigo,
                    notasDisponibles = notas.Count,
                    versionesCambiadas,
                    notasEnVersiones,
                    detalleVersiones,
                    registrosCambiados,
                    notasEnRegistros,
                    porVersion,
                    mensaje = "Listo. Las notas estáticas ya están en las versiones anteriores y en los registros existentes."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al copiar notas a versiones anteriores");
                return StatusCode(500, new { message = "Error al aplicar el cambio", detalle = ex.Message });
            }
        }
    }
}
