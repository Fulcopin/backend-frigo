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
    }
}
