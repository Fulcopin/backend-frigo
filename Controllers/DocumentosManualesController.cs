using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Documentos de la LISTA MAESTRA cargados a mano por SGI (procedimientos,
    /// programas, manuales…), agrupados por área. Complementan a los formularios
    /// del sistema para que la lista maestra salga completa.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentosManualesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DocumentosManualesController> _logger;

        public DocumentosManualesController(
            ApplicationDbContext context,
            ILogger<DocumentosManualesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Lista los documentos manuales. Con ?area=TH devuelve solo esa área.
        /// Ordenados por área y código, que es como se leen en la lista maestra.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentoManual>>> GetDocumentos([FromQuery] string? area)
        {
            try
            {
                var q = _context.DocumentosManuales.AsNoTracking().AsQueryable();
                if (!string.IsNullOrWhiteSpace(area))
                    q = q.Where(d => d.Area == area);

                var docs = await q
                    .OrderBy(d => d.Area)
                    .ThenBy(d => d.Codigo)
                    .ThenBy(d => d.Nombre)
                    .ToListAsync();

                return Ok(docs);
            }
            catch (Exception ex) when (TablaNoExiste(ex))
            {
                _logger.LogWarning(AvisoTablaFaltante);
                return Ok(Array.Empty<DocumentoManual>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar documentos manuales");
                return StatusCode(500, new { message = "Error al listar documentos" });
            }
        }

        /// <summary>Las áreas que ya tienen documentos cargados (las pestañas).</summary>
        [HttpGet("areas")]
        public async Task<ActionResult<IEnumerable<string>>> GetAreas()
        {
            try
            {
                var areas = await _context.DocumentosManuales
                    .AsNoTracking()
                    .Select(d => d.Area)
                    .Distinct()
                    .OrderBy(a => a)
                    .ToListAsync();
                return Ok(areas);
            }
            catch (Exception ex) when (TablaNoExiste(ex))
            {
                _logger.LogWarning(AvisoTablaFaltante);
                return Ok(Array.Empty<string>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar áreas");
                return StatusCode(500, new { message = "Error al listar áreas" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<DocumentoManual>> CreateDocumento([FromBody] DocumentoManual doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Nombre) || string.IsNullOrWhiteSpace(doc.Area))
                return BadRequest(new { message = "El área y el nombre del documento son obligatorios." });

            try
            {
                doc.Id = 0;
                doc.Area = doc.Area.Trim();
                doc.Nombre = doc.Nombre.Trim();
                doc.CopiaControlada = NormalizarSiNo(doc.CopiaControlada);
                doc.CreadoEn = DateTime.Now;
                doc.ActualizadoEn = null;

                _context.DocumentosManuales.Add(doc);
                await _context.SaveChangesAsync();
                return Ok(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear documento manual");
                if (TablaNoExiste(ex)) return StatusCode(503, new { message = AvisoTablaFaltante });
                return StatusCode(500, new { message = "Error al crear el documento" });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DocumentoManual>> UpdateDocumento(int id, [FromBody] DocumentoManual cambios)
        {
            if (cambios == null) return BadRequest(new { message = "Faltan los datos del documento." });

            try
            {
                var doc = await _context.DocumentosManuales.FindAsync(id);
                if (doc == null) return NotFound(new { message = "El documento no existe." });

                if (!string.IsNullOrWhiteSpace(cambios.Area)) doc.Area = cambios.Area.Trim();
                if (!string.IsNullOrWhiteSpace(cambios.Nombre)) doc.Nombre = cambios.Nombre.Trim();
                doc.Codigo = cambios.Codigo?.Trim();
                doc.Version = cambios.Version?.Trim();
                doc.Fecha = cambios.Fecha;
                doc.CopiaControlada = NormalizarSiNo(cambios.CopiaControlada);
                doc.Ubicacion = cambios.Ubicacion?.Trim();
                doc.Obsoleto = cambios.Obsoleto;
                doc.Observaciones = cambios.Observaciones?.Trim();
                doc.ActualizadoEn = DateTime.Now;

                await _context.SaveChangesAsync();
                return Ok(doc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar documento manual {Id}", id);
                if (TablaNoExiste(ex)) return StatusCode(503, new { message = AvisoTablaFaltante });
                return StatusCode(500, new { message = "Error al actualizar el documento" });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteDocumento(int id)
        {
            try
            {
                var doc = await _context.DocumentosManuales.FindAsync(id);
                if (doc == null) return NotFound(new { message = "El documento no existe." });

                _context.DocumentosManuales.Remove(doc);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Documento eliminado", id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar documento manual {Id}", id);
                if (TablaNoExiste(ex)) return StatusCode(503, new { message = AvisoTablaFaltante });
                return StatusCode(500, new { message = "Error al eliminar el documento" });
            }
        }

        /// <summary>
        /// ¿El error es "la tabla no existe"?
        ///
        /// El backend se despliega antes de correr el script de la tabla, y hasta
        /// que se corre cada consulta devolvía 500 y ensuciaba la consola del
        /// navegador. Con esto la pantalla se ve vacía y sigue funcionando.
        /// </summary>
        private static bool TablaNoExiste(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                var m = e.Message ?? "";
                if (m.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                    || m.Contains("nombre de objeto no válido", StringComparison.OrdinalIgnoreCase)
                    || m.Contains("DocumentosManuales", StringComparison.OrdinalIgnoreCase)
                       && m.Contains("no válido", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private const string AvisoTablaFaltante =
            "Falta crear la tabla DocumentosManuales. Ejecutá una vez el script "
            + "backend-frigo/Migrations/CreateDocumentosManualesTable.sql en SQL Server.";

        /// <summary>La copia controlada se guarda siempre como "Si" o "No".</summary>
        private static string NormalizarSiNo(string? valor)
        {
            var v = (valor ?? "").Trim().ToLowerInvariant();
            return (v == "si" || v == "sí" || v == "s" || v == "true" || v == "1") ? "Si" : "No";
        }
    }
}
