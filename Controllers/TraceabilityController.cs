using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TraceabilityController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TraceabilityController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Traceability/lotes
        /// <summary>
        /// La fecha que vale de un registro es la que ESCRIBIO el operario en el
        /// encabezado, no la de cuando se guardo. Un registro del turno noche se
        /// guarda pasada la medianoche y aparecia con la fecha del dia siguiente:
        /// buscar "del 24 al 24" no lo encontraba. Sobre los datos de planta, 693
        /// de 1.600 formularios (43 %) tienen una fecha distinta a la de guardado.
        /// Misma regla que utils/fechaFormulario.js en la pantalla.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex NoEsLaFecha =
            new System.Text.RegularExpressions.Regex(
                "VENCIM|CADUC|EXPIR|VERSION|NACIM",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        private static string SinTildes(string texto)
        {
            var d = texto.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var c in d)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            }
            return sb.ToString().ToUpperInvariant().Trim();
        }

        private static string? ADiaIso(string? valor)
        {
            var v = (valor ?? "").Trim();
            if (v.Length == 0) return null;

            var iso = System.Text.RegularExpressions.Regex.Match(v, @"^(DDDD)-(DD)-(DD)".Replace("DDDD", "[0-9]{4}").Replace("DD", "[0-9]{2}"));
            if (iso.Success) return iso.Value;

            var dmy = System.Text.RegularExpressions.Regex.Match(v, "^([0-9]{1,2})[/-]([0-9]{1,2})[/-]([0-9]{4})");
            if (dmy.Success)
                return dmy.Groups[3].Value + "-" + dmy.Groups[2].Value.PadLeft(2, '0') + "-" + dmy.Groups[1].Value.PadLeft(2, '0');

            return null;
        }

        /// <summary>Fecha del encabezado, o la de guardado si no hay ninguna usable.</summary>
        private static string FechaDelRegistro(string? headerData, DateTime createdAt)
        {
            var porDefecto = createdAt.ToString("yyyy-MM-dd");
            if (string.IsNullOrWhiteSpace(headerData)) return porDefecto;

            try
            {
                var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerData);
                if (doc == null) return porDefecto;

                string? exacta = null, aproximada = null;
                foreach (var kv in doc)
                {
                    var nombre = SinTildes(kv.Key);
                    if (!nombre.Contains("FECHA") || NoEsLaFecha.IsMatch(nombre)) continue;

                    var crudo = kv.Value.ValueKind == JsonValueKind.String
                        ? kv.Value.GetString()
                        : kv.Value.ToString();
                    var dia = ADiaIso(crudo);
                    if (dia == null) continue;

                    // "Fecha" a secas manda sobre "Fecha de recepcion" y similares.
                    if (nombre == "FECHA") { exacta = dia; break; }
                    aproximada ??= dia;
                }
                return exacta ?? aproximada ?? porDefecto;
            }
            catch { return porDefecto; }
        }

        [HttpGet("lotes")]
        public async Task<ActionResult<object>> GetLotes([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var dateFrom = from ?? DateTime.Now.AddDays(-60);
            var dateTo = to ?? DateTime.Now.AddDays(1);

            // El filtro real es por la fecha del ENCABEZADO, que vive dentro de un
            // JSON y no se puede consultar en SQL. Se trae una ventana mas ancha
            // por los dos lados —el desfase entre lo escrito y lo guardado llega a
            // 26 dias en los datos de planta— y el recorte fino se hace en memoria.
            // Filtro EXACTO por FechaRegistro, la columna con la fecha del
            // encabezado. Antes había que traer ±45 días y recortar en memoria,
            // porque el dato vivía dentro del JSON y SQL no lo podía leer: cada
            // búsqueda arrastraba 90 días de formularios para quedarse con unos
            // pocos.
            //
            // El respaldo por CreatedAt se mantiene para los formularios que
            // todavía no tengan la columna cargada.
            var desdeDia = dateFrom.ToString("yyyy-MM-dd");
            var hastaDiaExcl = dateTo.ToString("yyyy-MM-dd");

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.FechaRegistro != null
                    ? (f.FechaRegistro >= dateFrom.Date && f.FechaRegistro < dateTo.Date)
                    : (f.CreatedAt >= dateFrom.AddDays(-45) && f.CreatedAt < dateTo.AddDays(45)))
                .OrderByDescending(f => f.FechaRegistro ?? f.CreatedAt)
                .Select(f => new {
                    f.FormID,
                    f.CreatedAt,
                    f.FechaRegistro,
                    f.HeaderData,
                    f.BodyData,
                    TemplateCodigo = f.Template.Codigo,
                    f.TipoProducto
                })
                .ToListAsync();

            var lotesDict = new Dictionary<string, LoteInfo>();

            foreach (var form in forms)
            {
                // La fecha del operario, no la del servidor. Los que ya vienen
                // filtrados por columna pasan derecho; el recorte queda para los
                // que entraron por el respaldo de CreatedAt.
                var dateStr = form.FechaRegistro?.ToString("yyyy-MM-dd")
                    ?? FechaDelRegistro(form.HeaderData, form.CreatedAt);
                if (string.Compare(dateStr, desdeDia) < 0) continue;
                if (string.Compare(dateStr, hastaDiaExcl) >= 0) continue;
                var foundLotes = new HashSet<string>();
                string? productoEncontrado = null;
                string? subproductoEncontrado = null;

                // Search in HeaderData
                if (!string.IsNullOrEmpty(form.HeaderData))
                {
                    try
                    {
                        var headerData = JsonSerializer.Deserialize<Dictionary<string, object>>(form.HeaderData);
                        if (headerData != null)
                        {
                            foreach (var kvp in headerData)
                            {
                                if (kvp.Key.Contains("lote", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                                {
                                    var val = kvp.Value.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(val))
                                        foundLotes.Add(val);
                                }
                                else if (kvp.Key.Contains("producto", StringComparison.OrdinalIgnoreCase) && 
                                         !kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && 
                                         kvp.Value != null && productoEncontrado == null)
                                {
                                    productoEncontrado = kvp.Value.ToString()?.Trim();
                                }
                                else if (kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && 
                                         kvp.Value != null && subproductoEncontrado == null)
                                {
                                    subproductoEncontrado = kvp.Value.ToString()?.Trim();
                                }
                            }
                        }
                    }
                    catch { }
                }

                // Si no encontramos producto en el HeaderData, intentamos usar TipoProducto
                if (string.IsNullOrEmpty(productoEncontrado) && !string.IsNullOrEmpty(form.TipoProducto))
                {
                    productoEncontrado = form.TipoProducto;
                }

                // Search in BodyData
                if (!string.IsNullOrEmpty(form.BodyData))
                {
                    try
                    {
                        var bodyData = JsonSerializer.Deserialize<List<JsonElement>>(form.BodyData);
                        if (bodyData != null)
                        {
                            foreach (var element in bodyData)
                            {
                                if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "table")
                                {
                                    if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var row in dataProp.EnumerateArray())
                                        {
                                            if (row.ValueKind == JsonValueKind.Object)
                                            {
                                                foreach (var prop in row.EnumerateObject())
                                                {
                                                    if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        var val = prop.Value.ToString()?.Trim();
                                                        if (!string.IsNullOrEmpty(val))
                                                            foundLotes.Add(val);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                foreach (var lote in foundLotes)
                {
                    if (!lotesDict.ContainsKey(lote))
                    {
                        lotesDict[lote] = new LoteInfo
                        {
                            Count = 0,
                            FirstDate = dateStr,
                            LastDate = dateStr,
                            Codes = new HashSet<string>(),
                            Productos = new HashSet<string>(),
                            Subproductos = new HashSet<string>()
                        };
                    }

                    lotesDict[lote].Count++;
                    lotesDict[lote].Codes.Add(form.TemplateCodigo);
                    
                    if (!string.IsNullOrEmpty(productoEncontrado))
                        lotesDict[lote].Productos.Add(productoEncontrado);
                    
                    if (!string.IsNullOrEmpty(subproductoEncontrado))
                        lotesDict[lote].Subproductos.Add(subproductoEncontrado);
                    
                    if (string.Compare(dateStr, lotesDict[lote].LastDate) > 0)
                        lotesDict[lote].LastDate = dateStr;
                    if (string.Compare(dateStr, lotesDict[lote].FirstDate) < 0)
                        lotesDict[lote].FirstDate = dateStr;
                }
            }

            var lotesList = lotesDict.Select(kvp => new {
                lote = kvp.Key,
                count = kvp.Value.Count,
                first_date = kvp.Value.FirstDate,
                last_date = kvp.Value.LastDate,
                templates = kvp.Value.Codes.OrderBy(c => c).ToList(),
                productos = kvp.Value.Productos.ToList(),
                subproductos = kvp.Value.Subproductos.ToList()
            })
            .OrderByDescending(l => l.last_date)
            .ToList();

            return Ok(new
            {
                total = lotesList.Count,
                lotes = lotesList
            });
        }

        // GET: api/Traceability/lote/{numeroLote}
        [HttpGet("lote/{numeroLote}")]
        public async Task<ActionResult<object>> GetLoteTraceability(string numeroLote)
        {
            if (string.IsNullOrWhiteSpace(numeroLote))
                return BadRequest("El número de lote es requerido.");

            var searchPattern = numeroLote.Trim().ToLower();

            // First find forms that might contain the lote text somewhere
            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.HeaderData.ToLower().Contains(searchPattern) || f.BodyData.ToLower().Contains(searchPattern))
                // Orden cronológico por la fecha del REGISTRO: en trazabilidad
                // la secuencia es el dato. Ordenar por CreatedAt ponía primero
                // el formulario que alguien guardó antes, no el que ocurrió
                // antes en el proceso.
                .OrderBy(f => f.FechaRegistro ?? f.CreatedAt.Date).ThenBy(f => f.CreatedAt)
                .Select(f => new {
                    f.FormID,
                    f.TemplateID,
                    f.FechaRegistro,
                    TemplateCodigo = f.Template.Codigo,
                    TemplateNombre = f.Template.Nombre,
                    TemplateProceso = f.Template.Proceso,
                    f.CreatedAt,
                    f.FilledBy,
                    f.FilledByRole,
                    f.TipoProducto,
                    f.Observaciones,
                    f.HeaderData,
                    f.BodyData
                })
                .ToListAsync();

            var matchingForms = new List<object>();

            foreach (var form in forms)
            {
                bool loteFound = false;
                var headerObj = new Dictionary<string, object>();
                var matchingBodyRows = new List<Dictionary<string, object>>();

                if (!string.IsNullOrEmpty(form.HeaderData))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(form.HeaderData);
                        if (dict != null)
                        {
                            headerObj = dict;
                            foreach (var kvp in dict)
                            {
                                if (kvp.Key.Contains("lote", StringComparison.OrdinalIgnoreCase) && 
                                    kvp.Value?.ToString()?.Trim().Equals(searchPattern, StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    loteFound = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrEmpty(form.BodyData))
                {
                    try
                    {
                        var bodyData = JsonSerializer.Deserialize<List<JsonElement>>(form.BodyData);
                        if (bodyData != null)
                        {
                            foreach (var element in bodyData)
                            {
                                if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "table")
                                {
                                    if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var row in dataProp.EnumerateArray())
                                        {
                                            if (row.ValueKind == JsonValueKind.Object)
                                            {
                                                bool rowHasLote = false;
                                                var rowDict = new Dictionary<string, object>();
                                                
                                                foreach (var prop in row.EnumerateObject())
                                                {
                                                    var val = prop.Value.ToString();
                                                    rowDict[prop.Name] = val ?? string.Empty;
                                                    
                                                    if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase) && 
                                                        val?.Trim().Equals(searchPattern, StringComparison.OrdinalIgnoreCase) == true)
                                                    {
                                                        rowHasLote = true;
                                                    }
                                                }
                                                
                                                if (rowHasLote)
                                                {
                                                    loteFound = true;
                                                    matchingBodyRows.Add(rowDict);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (loteFound)
                {
                    matchingForms.Add(new {
                        form_id = form.FormID,
                        template_codigo = form.TemplateCodigo,
                        template_nombre = form.TemplateNombre,
                        proceso = form.TemplateProceso,
                        created_at = form.CreatedAt,
                        // La fecha que escribio el operario: es la que se muestra.
                        // `created_at` se conserva como sello de auditoria.
                        fecha_registro = form.FechaRegistro?.ToString("yyyy-MM-dd")
                            ?? FechaDelRegistro(form.HeaderData, form.CreatedAt),
                        filled_by = form.FilledBy ?? "N/A",
                        filled_by_role = form.FilledByRole ?? "",
                        tipo_producto = form.TipoProducto ?? "",
                        observaciones = form.Observaciones ?? "",
                        header = headerObj,
                        body_rows = matchingBodyRows
                    });
                }
            }

            return Ok(new {
                numeroLote = numeroLote,
                soloBorradores = false,
                pasos = matchingForms,
                mensaje = matchingForms.Any() ? "" : "No se encontraron formularios finalizados para este lote."
            });
        }
        
        private class LoteInfo
        {
            public int Count { get; set; }
            public string FirstDate { get; set; } = string.Empty;
            public string LastDate { get; set; } = string.Empty;
            public HashSet<string> Codes { get; set; } = new HashSet<string>();
            public HashSet<string> Productos { get; set; } = new HashSet<string>();
            public HashSet<string> Subproductos { get; set; } = new HashSet<string>();
        }
    }
}