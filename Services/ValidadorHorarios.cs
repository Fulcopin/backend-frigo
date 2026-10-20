using System.Text.Json;
using System.Text.RegularExpressions;
using FormBuilder.API.Models;
using Microsoft.Extensions.Options;

namespace FormBuilder.API.Services
{
    /// <summary>
    /// Reglas configurables (appsettings.json → "ReglasHorario").
    /// </summary>
    public sealed class ReglasHorarioOptions
    {
        /// <summary>
        /// true: un fin menor que el inicio se acepta como turno que termina al
        /// día siguiente (21:00 → 01:30), siempre que no supere
        /// MaxHorasCruceMedianoche. false: cualquier resta negativa se bloquea.
        /// </summary>
        public bool PermitirCruceMedianoche { get; set; } = true;

        /// <summary>
        /// Duración máxima de un turno que cruza la medianoche. Con 12:
        /// 17:30 → 02:00 (8,5 h) pasa; 08:00 → 04:00 (20 h) se bloquea.
        /// </summary>
        public double MaxHorasCruceMedianoche { get; set; } = 12;
    }

    /// <summary>Un renglón con horario inválido, con lo necesario para ubicarlo.</summary>
    public sealed record ErrorHorario(string Tabla, int Fila, string HoraInicio, string HoraFin, string Motivo);

    public sealed class ResultadoValidacionHorario
    {
        public List<ErrorHorario> Errores { get; } = new();
        public bool EsValido => Errores.Count == 0;

        /// <summary>Texto para mostrar tal cual al usuario.</summary>
        public string MensajeUsuario =>
            EsValido
                ? string.Empty
                : "⏱️ Revisa bien la hora de inicio y la hora de fin. No se guardó el formulario.\n"
                  + string.Join("\n", Errores.Take(10).Select(e =>
                        $"• {e.Tabla}, fila {e.Fila}: {e.HoraInicio} → {e.HoraFin}. {e.Motivo}"))
                  + (Errores.Count > 10 ? $"\n… y {Errores.Count - 10} fila(s) más." : "");
    }

    public interface IValidadorHorarios
    {
        /// <summary>Valida un rango suelto (inicio/fin).</summary>
        string? ValidarRango(TimeSpan inicio, TimeSpan fin);

        /// <summary>Valida todas las filas con hora de inicio y fin del BodyData.</summary>
        ResultadoValidacionHorario ValidarFormulario(string? bodyData, Template? template);
    }

    /// <summary>
    /// Regla de negocio de horarios, en UN solo lugar.
    ///
    /// No toca la base ni sabe de HTTP: recibe datos y devuelve errores. El
    /// controlador decide qué responder. Así la misma regla sirve para crear,
    /// editar y cualquier endpoint futuro, y se puede probar sin levantar la API.
    /// </summary>
    public sealed class ValidadorHorarios : IValidadorHorarios
    {
        // Mismos nombres de columna que usa el reporte de Personal
        // (PersonalController.RxHoraInicio / RxHoraFin), más las variantes
        // "HORA DE INICIO" y "HORA ENTRADA/SALIDA".
        private static readonly Regex RxHoraInicio = new(
            @"HORA\s*(DE\s*)?INICI|HORA\s*(DE\s*)?ENTRADA",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxHoraFin = new(
            @"HORA\s*(DE\s*)?FIN|HORA\s*(DE\s*)?SALIDA|HORA\s*(DE\s*)?T[EÉ]RMINO",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // FillForm guarda las columnas repetidas como "HORA INICIO_col3".
        private static readonly Regex RxSufijoColumna = new(@"_col\d+$", RegexOptions.Compiled);

        private readonly ReglasHorarioOptions _reglas;

        public ValidadorHorarios(IOptions<ReglasHorarioOptions> reglas)
        {
            _reglas = reglas.Value;
        }

        public string? ValidarRango(TimeSpan inicio, TimeSpan fin)
        {
            var diferencia = fin - inicio;

            if (diferencia == TimeSpan.Zero)
                return "La hora de fin es igual a la de inicio.";

            if (diferencia > TimeSpan.Zero)
                return null;

            // Resta negativa.
            if (!_reglas.PermitirCruceMedianoche)
                return "La hora de fin es anterior a la de inicio.";

            var duracion = diferencia + TimeSpan.FromHours(24);
            if (duracion.TotalHours > _reglas.MaxHorasCruceMedianoche)
                return $"La hora de fin es anterior a la de inicio. Si fuera un turno que pasa la medianoche "
                     + $"duraría {duracion.TotalHours:0.#} h, y el máximo permitido es "
                     + $"{_reglas.MaxHorasCruceMedianoche:0.#} h.";

            return null;   // turno nocturno válido (ej. 21:00 → 01:30)
        }

        public ResultadoValidacionHorario ValidarFormulario(string? bodyData, Template? template)
        {
            var resultado = new ResultadoValidacionHorario();
            if (string.IsNullOrWhiteSpace(bodyData)) return resultado;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(bodyData); }
            catch (JsonException) { return resultado; }   // el formato lo valida otra capa

            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return resultado;

                var (titulos, roles) = LeerPlantilla(template);
                var indiceTabla = 0;

                foreach (var bloque in doc.RootElement.EnumerateArray())
                {
                    if (bloque.ValueKind != JsonValueKind.Object) continue;

                    // Las tablas se guardan como "data" (FillForm) o "rows"
                    // (Editar Formulario Llenado).
                    if (!TryFilas(bloque, out var filas)) continue;
                    indiceTabla++;

                    var tabla = NombreTabla(bloque, titulos, indiceTabla);
                    var numeroFila = 0;

                    foreach (var fila in filas.EnumerateArray())
                    {
                        numeroFila++;
                        if (fila.ValueKind != JsonValueKind.Object) continue;

                        string? textoInicio = null, textoFin = null;
                        foreach (var celda in fila.EnumerateObject())
                        {
                            var tipo = TipoColumna(celda.Name, roles);
                            if (tipo == TipoHora.Inicio && textoInicio == null) textoInicio = Texto(celda.Value);
                            else if (tipo == TipoHora.Fin && textoFin == null) textoFin = Texto(celda.Value);
                        }

                        // Fila incompleta: no hay rango que validar.
                        if (!TryHora(textoInicio, out var inicio) || !TryHora(textoFin, out var fin))
                            continue;

                        var motivo = ValidarRango(inicio, fin);
                        if (motivo != null)
                            resultado.Errores.Add(new ErrorHorario(
                                tabla, numeroFila, textoInicio!.Trim(), textoFin!.Trim(), motivo));
                    }
                }
            }

            return resultado;
        }

