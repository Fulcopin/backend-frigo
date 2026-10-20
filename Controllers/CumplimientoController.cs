using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// 📋 CONTROL DE CUMPLIMIENTO
    ///
    /// El cuadro que Calidad llevaba en Excel: por cada registro y por cada día,
    /// si se LLENÓ y si está FIRMADO POR TODOS, con el responsable y sus
    /// reemplazos. Sirve para auditoría y para ver de un vistazo qué falta.
    ///
    ///   GET api/Cumplimiento?desde=2026-09-14&hasta=2026-09-21
    ///       &codigo=CC-04            (opcional, uno o varios separados por coma)
    ///       &frecuencia=Diaria       (opcional)
    ///       &incluirFinDeSemana=true (por defecto true: el sábado también cuenta)
    ///
    /// Un día se considera CUMPLIDO si hay al menos un formulario de ese
    /// formato con esa fecha de registro. "Se esperaba" depende de la
    /// frecuencia de la plantilla: Diaria todos los días, Semanal una vez por
    /// semana, Mensual una vez al mes. Lo que no corresponde no se marca en rojo.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CumplimientoController : ControllerBase
    {
        private const int MaxDias = 62;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<CumplimientoController> _logger;

        public CumplimientoController(ApplicationDbContext context, ILogger<CumplimientoController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] string? codigo,
            [FromQuery] string? frecuencia,
            [FromQuery] bool incluirFinDeSemana = true)
        {
            var fin = (hasta ?? DateTime.Today).Date;
            var ini = (desde ?? fin.AddDays(-6)).Date;
            if (ini > fin) (ini, fin) = (fin, ini);
            if ((fin - ini).TotalDays + 1 > MaxDias)
                return BadRequest(new { message = $"El rango no puede pasar de {MaxDias} días." });

            var codigos = (codigo ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            try
            {
                var plantillas = await _context.Templates.AsNoTracking()
                    .Where(t => !t.IsObsolete && !t.IsDraft)
                    .Where(t => codigos.Count == 0 || codigos.Contains(t.Codigo))
                    .Where(t => frecuencia == null || t.Frecuencia == frecuencia)
                    .OrderBy(t => t.Codigo)
                    .ToListAsync();

                var ids = plantillas.Select(p => p.TemplateID).ToList();
                var finExclusivo = fin.AddDays(1);

                // Un formulario cuenta para el día de su FechaRegistro; si no
                // tiene, para el día en que se guardó.
                var formularios = await _context.FilledForms.AsNoTracking()
                    .Where(f => ids.Contains(f.TemplateID))
                    .Where(f => (f.FechaRegistro >= ini && f.FechaRegistro < finExclusivo)
                                || (f.CreatedAt >= ini && f.CreatedAt < finExclusivo))
                    .Select(f => new { f.FormID, f.TemplateID, f.FechaRegistro, f.CreatedAt, f.FilledBy, f.FirmasData })
                    .ToListAsync();

                var dias = new List<DateTime>();
                for (var d = ini; d <= fin; d = d.AddDays(1)) dias.Add(d);

                var filas = plantillas.Select(t =>
                {
                    var freq = (t.Frecuencia ?? "Diaria").Trim();
                    var mios = formularios.Where(f => f.TemplateID == t.TemplateID)
                        .Select(f => new
                        {
                            f.FormID,
                            // FechaRegistro puede venir vacía: entonces cuenta el día en que se guardó.
                            Dia = (f.FechaRegistro ?? f.CreatedAt).Date,
                            f.FilledBy,
                            Firmado = TodasLasFirmasListas(f.FirmasData)
                        })
                        .Where(f => f.Dia >= ini && f.Dia <= fin)
                        .ToList();

                    var celdas = dias.Select(d =>
                    {
                        var delDia = mios.Where(f => f.Dia == d).ToList();
                        return new
                        {
                            Fecha = d.ToString("yyyy-MM-dd"),
                            SeEspera = SeEspera(freq, d, ini, fin, incluirFinDeSemana),
                            Llenado = delDia.Count > 0,
                            Formularios = delDia.Count,
                            FirmadoPorTodos = delDia.Count > 0 && delDia.All(f => f.Firmado),
                            Ids = delDia.Select(f => f.FormID).ToList(),
                            Personas = delDia.Select(f => f.FilledBy).Where(n => !string.IsNullOrWhiteSpace(n))
                                             .Distinct().ToList()
                        };
                    }).ToList();

                    var esperados = celdas.Count(c => c.SeEspera);
                    var cumplidos = celdas.Count(c => c.SeEspera && c.Llenado);

                    var (responsable, reemplazos) = ResponsableDe(t.Firmas);

                    return new
                    {
                        t.TemplateID,
                        t.Codigo,
                        t.Nombre,
                        Frecuencia = freq,
                        Responsable = responsable,
                        Reemplazos = reemplazos,
                        Dias = celdas,
                        DiasEsperados = esperados,
                        DiasCumplidos = cumplidos,
                        Faltantes = celdas.Where(c => c.SeEspera && !c.Llenado).Select(c => c.Fecha).ToList(),
                        SinFirmar = celdas.Where(c => c.Llenado && !c.FirmadoPorTodos).Select(c => c.Fecha).ToList(),
                        Cumplimiento = esperados == 0 ? 100 : (int)Math.Round(cumplidos * 100.0 / esperados)
                    };
                }).ToList();

                return Ok(new
                {
                    Desde = ini.ToString("yyyy-MM-dd"),
                    Hasta = fin.ToString("yyyy-MM-dd"),
                    IncluirFinDeSemana = incluirFinDeSemana,
                    Dias = dias.Select(d => new
                    {
                        Fecha = d.ToString("yyyy-MM-dd"),
                        Etiqueta = d.ToString("dd/MM", CultureInfo.InvariantCulture),
                        DiaSemana = DiaEnEspanol(d.DayOfWeek),
                        EsFinDeSemana = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday
                    }),
                    Registros = filas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al armar el control de cumplimiento");
                return StatusCode(500, new { message = "No se pudo armar el cuadro.", detalle = ex.GetBaseException().Message });
            }
        }

        /// <summary>Códigos y frecuencias disponibles, para los filtros de la pantalla.</summary>
        [HttpGet("formatos")]
        public async Task<IActionResult> Formatos()
        {
            var lista = await _context.Templates.AsNoTracking()
                .Where(t => !t.IsObsolete && !t.IsDraft)
                .OrderBy(t => t.Codigo)
                .Select(t => new { t.TemplateID, t.Codigo, t.Nombre, t.Frecuencia })
                .ToListAsync();
            return Ok(lista);
        }

        // ── Reglas ──────────────────────────────────────────────────────

        /// <summary>
        /// ¿Se esperaba un registro ese día?
        ///   Diaria   : todos los días (el domingo solo si se pide incluir el fin de semana)
        ///   Semanal  : el último día de cada semana dentro del rango
        ///   Mensual  : el último día de cada mes dentro del rango
        ///   Otras    : no se marca nada en rojo (Ocasional, sin frecuencia…)
        /// </summary>
        private static bool SeEspera(string frecuencia, DateTime dia, DateTime ini, DateTime fin, bool finDeSemana)
        {
            switch (frecuencia.ToLowerInvariant())
            {
                case "diaria":
                    if (!finDeSemana && (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday))
                        return false;
                    return true;

                case "semanal":
                    // Último día de la semana (domingo) o el último día del rango.
                    return dia.DayOfWeek == DayOfWeek.Sunday || dia == fin;

                case "quincenal":
                    return dia.Day == 15 || dia == UltimoDelMes(dia) || dia == fin;

                case "mensual":
                    return dia == UltimoDelMes(dia) || dia == fin;

                default:
                    return false;   // Ocasional, trimestral, sin frecuencia: no se exige por día
            }
        }

        private static DateTime UltimoDelMes(DateTime d) =>
            new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

        private static string DiaEnEspanol(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday => "Lun",
            DayOfWeek.Tuesday => "Mar",
            DayOfWeek.Wednesday => "Mié",
            DayOfWeek.Thursday => "Jue",
            DayOfWeek.Friday => "Vie",
            DayOfWeek.Saturday => "Sáb",
            _ => "Dom"
        };

        /// <summary>Responsable principal y sus reemplazos, de las firmas de la plantilla.</summary>
        private static (string? Responsable, List<string> Reemplazos) ResponsableDe(string? firmasJson)
        {
            if (string.IsNullOrWhiteSpace(firmasJson)) return (null, new List<string>());
            try
            {
                using var doc = JsonDocument.Parse(firmasJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    return (null, new List<string>());

                var primera = doc.RootElement[0];
                string? Texto(JsonElement o, string prop) =>
                    o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                var nombre = Texto(primera, "nombreCompleto");
                var puesto = Texto(primera, "puesto");
                var responsable = string.IsNullOrWhiteSpace(nombre) ? puesto : nombre;

                var reemplazos = new List<string>();
                if (primera.TryGetProperty("reemplazos", out var r) && r.ValueKind == JsonValueKind.Array)
                {
                    foreach (var x in r.EnumerateArray())
                        if (x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                            reemplazos.Add(x.GetString()!.Trim());
                }
                return (responsable, reemplazos);
            }
            catch (JsonException)
            {
                return (null, new List<string>());
            }
        }

        /// <summary>
        /// ¿Están todas las firmas puestas? Misma regla que las alertas por
        /// correo (AlertBackgroundService.AllFirmasCompleted): cada puesto tiene
        /// que tener una firma con imagen.
        /// </summary>
        private static bool TodasLasFirmasListas(string? firmasData)
        {
            if (string.IsNullOrWhiteSpace(firmasData)) return false;
            try
            {
                using var doc = JsonDocument.Parse(firmasData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

                var alguna = false;
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    alguna = true;
                    if (p.Value.ValueKind != JsonValueKind.Object) return false;
                    if (!p.Value.TryGetProperty("firma", out var firma) || firma.ValueKind != JsonValueKind.Object)
                        return false;

                    var conUrl = firma.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String
                                 && !string.IsNullOrWhiteSpace(u.GetString());
                    var conBase64 = firma.TryGetProperty("base64", out var b) && b.ValueKind == JsonValueKind.String
                                    && !string.IsNullOrWhiteSpace(b.GetString());
                    if (!conUrl && !conBase64) return false;
                }
                return alguna;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
