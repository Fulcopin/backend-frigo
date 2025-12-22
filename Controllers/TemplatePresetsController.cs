using Microsoft.AspNetCore.Mvc;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TemplatePresetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TemplatePresetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// POST: api/TemplatePresets/create-15-tinas
        /// 15 tinas en FILAS VERTICALES - cada fila es una tina completa
        /// Diseño: HORA | TINA | PESO 1 | PESO 2 | PESO 3 | PESO 4 | PESO 5 | TOTAL
        /// </summary>
        [HttpPost("create-15-tinas")]
        public async Task<ActionResult<Template>> CreateTemplate15Tinas()
        {
            var tableColumns = new object[]
            {
                new { id = "col-hora", header = "⏰ HORA", type = "time", width = 100 },
                new { id = "col-tina", header = "🔵 TINA", type = "text", width = 80 },
                new { id = "col-peso1", header = "⚖️ PESO 1", type = "number", width = 100, unit = "kg" },
                new { id = "col-peso2", header = "⚖️ PESO 2", type = "number", width = 100, unit = "kg" },
                new { id = "col-peso3", header = "⚖️ PESO 3", type = "number", width = 100, unit = "kg" },
                new { id = "col-peso4", header = "⚖️ PESO 4", type = "number", width = 100, unit = "kg" },
                new { id = "col-peso5", header = "⚖️ PESO 5", type = "number", width = 100, unit = "kg" },
                new { id = "col-total", header = "📊 TOTAL", type = "calculated", width = 120, unit = "kg", formula = "sum(PESO1,PESO2,PESO3,PESO4,PESO5)", @readonly = true, bold = true }
            };

            var tableRows = new List<object>();
            
            // Crear 15 filas verticales (una por cada tina)
            for (int i = 1; i <= 15; i++)
            {
                var tinaNum = $"T{i}";
                tableRows.Add(new
                {
                    id = $"row-{tinaNum.ToLower()}",
                    cells = new object[]
                    {
                        new { columnId = "col-hora", name = $"HORA_{tinaNum}", value = "" },
                        new { columnId = "col-tina", name = $"TINA_{tinaNum}", value = tinaNum, @readonly = true },
                        new { columnId = "col-peso1", name = $"PESO1_{tinaNum}", value = 0.0, min = 0, step = 0.1 },
                        new { columnId = "col-peso2", name = $"PESO2_{tinaNum}", value = 0.0, min = 0, step = 0.1 },
                        new { columnId = "col-peso3", name = $"PESO3_{tinaNum}", value = 0.0, min = 0, step = 0.1 },
                        new { columnId = "col-peso4", name = $"PESO4_{tinaNum}", value = 0.0, min = 0, step = 0.1 },
                        new { columnId = "col-peso5", name = $"PESO5_{tinaNum}", value = 0.0, min = 0, step = 0.1 },
                        new { columnId = "col-total", name = $"TOTAL_{tinaNum}", formula = $"sum(PESO1_{tinaNum},PESO2_{tinaNum},PESO3_{tinaNum},PESO4_{tinaNum},PESO5_{tinaNum})", format = "0.00" }
                    }
                });
            }

            var bodyElements = new object[]
            {
                new
                {
                    type = "table",
                    id = "tabla-tinas-vertical",
                    title = "📋 Registro de 15 Tinas (Filas Verticales)",
                    columns = tableColumns,
                    rows = tableRows.ToArray(),
                    allowAddRow = false,
                    allowDeleteRow = false,
                    showRowNumbers = true
                },
                new
                {
                    type = "summary-section",
                    id = "total-general",
                    title = "🏆 TOTAL GENERAL",
                    calculation = new
                    {
                        type = "sum",
                        sources = new[] { "TOTAL_T1", "TOTAL_T2", "TOTAL_T3", "TOTAL_T4", "TOTAL_T5", "TOTAL_T6", "TOTAL_T7", "TOTAL_T8", "TOTAL_T9", "TOTAL_T10", "TOTAL_T11", "TOTAL_T12", "TOTAL_T13", "TOTAL_T14", "TOTAL_T15" },
                        format = "0.00",
                        unit = "kg"
                    }
                }
            };

            var template = new Template
            {
                Codigo = "FRM-TINAS-15-VERTICAL",
                Nombre = "Registro 15 Tinas (Filas Verticales)",
                Version = "10-00",
                Objetivo = "Registro de pesadas de 15 tinas en formato tabla vertical",
                Proceso = "Producción",
                CuandoSeUsa = "Control de producción con múltiples tinas",
                QuienLoLlena = "Asistente de Producción",
                HeaderFields = System.Text.Json.JsonSerializer.Serialize(new object[]
                {
                    new { label = "Fecha", type = "date", required = true },
                    new { label = "Turno", type = "select", options = new[] { "Mañana", "Tarde", "Noche" } },
                    new { label = "Responsable", type = "text", required = true },
                    new { label = "Lote", type = "text", required = true }
                }),
                BodyElements = System.Text.Json.JsonSerializer.Serialize(bodyElements),
                Firmas = System.Text.Json.JsonSerializer.Serialize(new object[]
                {
                    new { puesto = "ASISTENTE" },
                    new { puesto = "SUPERVISOR" },
                    new { puesto = "JEFE CALIDAD" }
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Templates.Add(template);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetTemplate", "Templates", new { id = template.TemplateID }, template);
        }

        [HttpGet("available")]
        public ActionResult<object[]> GetAvailablePresets()
        {
            return Ok(new[]
            {
                new
                {
                    id = "15-tinas-vertical",
                    codigo = "FRM-TINAS-15-VERTICAL",
                    nombre = "15 Tinas (Filas Verticales)",
                    descripcion = "Cada fila es una tina con hora y 5 pesos",
                    endpoint = "/api/TemplatePresets/create-15-tinas"
                }
            });
        }
    }
}