        // ── Auxiliares ───────────────────────────────────────────────────

        private enum TipoHora { Ninguna, Inicio, Fin }

        private static TipoHora TipoColumna(string nombre, Dictionary<string, string> roles)
        {
            var limpio = RxSufijoColumna.Replace(nombre, "").Trim();

            // La marca de la plantilla manda sobre el nombre.
            if (roles.TryGetValue(limpio, out var rol))
            {
                return rol switch
                {
                    "horaInicio" => TipoHora.Inicio,
                    "horaFin" => TipoHora.Fin,
                    _ => TipoHora.Ninguna
                };
            }

            if (RxHoraInicio.IsMatch(limpio)) return TipoHora.Inicio;
            if (RxHoraFin.IsMatch(limpio)) return TipoHora.Fin;
            return TipoHora.Ninguna;
        }

        private static bool TryFilas(JsonElement bloque, out JsonElement filas)
        {
            if (bloque.TryGetProperty("rows", out filas) && filas.ValueKind == JsonValueKind.Array) return true;
            if (bloque.TryGetProperty("data", out filas) && filas.ValueKind == JsonValueKind.Array) return true;
            filas = default;
            return false;
        }

        private static string NombreTabla(JsonElement bloque, Dictionary<string, string> titulos, int indice)
        {
            if (bloque.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(t.GetString()))
                return t.GetString()!.Trim();

            var id = IdTexto(bloque);
            return id != null && titulos.TryGetValue(id, out var titulo) ? titulo : $"Tabla {indice}";
        }

        private static string? IdTexto(JsonElement el)
        {
            if (!el.TryGetProperty("id", out var id)) return null;
            return id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number => id.GetRawText(),
                _ => null
            };
        }

        private static string? Texto(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        };

        private static bool TryHora(string? texto, out TimeSpan hora)
        {
            hora = default;
            if (string.IsNullOrWhiteSpace(texto)) return false;
            var t = texto.Trim();

            // "08:00", "8:00", "08:00:00". Se exige ":" para no tomar un número
            // suelto ("8") como 8 días.
            if (t.Contains(':') && TimeSpan.TryParse(t, out hora)
                && hora >= TimeSpan.Zero && hora < TimeSpan.FromDays(1))
                return true;

            // "2026-09-14T08:00" u "8:00 AM".
            if (DateTime.TryParse(t, out var dt))
            {
                hora = dt.TimeOfDay;
                return true;
            }
            return false;
        }

        /// <summary>Títulos de tabla por id y roles de columna por etiqueta.</summary>
        private static (Dictionary<string, string> Titulos, Dictionary<string, string> Roles) LeerPlantilla(Template? template)
        {
            var titulos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var roles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(template?.BodyElements)) return (titulos, roles);

            try
            {
                using var doc = JsonDocument.Parse(template!.BodyElements!);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return (titulos, roles);

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var id = IdTexto(el);
                    if (id != null && el.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(t.GetString()))
                        titulos[id] = t.GetString()!.Trim();

                    if (!el.TryGetProperty("columns", out var cols) || cols.ValueKind != JsonValueKind.Array) continue;
                    foreach (var c in cols.EnumerateArray())
                    {
                        if (c.ValueKind != JsonValueKind.Object) continue;
                        var label = c.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
                        var rol = c.TryGetProperty("rol", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(rol))
                            roles[label!.Trim()] = rol!.Trim();
                    }
                }
            }
            catch (JsonException) { /* plantilla dañada: se usa solo el nombre de columna */ }

            return (titulos, roles);
        }
    }
}
