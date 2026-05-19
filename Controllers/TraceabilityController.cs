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
        [HttpGet("lotes")]
        public async Task<ActionResult<object>> GetLotes([FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var dateFrom = from ?? DateTime.Now.AddDays(-60);
            var dateTo = to ?? DateTime.Now.AddDays(1);

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.CreatedAt >= dateFrom && f.CreatedAt < dateTo)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new {
                    f.FormID,
                    f.CreatedAt,
                    f.HeaderData,
                    f.BodyData,
                    TemplateCodigo = f.Template.Codigo,
                    f.TipoProducto
                })
                .ToListAsync();

            var lotesDict = new Dictionary<string, LoteInfo>();

            foreach (var form in forms)
            {
                var dateStr = form.CreatedAt.ToString("yyyy-MM-dd");
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
                .OrderBy(f => f.CreatedAt)
                .Select(f => new {
                    f.FormID,
                    f.TemplateID,
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
