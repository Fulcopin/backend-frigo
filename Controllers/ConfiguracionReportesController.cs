using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Configuración del reporte de "Descargar Datos", una por formulario.
    ///
    /// Guarda qué columnas salen, qué hace cada una en el total (suma, promedio,
    /// máximo, no incluir) y cómo se arma la vista. Vive en la base para que se
    /// configure una vez y la use toda la planta desde cualquier computadora.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConfiguracionReportesController> _logger;

        public ConfiguracionReportesController(
            ApplicationDbContext context,
            ILogger<ConfiguracionReportesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Todas las configuraciones guardadas, con el código y el nombre de su
        /// formulario. Sirve para ver de un vistazo qué está configurado y qué no.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTodas()
        {
            try
            {
                var configs = await _context.ConfiguracionesReporte
                    .AsNoTracking()
                    .OrderBy(c => c.TemplateID)
                    .ToListAsync();

                var templates = await _context.Templates
                    .AsNoTracking()
                    .Select(t => new { t.TemplateID, t.Codigo, t.Nombre })
                    .ToListAsync();

                var result = configs.Select(c =>
                {
                    var tpl = templates.FirstOrDefault(t => t.TemplateID == c.TemplateID);
                    return new
                    {
                        c.Id,
                        c.TemplateID,
                        Codigo = tpl?.Codigo ?? (c.TemplateID == null ? "TODOS" : "N/A"),
                        FormularioNombre = tpl?.Nombre ?? (c.TemplateID == null ? "Todos los formularios" : "Plantilla eliminada"),
                        c.Nombre,
                        c.ColumnasVisibles,
                        c.Operaciones,
                        c.UnaLineaPorForm,
                        c.OcultarVacias,
                        c.CompactarFilas,
                        c.OmitirTotalesDelForm,
                        c.SubtotalPorForm,
                        c.ModoResumen,
                        c.AgruparPor,
                        c.ActualizadoPor,
                        c.UpdatedAt,
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar configuraciones de reporte");
                return StatusCode(500, new { message = "Error al listar las configuraciones" });
            }
        }

        /// <summary>
        /// La configuración de un formulario. templateId = 0 devuelve la de la
        /// vista general ("todos los formularios").
        /// </summary>
        [HttpGet("template/{templateId:int}")]
        public async Task<ActionResult<object>> GetPorTemplate(int templateId)
        {
            try
            {
                int? clave = templateId > 0 ? templateId : null;
                var config = await _context.ConfiguracionesReporte
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.TemplateID == clave);

                // Sin configuración no es un error: la pantalla arranca con sus valores por defecto.
                if (config == null) return NoContent();

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la configuración del template {TemplateId}", templateId);
                return StatusCode(500, new { message = "Error al obtener la configuración" });
            }
        }

        /// <summary>
        /// Guarda la configuración de un formulario. Si ya existe la reemplaza:
        /// hay una sola por formulario, no se acumulan versiones.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult> Guardar([FromBody] ConfiguracionReporteDto dto)
        {
            try
            {
                int? clave = dto.TemplateID > 0 ? dto.TemplateID : null;

                var config = await _context.ConfiguracionesReporte
                    .FirstOrDefaultAsync(c => c.TemplateID == clave);

                bool esNueva = config == null;
                if (config == null)
                {
                    config = new ConfiguracionReporte { TemplateID = clave, CreatedAt = DateTime.Now };
                    _context.ConfiguracionesReporte.Add(config);
                }

                config.Nombre               = string.IsNullOrWhiteSpace(dto.Nombre) ? "Resumen" : dto.Nombre.Trim();
                config.ColumnasVisibles     = dto.ColumnasVisibles ?? "[]";
                config.Operaciones          = dto.Operaciones ?? "{}";
                config.UnaLineaPorForm      = dto.UnaLineaPorForm;
                config.OcultarVacias        = dto.OcultarVacias;
                config.CompactarFilas       = dto.CompactarFilas;
                config.OmitirTotalesDelForm = dto.OmitirTotalesDelForm;
                config.SubtotalPorForm      = dto.SubtotalPorForm;
                config.ModoResumen          = dto.ModoResumen;
                config.AgruparPor           = dto.AgruparPor ?? "";
                config.ActualizadoPor       = dto.ActualizadoPor;
                config.UpdatedAt            = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Configuración de reporte {Accion} para el template {TemplateId} por {Usuario}",
                    esNueva ? "creada" : "actualizada", clave, dto.ActualizadoPor ?? "desconocido");

                return Ok(new { success = true, id = config.Id, message = "Configuración guardada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la configuración de reporte");
                return StatusCode(500, new { message = "Error al guardar la configuración" });
            }
        }

        /// <summary>Borra la configuración de un formulario; la pantalla vuelve a mostrar todo.</summary>
        [HttpDelete("template/{templateId:int}")]
        public async Task<ActionResult> Borrar(int templateId)
        {
            try
            {
                int? clave = templateId > 0 ? templateId : null;
                var config = await _context.ConfiguracionesReporte
                    .FirstOrDefaultAsync(c => c.TemplateID == clave);

                if (config == null) return NotFound(new { message = "No había configuración guardada" });

                _context.ConfiguracionesReporte.Remove(config);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Configuración borrada" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar la configuración del template {TemplateId}", templateId);
                return StatusCode(500, new { message = "Error al borrar la configuración" });
            }
        }
    }

    public class ConfiguracionReporteDto
    {
        public int TemplateID { get; set; }          // 0 = vista de todos los formularios
        public string? Nombre { get; set; }
        public string? ColumnasVisibles { get; set; }
        public string? Operaciones { get; set; }
        public bool UnaLineaPorForm { get; set; }
        public bool OcultarVacias { get; set; } = true;
        public bool CompactarFilas { get; set; } = true;
        public bool OmitirTotalesDelForm { get; set; }
        public bool SubtotalPorForm { get; set; } = true;
        public bool ModoResumen { get; set; }
        public string? AgruparPor { get; set; }
        public string? ActualizadoPor { get; set; }
    }
}
