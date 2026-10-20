using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;

namespace FormBuilder.API.Services
{
    // ── Lo que devuelve el cuadro ───────────────────────────────────────
    public class CeldaCumplimiento
    {
        public string Fecha { get; set; } = "";
        public bool SeEspera { get; set; }
        public bool Llenado { get; set; }
        public int Formularios { get; set; }
        public bool FirmadoPorTodos { get; set; }
        public List<int> Ids { get; set; } = new();
        public List<string> Personas { get; set; } = new();
    }

    public class RegistroCumplimiento
    {
        public int TemplateID { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Frecuencia { get; set; } = "";
        public string? Responsable { get; set; }
        /// <summary>
        /// Solo el PRIMER reemplazo: es el que responde si el titular no está.
        /// Los demás siguen en la plantilla y pueden firmar igual; simplemente
        /// no se listan como responsables, que era ilegible con 17 nombres.
        /// </summary>
        public string? ReemplazoPrincipal { get; set; }
        /// <summary>Todos los que pueden firmar, por si se necesita el detalle.</summary>
        public List<string> Reemplazos { get; set; } = new();
        /// <summary>Cuántos pueden firmar además del responsable y el primer reemplazo.</summary>
        public int OtrosQuePuedenFirmar { get; set; }
        /// <summary>Correos a los que se avisa: responsable y primer reemplazo.</summary>
        public List<string> Correos { get; set; } = new();
        public List<CeldaCumplimiento> Dias { get; set; } = new();
        public int DiasEsperados { get; set; }
        public int DiasCumplidos { get; set; }
        public List<string> Faltantes { get; set; } = new();
        public List<string> SinFirmar { get; set; } = new();
        public int Cumplimiento { get; set; }
    }

    public class DiaCuadro
    {
        public string Fecha { get; set; } = "";
        public string Etiqueta { get; set; } = "";
        public string DiaSemana { get; set; } = "";
        public bool EsFinDeSemana { get; set; }
    }

    public class CuadroCumplimiento
    {
        public string Desde { get; set; } = "";
        public string Hasta { get; set; } = "";
        public bool IncluirFinDeSemana { get; set; }
        public List<DiaCuadro> Dias { get; set; } = new();
        public List<RegistroCumplimiento> Registros { get; set; } = new();

        public int TotalFaltantes => Registros.Sum(r => r.Faltantes.Count);
        public int TotalSinFirmar => Registros.Sum(r => r.SinFirmar.Count);
    }

    /// <summary>
    /// 📋 Arma el cuadro de cumplimiento: por registro y por día, si se llenó y
    /// si está firmado por todos. Lo usan la pantalla (CumplimientoController)
    /// y el correo de resumen (ResumenCumplimientoService), para que los dos
    /// muestren exactamente lo mismo.
    /// </summary>
    public class CumplimientoService
    {
        public const int MaxDias = 62;

        private readonly ApplicationDbContext _context;

