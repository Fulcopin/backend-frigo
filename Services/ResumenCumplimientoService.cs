using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;

namespace FormBuilder.API.Services
{
    /// <summary>
    /// 📧 RESUMEN DE CUMPLIMIENTO POR CORREO
    ///
    /// Cada X horas (configurable, por defecto 10) envía el cuadro de qué
    /// registros faltan y cuáles están sin firmar:
    ///   - a los destinatarios fijos (SGI / Calidad): el cuadro completo;
    ///   - a cada responsable y sus reemplazos: SOLO sus registros pendientes,
    ///     usando el correo del catálogo de firmas.
    ///
    /// Se configura en la pantalla de Alertas (tabla AlertConfiguration):
    ///   EnableResumenCumplimiento, ResumenCadaHoras, ResumenDiasAtras,
    ///   ResumenIncluirFinDeSemana, ResumenAResponsables, ResumenRecipients.
    ///
    /// La última vez enviada queda en UltimoResumenCumplimiento, así que si la
    /// API se reinicia no se reenvía de más ni se saltea el turno.
    /// </summary>
    public class ResumenCumplimientoService : BackgroundService
    {
        private static readonly TimeSpan CadaCuanto = TimeSpan.FromMinutes(15);

        private readonly IServiceProvider _servicios;
        private readonly ILogger<ResumenCumplimientoService> _logger;

        public ResumenCumplimientoService(IServiceProvider servicios, ILogger<ResumenCumplimientoService> logger)
        {
            _servicios = servicios;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            // Un respiro al arrancar, para no competir con el inicio de la API.
            await Task.Delay(TimeSpan.FromMinutes(1), token);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var alcance = _servicios.CreateScope();
                    var context = alcance.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var email = alcance.ServiceProvider.GetRequiredService<IEmailService>();

                    var config = await context.AlertConfigurations.FirstOrDefaultAsync(token);
                    if (config != null && config.EnableResumenCumplimiento)
                    {
                        var horas = Math.Max(1, config.ResumenCadaHoras);
                        var ultimo = config.UltimoResumenCumplimiento;
                        if (ultimo == null || DateTime.Now - ultimo.Value >= TimeSpan.FromHours(horas))
                        {
                            var enviados = await EnviarAsync(context, email, config);
                            config.UltimoResumenCumplimiento = DateTime.Now;
                            await context.SaveChangesAsync(token);
                            _logger.LogInformation("📧 Resumen de cumplimiento enviado a {Cantidad} destinatario(s)", enviados);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar el resumen de cumplimiento");
                }

                await Task.Delay(CadaCuanto, token);
            }
        }

        /// <summary>
        /// Arma y manda el resumen. Devuelve a cuántos correos se envió.
        /// También lo usa el botón "Enviar ahora" de la pantalla.
        /// </summary>
        public static async Task<int> EnviarAsync(
            ApplicationDbContext context, IEmailService email, Models.AlertConfiguration config)
        {
            var dias = Math.Max(1, config.ResumenDiasAtras);
            var hasta = DateTime.Today;
            var desde = hasta.AddDays(-(dias - 1));

            var servicio = new CumplimientoService(context);
            var cuadro = await servicio.CalcularAsync(desde, hasta,
                incluirFinDeSemana: config.ResumenIncluirFinDeSemana);

            // Sin nada pendiente no se manda correo: avisar "todo bien" cada 10
            // horas hace que dejen de leer los avisos que sí importan.
            var pendientes = cuadro.Registros
                .Where(r => r.Faltantes.Count > 0 || r.SinFirmar.Count > 0)
                .ToList();
            if (pendientes.Count == 0) return 0;

            var enviados = 0;

            // 1) Destinatarios fijos: el cuadro completo.
            foreach (var correo in LeerCorreos(config.ResumenRecipients))
            {
                var ok = await email.SendAlertEmailAsync(
                    correo,
                    $"Registros pendientes ({cuadro.TotalFaltantes} sin llenar, {cuadro.TotalSinFirmar} sin firmar)",
                    Html(cuadro, pendientes, null));
                if (ok) enviados++;
            }

            // 2) Cada responsable: solo lo suyo.
            if (config.ResumenAResponsables)
            {
                var porCorreo = new Dictionary<string, List<RegistroCumplimiento>>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in pendientes)
                    foreach (var c in r.Correos)
                    {
                        if (!porCorreo.TryGetValue(c, out var lista))
                            porCorreo[c] = lista = new List<RegistroCumplimiento>();
                        lista.Add(r);
                    }

                foreach (var (correo, registros) in porCorreo)
                {
                    var faltan = registros.Sum(r => r.Faltantes.Count);
                    var sinFirmar = registros.Sum(r => r.SinFirmar.Count);
                    var ok = await email.SendAlertEmailAsync(
                        correo,
                        $"Tienes {faltan} registro(s) sin llenar y {sinFirmar} sin firmar",
                        Html(cuadro, registros, correo));
                    if (ok) enviados++;
                }
            }

