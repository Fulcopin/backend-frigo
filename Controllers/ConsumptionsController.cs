using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using OfficeOpenXml;

namespace FormBuilder.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsumptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConsumptionsController> _logger;

        public ConsumptionsController(ApplicationDbContext context, ILogger<ConsumptionsController> logger)
        {
            _context = context;
            _logger = logger;
            
            // EPPlus 8+: usar la nueva API de licencia
            ExcelPackage.License.SetNonCommercialOrganization("Frigolab");
        }

        // GET /api/Consumptions/all-form-data — Retorna TODOS los datos de insumos extraidos de formularios
        [HttpGet("all-form-data")]
        public async Task<ActionResult> GetAllFormData(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(f => f.CreatedAt <= endDate.Value);

                var forms = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();

                var result = new List<object>();
                foreach (var form in forms)
                {
                    var allSections = ExtractAllSectionsFromForm(form);
                    if (allSections.Any())
                    {
                        result.Add(new
                        {
                            formID = form.FormID,
                            templateName = form.Template?.Nombre ?? "N/A",
                            area = form.Template?.Area ?? form.Template?.Proceso ?? "N/A",
                            filledBy = form.FilledBy ?? "N/A",
                            createdAt = form.CreatedAt,
                            totalSections = allSections.Count,
                            totalRows = allSections.Sum(s => ((dynamic)s).rows?.Count ?? 0),
                            sections = allSections
                        });
                    }
                }

                return Ok(new
                {
                    totalForms = result.Count,
                    totalSections = result.Sum(r => ((dynamic)r).totalSections),
                    totalRows = result.Sum(r => ((dynamic)r).totalRows),
                    forms = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener datos de formularios");
                return StatusCode(500, new { message = "Error al obtener datos de formularios" });
            }
        }

        // GET /api/Consumptions/all-sections-data — Retorna TODAS las secciones de TODOS los formularios con datos completos
        [HttpGet("all-sections-data")]
        public async Task<ActionResult> GetAllSectionsData(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? templateName,
            [FromQuery] string? area)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                if (endDate.HasValue)
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                if (!string.IsNullOrEmpty(templateName))
                    query = query.Where(f => f.Template != null && f.Template.Nombre.Contains(templateName));
                if (!string.IsNullOrEmpty(area))
                    query = query.Where(f => (f.Template != null && f.Template.Area != null && f.Template.Area.Contains(area)) || 
                                             (f.Template != null && f.Template.Proceso != null && f.Template.Proceso.Contains(area)));

                var forms = await query.OrderByDescending(f => f.CreatedAt).ToListAsync();

                var result = new List<object>();
                foreach (var form in forms)
                {
                    var allSections = ExtractAllSectionsFromForm(form);
                    if (allSections.Any())
                    {
                        result.Add(new
                        {
                            formID = form.FormID,
                            templateName = form.Template?.Nombre ?? "N/A",
                            templateCode = form.Template?.Codigo ?? "N/A",
                            area = form.Template?.Area ?? form.Template?.Proceso ?? "N/A",
                            filledBy = form.FilledBy ?? "N/A",
                            createdAt = form.CreatedAt,
                            totalSections = allSections.Count,
                            sections = allSections
                        });
                    }
                }

                // Agrupar por template para resumen
                var templateSummary = result
                    .GroupBy(r => ((dynamic)r).templateName)
                    .Select(g => new {
                        templateName = g.Key,
                        formCount = g.Count(),
                        totalSections = g.Sum(r => (int)((dynamic)r).totalSections)
                    })
                    .ToList();

                return Ok(new
                {
                    totalForms = result.Count,
                    templateSummary = templateSummary,
                    forms = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las secciones");
                return StatusCode(500, new { message = "Error al obtener todas las secciones" });
            }
        }

        // GET /api/Consumptions/consolidated
        [HttpGet("consolidated")]
        public async Task<ActionResult<IEnumerable<ConsolidatedConsumption>>> GetConsolidatedConsumptions(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? product,
            [FromQuery] string? area,
            [FromQuery] string groupBy = "product")
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                var forms = await query.ToListAsync();

                // Extraer consumos de todos los formularios
                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                // Filtrar por producto si se especifica
                if (!string.IsNullOrEmpty(product))
                {
                    allConsumptions = allConsumptions
                        .Where(c => c.ProductName.Contains(product, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Filtrar por área si se especifica
                if (!string.IsNullOrEmpty(area))
                {
                    allConsumptions = allConsumptions
                        .Where(c => c.Area.Contains(area, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // Agrupar según el criterio especificado
                var consolidated = allConsumptions
                    .GroupBy(c => c.ProductName)
                    .Select(g => new ConsolidatedConsumption
                    {
                        ProductName = g.Key,
                        TotalQuantity = g.Sum(c => c.Quantity),
                        Unit = g.First().Unit,
                        FormCount = g.Select(c => c.FormName).Distinct().Count(),
                        Areas = g.Select(c => c.Area).Distinct().ToList(),
                        LastDate = g.Max(c => c.Date)
                    })
                    .OrderByDescending(c => c.TotalQuantity)
                    .ToList();

                return Ok(consolidated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consumos consolidados");
                return StatusCode(500, new { message = "Error al obtener consumos" });
            }
        }

        // GET /api/Consumptions/by-product/{productName}
        [HttpGet("by-product/{productName}")]
        public async Task<ActionResult<IEnumerable<ConsumptionDetail>>> GetConsumptionsByProduct(
            string productName,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                var forms = await query.ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                var filteredConsumptions = allConsumptions
                    .Where(c => c.ProductName.Contains(productName, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.Date)
                    .ToList();

                return Ok(filteredConsumptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consumos por producto");
                return StatusCode(500, new { message = "Error al obtener consumos" });
            }
        }

        // GET /api/Consumptions/by-area/{area}
        [HttpGet("by-area/{area}")]
        public async Task<ActionResult<IEnumerable<ConsumptionDetail>>> GetConsumptionsByArea(
            string area,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                var forms = await query.ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                var filteredConsumptions = allConsumptions
                    .Where(c => c.Area.Contains(area, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.Date)
                    .ToList();

                return Ok(filteredConsumptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener consumos por área");
                return StatusCode(500, new { message = "Error al obtener consumos" });
            }
        }

        // GET /api/Consumptions/products
        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<ProductSummary>>> GetProducts()
        {
            try
            {
                var forms = await _context.FilledForms
                    .Include(f => f.Template)
                    .ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                var products = allConsumptions
                    .GroupBy(c => c.ProductName)
                    .Select(g => new ProductSummary
                    {
                        Name = g.Key,
                        TotalConsumption = g.Sum(c => c.Quantity),
                        Unit = g.First().Unit
                    })
                    .OrderBy(p => p.Name)
                    .ToList();

                return Ok(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener productos");
                return StatusCode(500, new { message = "Error al obtener productos" });
            }
        }

        // GET /api/Consumptions/areas
        [HttpGet("areas")]
        public async Task<ActionResult<IEnumerable<AreaSummary>>> GetAreas()
        {
            try
            {
                var forms = await _context.FilledForms
                    .Include(f => f.Template)
                    .ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                var areas = allConsumptions
                    .GroupBy(c => c.Area)
                    .Select(g => new AreaSummary
                    {
                        Name = g.Key,
                        ProductCount = g.Select(c => c.ProductName).Distinct().Count()
                    })
                    .OrderBy(a => a.Name)
                    .ToList();

                return Ok(areas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener áreas");
                return StatusCode(500, new { message = "Error al obtener áreas" });
            }
        }

        // GET /api/Consumptions/stats
        [HttpGet("stats")]
        public async Task<ActionResult<ConsumptionStats>> GetStats(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                var forms = await query.ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                var stats = new ConsumptionStats
                {
                    TotalProducts = allConsumptions.Select(c => c.ProductName).Distinct().Count(),
                    TotalConsumption = allConsumptions.Sum(c => c.Quantity),
                    TotalForms = forms.Count,
                    TotalAreas = allConsumptions.Select(c => c.Area).Distinct().Count()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas");
                return StatusCode(500, new { message = "Error al obtener estadísticas" });
            }
        }

        // GET /api/Consumptions/export/excel
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportToExcel(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] string? product,
            [FromQuery] string? area)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsQueryable();

                if (startDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(f => f.CreatedAt <= endDate.Value);
                }

                var forms = await query.ToListAsync();

                var allConsumptions = new List<ConsumptionDetail>();
                foreach (var form in forms)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    allConsumptions.AddRange(consumptions);
                }

                if (!string.IsNullOrEmpty(product))
                {
                    allConsumptions = allConsumptions
                        .Where(c => c.ProductName.Contains(product, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (!string.IsNullOrEmpty(area))
                {
                    allConsumptions = allConsumptions
                        .Where(c => c.Area.Contains(area, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                var consolidated = allConsumptions
                    .GroupBy(c => c.ProductName)
                    .Select(g => new ConsolidatedConsumption
                    {
                        ProductName = g.Key,
                        TotalQuantity = g.Sum(c => c.Quantity),
                        Unit = g.First().Unit,
                        FormCount = g.Select(c => c.FormName).Distinct().Count(),
                        Areas = g.Select(c => c.Area).Distinct().ToList()
                    })
                    .OrderByDescending(c => c.TotalQuantity)
                    .ToList();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Consumos");

                    // Headers
                    worksheet.Cells[1, 1].Value = "Producto";
                    worksheet.Cells[1, 2].Value = "Cantidad Total";
                    worksheet.Cells[1, 3].Value = "Unidad";
                    worksheet.Cells[1, 4].Value = "Registros";
                    worksheet.Cells[1, 5].Value = "Áreas";

                    // Estilos para headers
                    using (var range = worksheet.Cells[1, 1, 1, 5])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    }

                    // Data
                    int row = 2;
                    foreach (var item in consolidated)
                    {
                        worksheet.Cells[row, 1].Value = item.ProductName;
                        worksheet.Cells[row, 2].Value = item.TotalQuantity;
                        worksheet.Cells[row, 3].Value = item.Unit;
                        worksheet.Cells[row, 4].Value = item.FormCount;
                        worksheet.Cells[row, 5].Value = string.Join(", ", item.Areas);
                        row++;
                    }

                    // Autoajustar columnas
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    var excelData = package.GetAsByteArray();
                    return File(excelData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"consumos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar a Excel");
                return StatusCode(500, new { message = "Error al exportar a Excel" });
            }
        }

        // POST /api/Consumptions/compare
        [HttpPost("compare")]
        public async Task<ActionResult<PeriodComparison>> ComparePeriods([FromBody] ComparePeriodRequest request)
        {
            try
            {
                // Obtener consumos del período 1
                var forms1 = await _context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt >= request.Period1.Start && 
                               f.CreatedAt <= request.Period1.End)
                    .ToListAsync();

                var consumptions1 = new List<ConsumptionDetail>();
                foreach (var form in forms1)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    consumptions1.AddRange(consumptions);
                }

                // Obtener consumos del período 2
                var forms2 = await _context.FilledForms
                    .Include(f => f.Template)
                    .Where(f => f.CreatedAt >= request.Period2.Start && 
                               f.CreatedAt <= request.Period2.End)
                    .ToListAsync();

                var consumptions2 = new List<ConsumptionDetail>();
                foreach (var form in forms2)
                {
                    var consumptions = ExtractConsumptionsFromForm(form);
                    consumptions2.AddRange(consumptions);
                }

                var period1Total = consumptions1.Sum(c => c.Quantity);
                var period2Total = consumptions2.Sum(c => c.Quantity);
                var difference = period2Total - period1Total;
                var percentageChange = period1Total > 0 
                    ? (difference / period1Total) * 100 
                    : 0;

                // Comparar por producto
                var products1 = consumptions1
                    .GroupBy(c => c.ProductName)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity));

                var products2 = consumptions2
                    .GroupBy(c => c.ProductName)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity));

                var allProducts = products1.Keys.Union(products2.Keys).ToList();

                var productComparisons = allProducts.Select(product =>
                {
                    var p1 = products1.ContainsKey(product) ? products1[product] : 0;
                    var p2 = products2.ContainsKey(product) ? products2[product] : 0;
                    var diff = p2 - p1;
                    var pct = p1 > 0 ? (diff / p1) * 100 : 0;

                    return new ProductComparison
                    {
                        Name = product,
                        Period1 = p1,
                        Period2 = p2,
                        Difference = diff,
                        PercentageChange = pct
                    };
                }).ToList();

                var comparison = new PeriodComparison
                {
                    Period1Total = period1Total,
                    Period2Total = period2Total,
                    Difference = difference,
                    PercentageChange = percentageChange,
                    Products = productComparisons
                };

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comparar períodos");
                return StatusCode(500, new { message = "Error al comparar períodos" });
            }
        }

        // ===== MÉTODOS PRIVADOS =====

        // POST /api/Consumptions/export-sections/excel — Exportar secciones seleccionadas a Excel
        [HttpPost("export-sections/excel")]
        public ActionResult ExportSelectedSectionsToExcel([FromBody] ExportSectionsRequest request)
        {
            try
            {
                if (request == null || request.Sections == null || !request.Sections.Any())
                {
                    return BadRequest(new { message = "No se enviaron secciones para exportar" });
                }

                using (var package = new ExcelPackage())
                {
                    foreach (var section in request.Sections)
                    {
                        // Nombre de la hoja: Plantilla - Sección (máx 31 chars para Excel)
                        var sheetName = $"{section.TemplateName ?? "Form"} - {section.SectionTitle ?? "Sección"}";
                        if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
                        // Reemplazar caracteres inválidos para nombre de hoja Excel
                        sheetName = sheetName.Replace("/", "-").Replace("\\", "-").Replace("?", "").Replace("*", "").Replace("[", "").Replace("]", "");
                        
                        // Si ya existe una hoja con ese nombre, agregar un sufijo
                        var baseName = sheetName;
                        int suffix = 2;
                        while (package.Workbook.Worksheets.Any(ws => ws.Name == sheetName))
                        {
                            var maxLen = 31 - suffix.ToString().Length - 1;
                            sheetName = (baseName.Length > maxLen ? baseName.Substring(0, maxLen) : baseName) + "_" + suffix;
                            suffix++;
                        }

                        var worksheet = package.Workbook.Worksheets.Add(sheetName);

                        // Fila 1: Info del formulario
                        worksheet.Cells[1, 1].Value = "Plantilla:";
                        worksheet.Cells[1, 1].Style.Font.Bold = true;
                        worksheet.Cells[1, 2].Value = section.TemplateName ?? "N/A";
                        worksheet.Cells[1, 3].Value = "Área:";
                        worksheet.Cells[1, 3].Style.Font.Bold = true;
                        worksheet.Cells[1, 4].Value = section.Area ?? "N/A";

                        worksheet.Cells[2, 1].Value = "Llenado por:";
                        worksheet.Cells[2, 1].Style.Font.Bold = true;
                        worksheet.Cells[2, 2].Value = section.FilledBy ?? "N/A";
                        worksheet.Cells[2, 3].Value = "Fecha:";
                        worksheet.Cells[2, 3].Style.Font.Bold = true;
                        worksheet.Cells[2, 4].Value = section.CreatedAt ?? "N/A";

                        worksheet.Cells[3, 1].Value = "Sección:";
                        worksheet.Cells[3, 1].Style.Font.Bold = true;
                        worksheet.Cells[3, 2].Value = section.SectionTitle ?? "N/A";
                        worksheet.Cells[3, 3].Value = "Tipo:";
                        worksheet.Cells[3, 3].Style.Font.Bold = true;
                        worksheet.Cells[3, 4].Value = section.SectionType ?? "N/A";

                        // Fila 5: Headers de columnas
                        int headerRow = 5;
                        var columns = section.Columns ?? new List<string>();
                        for (int i = 0; i < columns.Count; i++)
                        {
                            worksheet.Cells[headerRow, i + 1].Value = columns[i];
                            worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                            worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(219, 234, 254)); // blue-100
                        }

                        // Filas de datos
                        var rows = section.Rows ?? new List<Dictionary<string, object>>();
                        int dataRow = headerRow + 1;
                        foreach (var row in rows)
                        {
                            for (int colIdx = 0; colIdx < columns.Count; colIdx++)
                            {
                                var colName = columns[colIdx];
                                object cellValue = null;

                                // Buscar el valor: directamente, case-insensitive, o por posición
                                if (row.ContainsKey(colName))
                                {
                                    cellValue = row[colName];
                                }
                                else
                                {
                                    var matchKey = row.Keys.FirstOrDefault(k =>
                                        k.Equals(colName, StringComparison.OrdinalIgnoreCase) ||
                                        k.Contains(colName, StringComparison.OrdinalIgnoreCase) ||
                                        colName.Contains(k, StringComparison.OrdinalIgnoreCase));
                                    if (matchKey != null)
                                        cellValue = row[matchKey];
                                    else if (colIdx < row.Keys.Count)
                                        cellValue = row[row.Keys.ElementAt(colIdx)];
                                }

                                if (cellValue != null)
                                {
                                    // Extraer valor real de JsonElement si es necesario
                                    string strVal;
                                    if (cellValue is System.Text.Json.JsonElement je)
                                    {
                                        strVal = je.ValueKind switch
                                        {
                                            System.Text.Json.JsonValueKind.Number => je.GetRawText(),
                                            System.Text.Json.JsonValueKind.String => je.GetString() ?? "",
                                            System.Text.Json.JsonValueKind.True => "true",
                                            System.Text.Json.JsonValueKind.False => "false",
                                            System.Text.Json.JsonValueKind.Null => "",
                                            _ => je.GetRawText()
                                        };
                                    }
                                    else
                                    {
                                        strVal = cellValue.ToString() ?? "";
                                    }

                                    if (decimal.TryParse(strVal, System.Globalization.NumberStyles.Any, 
                                        System.Globalization.CultureInfo.InvariantCulture, out decimal numVal))
                                    {
                                        worksheet.Cells[dataRow, colIdx + 1].Value = numVal;
                                        worksheet.Cells[dataRow, colIdx + 1].Style.Numberformat.Format = "#,##0.##";
                                    }
                                    else
                                    {
                                        worksheet.Cells[dataRow, colIdx + 1].Value = strVal;
                                    }
                                }
                            }
                            dataRow++;
                        }

                        // Autoajustar columnas
                        if (columns.Count > 0 && rows.Count > 0)
                        {
                            worksheet.Cells[headerRow, 1, dataRow - 1, columns.Count].AutoFitColumns();
                        }
                    }

                    var excelData = package.GetAsByteArray();
                    return File(excelData, 
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"secciones_seleccionadas_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al exportar secciones seleccionadas a Excel");
                return StatusCode(500, new { message = "Error al exportar secciones a Excel" });
            }
        }

        private List<ConsumptionDetail> ExtractConsumptionsFromForm(FilledForm form)
        {
            var consumptions = new List<ConsumptionDetail>();

            try
            {
                if (string.IsNullOrEmpty(form.BodyData))
                {
                    return consumptions;
                }

                var formData = JsonSerializer.Deserialize<JsonElement>(form.BodyData);

                // bodyData es un array de tablas: [{data: [{col: val, ...}, ...]}, ...]
                if (formData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tableElement in formData.EnumerateArray())
                    {
                        // Cada tabla tiene una propiedad "data" que es un array de filas
                        if (tableElement.TryGetProperty("data", out JsonElement dataArray) &&
                            dataArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var row in dataArray.EnumerateArray())
                            {
                                if (row.ValueKind != JsonValueKind.Object) continue;

                                var productName = ExtractProductName(row);
                                var quantity = ExtractQuantity(row);

                                if (!string.IsNullOrEmpty(productName) && quantity > 0)
                                {
                                    consumptions.Add(new ConsumptionDetail
                                    {
                                        Date = form.CreatedAt,
                                        FormName = form.Template?.Nombre ?? "N/A",
                                        Area = form.Template?.Area ?? form.Template?.Proceso ?? "N/A",
                                        ProductName = productName,
                                        Quantity = quantity,
                                        Unit = ExtractUnit(row) ?? "unidad",
                                        BatchCode = ExtractBatchCode(row),
                                        Responsible = form.FilledBy ?? "N/A"
                                    });
                                }
                            }
                        }

                        // Tambien intentar como objeto directo (fila unica)
                        if (tableElement.ValueKind == JsonValueKind.Object && 
                            !tableElement.TryGetProperty("data", out _))
                        {
                            var productName = ExtractProductName(tableElement);
                            var quantity = ExtractQuantity(tableElement);

                            if (!string.IsNullOrEmpty(productName) && quantity > 0)
                            {
                                consumptions.Add(new ConsumptionDetail
                                {
                                    Date = form.CreatedAt,
                                    FormName = form.Template?.Nombre ?? "N/A",
                                    Area = form.Template?.Area ?? form.Template?.Proceso ?? "N/A",
                                    ProductName = productName,
                                    Quantity = quantity,
                                    Unit = ExtractUnit(tableElement) ?? "unidad",
                                    BatchCode = ExtractBatchCode(tableElement),
                                    Responsible = form.FilledBy ?? "N/A"
                                });
                            }
                        }
                    }
                }

                // Fallback: Intentar extraer de "sections" (formato legacy)
                if (formData.ValueKind == JsonValueKind.Object &&
                    formData.TryGetProperty("sections", out JsonElement sections) && 
                    sections.ValueKind == JsonValueKind.Array)
                {
                    foreach (var section in sections.EnumerateArray())
                    {
                        if (section.TryGetProperty("rows", out JsonElement rows) && 
                            rows.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var row in rows.EnumerateArray())
                            {
                                var productName = ExtractProductName(row);
                                var quantity = ExtractQuantity(row);

                                if (!string.IsNullOrEmpty(productName) && quantity > 0)
                                {
                                    consumptions.Add(new ConsumptionDetail
                                    {
                                        Date = form.CreatedAt,
                                        FormName = form.Template?.Nombre ?? "N/A",
                                        Area = form.Template?.Area ?? form.Template?.Proceso ?? "N/A",
                                        ProductName = productName,
                                        Quantity = quantity,
                                        Unit = ExtractUnit(row) ?? "unidad",
                                        BatchCode = ExtractBatchCode(row),
                                        Responsible = form.FilledBy ?? "N/A"
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer consumos del formulario {FormId}", form.FormID);
            }

            return consumptions;
        }

        private string? ExtractProductName(JsonElement row)
        {
            var productKeys = new[] { "producto", "insumo", "material", "item", "descripcion", "nombre", "materia prima", "suministro", "ingrediente" };

            // Busqueda case-insensitive sobre las propiedades del row
            foreach (var property in row.EnumerateObject())
            {
                var propNameLower = property.Name.ToLower().Trim();
                foreach (var key in productKeys)
                {
                    if (propNameLower.Contains(key) && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var str = property.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            return str;
                        }
                    }
                }
            }

            return null;
        }

        private decimal ExtractQuantity(JsonElement row)
        {
            var quantityKeys = new[] { "cantidad", "peso", "kg", "litros", "unidades", "total", "volumen", "consumo", "kilos", "gramos" };

            foreach (var property in row.EnumerateObject())
            {
                var propNameLower = property.Name.ToLower().Trim();
                foreach (var key in quantityKeys)
                {
                    if (propNameLower.Contains(key))
                    {
                        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out decimal decimalValue))
                        {
                            return decimalValue;
                        }
                        else if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            var str = property.Value.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(str) && decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal parsedValue))
                            {
                                return parsedValue;
                            }
                        }
                    }
                }
            }

            return 0;
        }

        private string? ExtractUnit(JsonElement row)
        {
            var unitKeys = new[] { "unidad", "unit", "medida" };

            foreach (var key in unitKeys)
            {
                if (row.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            // Intentar inferir la unidad de los nombres de columnas
            foreach (var property in row.EnumerateObject())
            {
                var propName = property.Name.ToLower();
                if (propName.Contains("kg"))
                {
                    return "kg";
                }
                else if (propName.Contains("litro"))
                {
                    return "litros";
                }
                else if (propName.Contains("unidad"))
                {
                    return "unidades";
                }
            }

            return "unidad";
        }

        private string? ExtractBatchCode(JsonElement row)
        {
            var batchKeys = new[] { "lote", "batch", "codigo_lote", "codigoLote" };

            foreach (var key in batchKeys)
            {
                if (row.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }

        // ===== NUEVO: Extraer TODAS las secciones de un formulario =====
        private List<object> ExtractAllSectionsFromForm(FilledForm form)
        {
            var sections = new List<object>();

            try
            {
                if (string.IsNullOrEmpty(form.BodyData))
                    return sections;

                var formData = JsonSerializer.Deserialize<JsonElement>(form.BodyData);

                // Obtener los bodyElements del template para saber los títulos/tipos de cada sección
                List<JsonElement>? templateElements = null;
                if (form.Template != null && !string.IsNullOrEmpty(form.Template.BodyElements))
                {
                    try
                    {
                        var tplElements = JsonSerializer.Deserialize<JsonElement>(form.Template.BodyElements);
                        if (tplElements.ValueKind == JsonValueKind.Array)
                        {
                            templateElements = new List<JsonElement>();
                            foreach (var el in tplElements.EnumerateArray())
                                templateElements.Add(el);
                        }
                    }
                    catch { /* ignorar si no se puede parsear */ }
                }

                // bodyData es un array: [{id, type, data}, ...]
                if (formData.ValueKind == JsonValueKind.Array)
                {
                    int sectionIndex = 0;
                    foreach (var element in formData.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.Object)
                        {
                            sectionIndex++;
                            continue;
                        }

                        // Obtener tipo del elemento
                        string elementType = "unknown";
                        if (element.TryGetProperty("type", out JsonElement typeEl) && typeEl.ValueKind == JsonValueKind.String)
                            elementType = typeEl.GetString() ?? "unknown";

                        // Obtener id del elemento
                        string elementId = "";
                        if (element.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.String)
                            elementId = idEl.GetString() ?? "";

                        // Obtener título desde el template
                        string sectionTitle = elementId;
                        List<string> columnNames = new();
                        if (templateElements != null && sectionIndex < templateElements.Count)
                        {
                            var tplElement = templateElements[sectionIndex];
                            if (tplElement.TryGetProperty("title", out JsonElement titleEl) && titleEl.ValueKind == JsonValueKind.String)
                                sectionTitle = titleEl.GetString() ?? elementId;

                            // Obtener nombres de columnas del template
                            if (tplElement.TryGetProperty("columns", out JsonElement colsEl) && colsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var col in colsEl.EnumerateArray())
                                {
                                    string colName = "";
                                    if (col.TryGetProperty("label", out JsonElement lblEl) && lblEl.ValueKind == JsonValueKind.String)
                                        colName = lblEl.GetString() ?? "";
                                    else if (col.TryGetProperty("name", out JsonElement nmEl) && nmEl.ValueKind == JsonValueKind.String)
                                        colName = nmEl.GetString() ?? "";
                                    else if (col.TryGetProperty("header", out JsonElement hdEl) && hdEl.ValueKind == JsonValueKind.String)
                                        colName = hdEl.GetString() ?? "";
                                    else if (col.TryGetProperty("id", out JsonElement cidEl) && cidEl.ValueKind == JsonValueKind.String)
                                        colName = cidEl.GetString() ?? "";

                                    if (!string.IsNullOrEmpty(colName))
                                        columnNames.Add(colName);
                                }
                            }
                        }

                        if (elementType == "table")
                        {
                            // Extraer filas de la tabla
                            var rows = new List<Dictionary<string, object>>();
                            JsonElement dataArray;

                            bool hasData = element.TryGetProperty("data", out dataArray) && dataArray.ValueKind == JsonValueKind.Array;

                            if (hasData)
                            {
                                foreach (var row in dataArray.EnumerateArray())
                                {
                                    if (row.ValueKind != JsonValueKind.Object) continue;

                                    var rowDict = new Dictionary<string, object>();
                                    bool hasAnyValue = false;

                                    foreach (var prop in row.EnumerateObject())
                                    {
                                        var val = ExtractValueAsObject(prop.Value);
                                        rowDict[prop.Name] = val;
                                        // Verificar si la fila tiene algún valor no vacío
                                        if (val != null && val.ToString() != "" && val.ToString() != "0")
                                            hasAnyValue = true;
                                    }

                                    if (hasAnyValue)
                                        rows.Add(rowDict);
                                }
                            }

                            if (rows.Any())
                            {
                                // Detectar las columnas reales de los datos
                                var detectedColumns = rows.SelectMany(r => r.Keys).Distinct().ToList();

                                sections.Add(new
                                {
                                    sectionIndex = sectionIndex,
                                    sectionTitle = sectionTitle,
                                    sectionType = "table",
                                    columns = columnNames.Any() ? columnNames : detectedColumns,
                                    rows = rows,
                                    rowCount = rows.Count
                                });
                            }
                        }
                        else if (elementType == "section")
                        {
                            // Sección con campos individuales
                            if (element.TryGetProperty("data", out JsonElement sectionData) && sectionData.ValueKind == JsonValueKind.Object)
                            {
                                var fields = new Dictionary<string, object>();
                                bool hasAnyValue = false;

                                foreach (var prop in sectionData.EnumerateObject())
                                {
                                    var val = ExtractValueAsObject(prop.Value);
                                    fields[prop.Name] = val;
                                    if (val != null && val.ToString() != "")
                                        hasAnyValue = true;
                                }

                                if (hasAnyValue)
                                {
                                    // Convertir los campos a una fila para uniformidad
                                    sections.Add(new
                                    {
                                        sectionIndex = sectionIndex,
                                        sectionTitle = sectionTitle,
                                        sectionType = "section",
                                        columns = fields.Keys.ToList(),
                                        rows = new List<Dictionary<string, object>> { fields },
                                        rowCount = 1
                                    });
                                }
                            }
                        }
                        else if (elementType == "observaciones")
                        {
                            // Observaciones (texto libre)
                            if (element.TryGetProperty("data", out JsonElement obsData) && obsData.ValueKind == JsonValueKind.Object)
                            {
                                if (obsData.TryGetProperty("texto", out JsonElement textoEl) && textoEl.ValueKind == JsonValueKind.String)
                                {
                                    var texto = textoEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(texto))
                                    {
                                        var fields = new Dictionary<string, object> { { "Observaciones", texto! } };
                                        sections.Add(new
                                        {
                                            sectionIndex = sectionIndex,
                                            sectionTitle = sectionTitle.Contains("observ", StringComparison.OrdinalIgnoreCase) ? sectionTitle : "Observaciones",
                                            sectionType = "observaciones",
                                            columns = new List<string> { "Observaciones" },
                                            rows = new List<Dictionary<string, object>> { fields },
                                            rowCount = 1
                                        });
                                    }
                                }
                            }
                        }

                        sectionIndex++;
                    }
                }

                // Fallback: Si bodyData es un objeto con propiedad "sections" (formato legacy)
                if (formData.ValueKind == JsonValueKind.Object &&
                    formData.TryGetProperty("sections", out JsonElement legacySections) &&
                    legacySections.ValueKind == JsonValueKind.Array)
                {
                    int idx = 0;
                    foreach (var section in legacySections.EnumerateArray())
                    {
                        if (section.TryGetProperty("rows", out JsonElement legacyRows) && legacyRows.ValueKind == JsonValueKind.Array)
                        {
                            var rows = new List<Dictionary<string, object>>();
                            foreach (var row in legacyRows.EnumerateArray())
                            {
                                if (row.ValueKind != JsonValueKind.Object) continue;
                                var rowDict = new Dictionary<string, object>();
                                foreach (var prop in row.EnumerateObject())
                                    rowDict[prop.Name] = ExtractValueAsObject(prop.Value);
                                rows.Add(rowDict);
                            }

                            if (rows.Any())
                            {
                                string title = "Sección " + (idx + 1);
                                if (section.TryGetProperty("title", out JsonElement tEl) && tEl.ValueKind == JsonValueKind.String)
                                    title = tEl.GetString() ?? title;

                                sections.Add(new
                                {
                                    sectionIndex = idx,
                                    sectionTitle = title,
                                    sectionType = "table",
                                    columns = rows.SelectMany(r => r.Keys).Distinct().ToList(),
                                    rows = rows,
                                    rowCount = rows.Count
                                });
                            }
                        }
                        idx++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extraer todas las secciones del formulario {FormId}", form.FormID);
            }

            return sections;
        }

        private object ExtractValueAsObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? "";
                case JsonValueKind.Number:
                    if (element.TryGetDecimal(out decimal decVal))
                        return decVal;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return "";
                default:
                    return element.ToString();
            }
        }
    }
}