        public CumplimientoService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CuadroCumplimiento> CalcularAsync(
            DateTime desde, DateTime hasta,
            IEnumerable<string>? codigos = null,
            string? frecuencia = null,
            bool incluirFinDeSemana = true)
        {
            var ini = desde.Date;
            var fin = hasta.Date;
            if (ini > fin) (ini, fin) = (fin, ini);

            var filtroCodigos = (codigos ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();

            var plantillas = await _context.Templates.AsNoTracking()
                .Where(t => !t.IsObsolete && !t.IsDraft)
                .Where(t => filtroCodigos.Count == 0 || filtroCodigos.Contains(t.Codigo))
                .Where(t => frecuencia == null || t.Frecuencia == frecuencia)
                .OrderBy(t => t.Codigo)
                .ToListAsync();

            var ids = plantillas.Select(p => p.TemplateID).ToList();
            var finExclusivo = fin.AddDays(1);

            // Un formulario cuenta para el día de su FechaRegistro; si no tiene,
            // para el día en que se guardó.
            var formularios = await _context.FilledForms.AsNoTracking()
                .Where(f => ids.Contains(f.TemplateID))
                .Where(f => (f.FechaRegistro >= ini && f.FechaRegistro < finExclusivo)
                            || (f.CreatedAt >= ini && f.CreatedAt < finExclusivo))
                .Select(f => new { f.FormID, f.TemplateID, f.FechaRegistro, f.CreatedAt, f.FilledBy, f.FirmasData })
                .ToListAsync();

            // Correos del catálogo de firmas, para avisar al responsable.
            var catalogo = await _context.CatalogoFirmas.AsNoTracking()
                .Where(c => c.Activo && c.NombreCompleto != null && c.Correo != null)
                .Select(c => new { c.NombreCompleto, c.Correo })
                .ToListAsync();
            var correoDe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in catalogo)
                if (!string.IsNullOrWhiteSpace(c.NombreCompleto) && !string.IsNullOrWhiteSpace(c.Correo))
                    correoDe[c.NombreCompleto!.Trim()] = c.Correo!.Trim();

            var dias = new List<DateTime>();
            for (var d = ini; d <= fin; d = d.AddDays(1)) dias.Add(d);

            var cuadro = new CuadroCumplimiento
            {
                Desde = ini.ToString("yyyy-MM-dd"),
                Hasta = fin.ToString("yyyy-MM-dd"),
                IncluirFinDeSemana = incluirFinDeSemana,
                Dias = dias.Select(d => new DiaCuadro
                {
                    Fecha = d.ToString("yyyy-MM-dd"),
                    Etiqueta = d.ToString("dd/MM", CultureInfo.InvariantCulture),
                    DiaSemana = DiaEnEspanol(d.DayOfWeek),
                    EsFinDeSemana = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                }).ToList()
            };

            foreach (var t in plantillas)
            {
                var freq = (t.Frecuencia ?? "Diaria").Trim();
                var mios = formularios.Where(f => f.TemplateID == t.TemplateID)
                    .Select(f => new
                    {
                        f.FormID,
                        Dia = (f.FechaRegistro ?? f.CreatedAt).Date,
                        f.FilledBy,
                        Firmado = TodasLasFirmasListas(f.FirmasData)
                    })
                    .Where(f => f.Dia >= ini && f.Dia <= fin)
                    .ToList();

                var celdas = dias.Select(d =>
                {
                    var delDia = mios.Where(f => f.Dia == d).ToList();
                    return new CeldaCumplimiento
                    {
                        Fecha = d.ToString("yyyy-MM-dd"),
                        SeEspera = SeEspera(freq, d, fin, incluirFinDeSemana),
                        Llenado = delDia.Count > 0,
                        Formularios = delDia.Count,
                        FirmadoPorTodos = delDia.Count > 0 && delDia.All(f => f.Firmado),
                        Ids = delDia.Select(f => f.FormID).ToList(),
                        Personas = delDia.Select(f => f.FilledBy).Where(n => !string.IsNullOrWhiteSpace(n))
                                         .Select(n => n!).Distinct().ToList()
                    };
                }).ToList();

                var (responsable, reemplazos) = ResponsableDe(t.Firmas);
                // Responsables = titular + primer reemplazo. Los demás llenan y
                // firman igual, pero no se listan ni reciben el aviso.
                var reemplazoPrincipal = reemplazos.FirstOrDefault();
                var correos = new[] { responsable, reemplazoPrincipal }
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Select(n => correoDe.TryGetValue(n!.Trim(), out var c) ? c : null)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var esperados = celdas.Count(c => c.SeEspera);
                var cumplidos = celdas.Count(c => c.SeEspera && c.Llenado);

                cuadro.Registros.Add(new RegistroCumplimiento
                {
                    TemplateID = t.TemplateID,
                    Codigo = t.Codigo,
                    Nombre = t.Nombre,
                    Frecuencia = freq,
                    Responsable = responsable,
                    ReemplazoPrincipal = reemplazoPrincipal,
                    Reemplazos = reemplazos,
                    OtrosQuePuedenFirmar = Math.Max(0, reemplazos.Count - 1),
                    Correos = correos,
                    Dias = celdas,
                    DiasEsperados = esperados,
                    DiasCumplidos = cumplidos,
                    Faltantes = celdas.Where(c => c.SeEspera && !c.Llenado).Select(c => c.Fecha).ToList(),
                    SinFirmar = celdas.Where(c => c.Llenado && !c.FirmadoPorTodos).Select(c => c.Fecha).ToList(),
                    Cumplimiento = esperados == 0 ? 100 : (int)Math.Round(cumplidos * 100.0 / esperados)
                });
            }

            return cuadro;
        }

        // ── Reglas ──────────────────────────────────────────────────────

        /// <summary>
        /// ¿Se esperaba un registro ese día?
        ///   Diaria   : todos los días (sábado y domingo solo si se piden)
        ///   Semanal  : el domingo, o el último día del rango
        ///   Quincenal: el 15, el último del mes, o el último día del rango
        ///   Mensual  : el último del mes, o el último día del rango
        ///   Otras    : no se exige por día (Ocasional, trimestral, sin frecuencia)
        /// </summary>
        public static bool SeEspera(string frecuencia, DateTime dia, DateTime fin, bool finDeSemana)
        {
            switch ((frecuencia ?? "").Trim().ToLowerInvariant())
            {
                case "diaria":
                    if (!finDeSemana && (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday))
                        return false;
                    return true;
                case "semanal":
                    return dia.DayOfWeek == DayOfWeek.Sunday || dia == fin;
                case "quincenal":
                    return dia.Day == 15 || dia == UltimoDelMes(dia) || dia == fin;
                case "mensual":
                    return dia == UltimoDelMes(dia) || dia == fin;
                default:
                    return false;
            }
        }

        private static DateTime UltimoDelMes(DateTime d) =>
            new(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

        public static string DiaEnEspanol(DayOfWeek d) => d switch
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
        public static (string? Responsable, List<string> Reemplazos) ResponsableDe(string? firmasJson)
        {
            if (string.IsNullOrWhiteSpace(firmasJson)) return (null, new List<string>());
            try
            {
                using var doc = JsonDocument.Parse(firmasJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    return (null, new List<string>());

                var primera = doc.RootElement[0];
                static string? Texto(JsonElement o, string prop) =>
                    o.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                var nombre = Texto(primera, "nombreCompleto");
                var puesto = Texto(primera, "puesto");
                var responsable = string.IsNullOrWhiteSpace(nombre) ? puesto : nombre;

                var reemplazos = new List<string>();
                if (primera.TryGetProperty("reemplazos", out var r) && r.ValueKind == JsonValueKind.Array)
                    foreach (var x in r.EnumerateArray())
                        if (x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                            reemplazos.Add(x.GetString()!.Trim());

                return (responsable, reemplazos);
            }
            catch (JsonException)
            {
                return (null, new List<string>());
            }
        }

        /// <summary>
        /// ¿Están todas las firmas puestas? Misma regla que las alertas por
        /// correo (AlertBackgroundService.AllFirmasCompleted).
        /// </summary>
        public static bool TodasLasFirmasListas(string? firmasData)
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