            return enviados;
        }

        private static List<string> LeerCorreos(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var lista = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return lista.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (JsonException)
            {
                // Puede venir como "uno@x.com, dos@x.com"
                return json.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(c => c.Trim().Trim('"', '[', ']'))
                           .Where(c => c.Contains('@'))
                           .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        /// <summary>Correo en HTML: una tabla con lo pendiente de cada registro.</summary>
        private static string Html(CuadroCumplimiento cuadro, List<RegistroCumplimiento> registros, string? paraQuien)
        {
            static string Fecha(string iso) =>
                DateTime.TryParse(iso, out var d) ? d.ToString("dd/MM") : iso;

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;color:#1e293b'>");
            sb.Append("<h2 style='color:#0f3d5e;margin:0 0 4px'>Registros pendientes</h2>");
            sb.Append($"<p style='margin:0 0 14px;color:#64748b'>Del {Fecha(cuadro.Desde)} al {Fecha(cuadro.Hasta)}");
            if (paraQuien != null) sb.Append(" · registros a tu cargo");
            sb.Append("</p>");

            sb.Append("<table style='border-collapse:collapse;font-size:13px;min-width:520px'>");
            sb.Append("<tr style='background:#0f3d5e;color:#fff'>")
              .Append("<th style='padding:8px;text-align:left'>Registro</th>")
              .Append("<th style='padding:8px;text-align:left'>Responsable</th>")
              .Append("<th style='padding:8px'>Sin llenar</th>")
              .Append("<th style='padding:8px'>Sin firmar</th>")
              .Append("<th style='padding:8px'>Cumple</th></tr>");

            foreach (var r in registros.OrderBy(r => r.Cumplimiento))
            {
                var faltan = r.Faltantes.Count == 0 ? "—" : string.Join(", ", r.Faltantes.Select(Fecha));
                var sinFirmar = r.SinFirmar.Count == 0 ? "—" : string.Join(", ", r.SinFirmar.Select(Fecha));
                var color = r.Cumplimiento >= 100 ? "#166534" : r.Cumplimiento >= 80 ? "#92400e" : "#b91c1c";
                var responsable = string.IsNullOrWhiteSpace(r.Responsable) ? "sin responsable" : r.Responsable;
                // Solo el primer reemplazo: es el responsable de que se llene si
                // el titular no está. Los demás pueden firmar igual.
                var reemplazo = string.IsNullOrWhiteSpace(r.ReemplazoPrincipal)
                    ? "<div style='color:#b91c1c;font-size:11px'>sin reemplazo</div>"
                    : $"<div style='color:#64748b;font-size:11px'>reemplazo: {r.ReemplazoPrincipal}</div>";

                sb.Append("<tr style='border-bottom:1px solid #e2e8f0'>")
                  .Append($"<td style='padding:8px'><strong>{r.Codigo}</strong><div style='color:#64748b;font-size:11px'>{r.Nombre} · {r.Frecuencia}</div></td>")
                  .Append($"<td style='padding:8px'>{responsable}{reemplazo}</td>")
                  .Append($"<td style='padding:8px;text-align:center;color:#b91c1c'>{faltan}</td>")
                  .Append($"<td style='padding:8px;text-align:center;color:#92400e'>{sinFirmar}</td>")
                  .Append($"<td style='padding:8px;text-align:center;color:{color};font-weight:bold'>{r.Cumplimiento}%</td>")
                  .Append("</tr>");
            }

            sb.Append("</table>");
            sb.Append("<p style='margin-top:16px;color:#64748b;font-size:12px'>")
              .Append("Este aviso se envía automáticamente. El detalle completo está en el sistema, en ")
              .Append("<strong>Control de cumplimiento</strong>.</p>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}