// Estos 'using' le dicen al archivo qué herramientas necesita usar.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    // [Route] define la URL base para este controlador. Todas las peticiones
    // a "/api/Templates" llegarán aquí.
    [Route("api/[controller]")]
    [ApiController]
    public class TemplatesController : ControllerBase
    {
        // Esta variable privada guardará la conexión a la base de datos.
        private readonly ApplicationDbContext _context;

        // Este es el "constructor". Cuando se crea el controlador para manejar
        // una petición, .NET automáticamente le pasa la conexión a la base de datos
        // (esto se configuró en Program.cs).
        public TemplatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        //==============================================================
        // MÉTODO GET - Para obtener todas las plantillas
        // URL: GET /api/Templates
        //==============================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Template>>> GetTemplates()
        {
            // Usa Entity Framework para ir a la base de datos, tomar todas
            // las filas de la tabla "Templates" y devolverlas como una lista.
            return await _context.Templates.ToListAsync();
        }

        //==============================================================
        // MÉTODO GET - Para obtener UNA plantilla por su ID
        // URL: GET /api/Templates/5  (el 5 es un ejemplo de ID)
        //==============================================================
        [HttpGet("{id}")]
        public async Task<ActionResult<Template>> GetTemplate(int id)
        {
            // Busca en la tabla "Templates" una fila cuya clave primaria (TemplateID)
            // coincida con el 'id' que vino en la URL.
            var template = await _context.Templates.FindAsync(id);

            // Si no se encuentra ninguna plantilla con ese ID, devuelve
            // un error estándar 404 (No Encontrado).
            if (template == null)
            {
                return NotFound();
            }

            // Si se encuentra, devuelve los datos de la plantilla.
            return template;
        }

        // NUEVO: Endpoint para crear template con 5 columnas por defecto
        [HttpGet("{id}/with-default-columns")]
        public async Task<ActionResult<object>> GetTemplateWithDefaultColumns(int id)
        {
            var template = await _context.Templates.FindAsync(id);

            if (template == null)
            {
                return NotFound();
            }

            // Crear estructura por defecto con 5 columnas si no existe BodyElements
            var defaultBodyElements = template.BodyElements ?? GenerateDefault5ColumnStructure();

            return Ok(new
            {
                template.TemplateID,
                template.Codigo,
                template.Nombre,
                template.Version,
                template.Objetivo,
                template.Proceso,
                template.CuandoSeUsa,
                template.QuienLoLlena,
                template.HeaderFields,
                BodyElements = defaultBodyElements,
                template.Firmas,
                template.CreatedAt,
                template.UpdatedAt
            });
        }

        //==============================================================
        // MÉTODO POST - Para crear una NUEVA plantilla
        // URL: POST /api/Templates
        //==============================================================
        [HttpPost]
        public async Task<ActionResult<Template>> PostTemplate([FromBody] Template template)
        {
            // Recibe los datos de la nueva plantilla desde el cuerpo (body) de la petición del frontend.
            
            // Añade este nuevo objeto 'template' a la colección de Entity Framework.
            // Todavía no se guarda en la base de datos.
            _context.Templates.Add(template);
            
            // Este comando toma todos los cambios pendientes (en este caso, añadir
            // la nueva plantilla) y los ejecuta en la base de datos de Azure.
            await _context.SaveChangesAsync();

            // Devuelve una respuesta HTTP estándar "201 Created".
            // También incluye la URL para obtener el recurso recién creado y el
            // propio objeto 'template' (que ahora tendrá un 'templateID' asignado por la BD).
            return CreatedAtAction(nameof(GetTemplate), new { id = template.TemplateID }, template);
        }

        //==============================================================
        // MÉTODO PUT - Para actualizar una plantilla existente
        // URL: PUT /api/Templates/5
        //==============================================================
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTemplate(int id, [FromBody] Template template)
        {
            // Comprobación de seguridad: si el ID en la URL no coincide con el
            // ID del objeto que se está enviando, es una petición incorrecta.
            if (id != template.TemplateID)
            {
                return BadRequest();
            }

            // Actualizar la fecha de modificación
            template.UpdatedAt = DateTime.UtcNow;

            // Le dice a Entity Framework que este objeto 'template' no es nuevo,
            // sino que representa una versión modificada de una fila que ya existe.
            _context.Entry(template).State = EntityState.Modified;

            try
            {
                // Intenta guardar los cambios en la base de datos.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Este error puede ocurrir si alguien borró la plantilla justo
                // mientras intentábamos actualizarla.
                if (!TemplateExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Devuelve una respuesta "204 No Content", que es el estándar para
            // indicar que la actualización se realizó con éxito.
            return NoContent();
        }
        
        //==============================================================
        // MÉTODO DELETE - Para borrar una plantilla
        // URL: DELETE /api/Templates/5
        //==============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            // Primero, busca la plantilla que se va a borrar.
            var template = await _context.Templates.FindAsync(id);
            if (template == null)
            {
                // Si no existe, no se puede borrar. Devuelve 404.
                return NotFound();
            }

            // Le dice a Entity Framework que esta plantilla debe ser eliminada.
            _context.Templates.Remove(template);
            // Ejecuta el comando DELETE en la base de datos.
            await _context.SaveChangesAsync();

            // Devuelve "204 No Content" para indicar el éxito.
            return NoContent();
        }


        // Función de ayuda interna. No es un endpoint de la API.
        private bool TemplateExists(int id)
        {
            return _context.Templates.Any(e => e.TemplateID == id);
        }

        // NUEVO: Método helper para generar estructura de 5 columnas por defecto
        private string GenerateDefault5ColumnStructure()
        {
            var defaultStructure = new
            {
                sections = new[]
                {
                    new
                    {
                        id = "section-1",
                        title = "Datos Principales",
                        type = "table",
                        columns = new[]
                        {
                            new { id = "col1", name = "Columna 1", type = "text" },
                            new { id = "col2", name = "Columna 2", type = "text" },
                            new { id = "col3", name = "Columna 3", type = "text" },
                            new { id = "col4", name = "Columna 4", type = "text" },
                            new { id = "col5", name = "Columna 5", type = "text" }
                        },
                        rows = new object[0] // Array vacío inicialmente
                    }
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(defaultStructure);
        }

        // NUEVO: Método helper para generar datos de formulario con 5 columnas por defecto
        private string GenerateDefault5ColumnData()
        {
            var defaultData = new
            {
                sections = new[]
                {
                    new
                    {
                        id = "section-1",
                        data = new[]
                        {
                            new
                            {
                                col1 = "",
                                col2 = "",
                                col3 = "",
                                col4 = "",
                                col5 = ""
                            }
                        }
                    }
                }
            };

            return System.Text.Json.JsonSerializer.Serialize(defaultData);
        }

        //==============================================================
        // HISTORIAL DE VERSIONES - Endpoints para ventana de versiones
        //==============================================================

        // GET: api/Templates/5/versions/history
        // Obtiene el historial de todas las versiones usadas de un template
        [HttpGet("{id}/versions/history")]
        public async Task<ActionResult<IEnumerable<TemplateVersionHistoryDto>>> GetTemplateVersionHistory(int id)
        {
            // Verificar que el template existe
            var templateExists = await _context.Templates.AnyAsync(t => t.TemplateID == id);
            if (!templateExists)
            {
                return NotFound(new { message = $"Template con ID {id} no encontrado" });
            }

            // Obtener la versión actual del template
            var currentTemplate = await _context.Templates.FindAsync(id);
            var currentVersion = currentTemplate?.Version ?? "1.0";

            // Agrupar formularios por versión del template
            var versionHistory = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion != null)
                .GroupBy(f => f.TemplateVersion)
                .Select(g => new TemplateVersionHistoryDto
                {
                    Version = g.Key ?? "Desconocida",
                    FirstUsedDate = g.Min(f => f.CreatedAt),
                    LastUsedDate = g.Max(f => f.CreatedAt),
                    FormCount = g.Count(),
                    IsCurrentVersion = g.Key == currentVersion
                })
                .OrderByDescending(v => v.FirstUsedDate)
                .ToListAsync();

            // Si no hay formularios guardados, mostrar al menos la versión actual
            if (!versionHistory.Any())
            {
                versionHistory.Add(new TemplateVersionHistoryDto
                {
                    Version = currentVersion,
                    FirstUsedDate = currentTemplate?.CreatedAt,
                    LastUsedDate = null,
                    FormCount = 0,
                    IsCurrentVersion = true
                });
            }

            return Ok(versionHistory);
        }

        // GET: api/Templates/5/versions/02-01
        // Obtiene los detalles de una versión específica y sus formularios
        [HttpGet("{id}/versions/{version}")]
        public async Task<ActionResult<TemplateVersionDetailDto>> GetVersionDetail(int id, string version)
        {
            // Obtener template actual
            var currentTemplate = await _context.Templates.FindAsync(id);
            if (currentTemplate == null)
            {
                return NotFound(new { message = $"Template con ID {id} no encontrado" });
            }

            // Obtener formularios con esta versión
            var formsWithVersion = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version)
                .Select(f => new FormSummaryDto
                {
                    FormID = f.FormID,
                    CreatedAt = f.CreatedAt,
                    HeaderData = f.HeaderData,
                    Observaciones = f.Observaciones
                })
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            // Si es la versión actual, usar el template actual
            if (version == currentTemplate.Version)
            {
                return Ok(new TemplateVersionDetailDto
                {
                    Version = currentTemplate.Version ?? "1.0",
                    TemplateID = currentTemplate.TemplateID,
                    Codigo = currentTemplate.Codigo ?? "",
                    Nombre = currentTemplate.Nombre ?? "",
                    Objetivo = currentTemplate.Objetivo,
                    Proceso = currentTemplate.Proceso,
                    HeaderFields = currentTemplate.HeaderFields,
                    BodyElements = currentTemplate.BodyElements,
                    Firmas = currentTemplate.Firmas,
                    AssociatedForms = formsWithVersion
                });
            }

            // Si es una versión antigua, buscar en el snapshot del primer formulario
            var firstFormWithVersion = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version && f.TemplateSnapshot != null)
                .OrderBy(f => f.CreatedAt)
                .FirstOrDefaultAsync();

            if (firstFormWithVersion?.TemplateSnapshot != null)
            {
                try
                {
                    // Deserializar el snapshot para obtener la estructura antigua
                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<Template>(firstFormWithVersion.TemplateSnapshot);
                    
                    if (snapshot != null)
                    {
                        return Ok(new TemplateVersionDetailDto
                        {
                            Version = version,
                            TemplateID = id,
                            Codigo = snapshot.Codigo ?? "",
                            Nombre = snapshot.Nombre ?? "",
                            Objetivo = snapshot.Objetivo,
                            Proceso = snapshot.Proceso,
                            HeaderFields = snapshot.HeaderFields,
                            BodyElements = snapshot.BodyElements,
                            Firmas = snapshot.Firmas,
                            AssociatedForms = formsWithVersion
                        });
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Si falla la deserialización, continuar con fallback
                }
            }

            // Fallback: devolver estructura básica con los formularios
            return Ok(new TemplateVersionDetailDto
            {
                Version = version,
                TemplateID = id,
                Codigo = currentTemplate.Codigo ?? "",
                Nombre = $"{currentTemplate.Nombre} (Versión {version})",
                Objetivo = "Snapshot no disponible - versión histórica",
                Proceso = currentTemplate.Proceso,
                HeaderFields = null,
                BodyElements = null,
                Firmas = null,
                AssociatedForms = formsWithVersion
            });
        }

        // GET: api/Templates/5/versions/compare?oldVersion=02-01&newVersion=03-01
        // Compara dos versiones de un template
        [HttpGet("{id}/versions/compare")]
        public async Task<ActionResult<VersionComparisonDto>> CompareVersions(
            int id, 
            [FromQuery] string oldVersion, 
            [FromQuery] string newVersion)
        {
            if (string.IsNullOrEmpty(oldVersion) || string.IsNullOrEmpty(newVersion))
            {
                return BadRequest(new { message = "Se requieren oldVersion y newVersion como parámetros" });
            }

            // Verificar que el template existe
            var currentTemplate = await _context.Templates.FindAsync(id);
            if (currentTemplate == null)
            {
                return NotFound(new { message = $"Template con ID {id} no encontrado" });
            }

            // Obtener detalles de la versión antigua
            TemplateVersionDetailDto? oldData = null;
            if (oldVersion == currentTemplate.Version)
            {
                // Versión actual
                oldData = await GetVersionDetailInternal(id, oldVersion, currentTemplate);
            }
            else
            {
                // Versión histórica
                oldData = await GetVersionDetailInternal(id, oldVersion, currentTemplate);
            }

            // Obtener detalles de la versión nueva
            TemplateVersionDetailDto? newData = null;
            if (newVersion == currentTemplate.Version)
            {
                // Versión actual
                newData = await GetVersionDetailInternal(id, newVersion, currentTemplate);
            }
            else
            {
                // Versión histórica
                newData = await GetVersionDetailInternal(id, newVersion, currentTemplate);
            }

            if (oldData == null || newData == null)
            {
                return NotFound(new { message = "Una o ambas versiones no encontradas" });
            }

            var comparison = new VersionComparisonDto
            {
                OldVersion = oldVersion,
                NewVersion = newVersion,
                ComparisonDate = DateTime.UtcNow,
                Changes = new List<string>()
            };

            // Comparar campos
            if (oldData.Nombre != newData.Nombre)
                comparison.Changes.Add($"Nombre: '{oldData.Nombre}' → '{newData.Nombre}'");

            if (oldData.Objetivo != newData.Objetivo)
                comparison.Changes.Add($"Objetivo: '{oldData.Objetivo}' → '{newData.Objetivo}'");

            if (oldData.Proceso != newData.Proceso)
                comparison.Changes.Add($"Proceso: '{oldData.Proceso}' → '{newData.Proceso}'");

            if (oldData.HeaderFields != newData.HeaderFields)
                comparison.Changes.Add("HeaderFields: Estructura modificada");

            if (oldData.BodyElements != newData.BodyElements)
                comparison.Changes.Add("BodyElements: Estructura de tabla modificada");

            if (oldData.Firmas != newData.Firmas)
                comparison.Changes.Add("Firmas: Estructura de firmas modificada");

            if (!comparison.Changes.Any())
                comparison.Changes.Add("No se detectaron cambios entre versiones");

            return Ok(comparison);
        }

        // Método helper interno para obtener detalles de versión sin ActionResult
        private async Task<TemplateVersionDetailDto?> GetVersionDetailInternal(int id, string version, Template currentTemplate)
        {
            // Obtener formularios con esta versión
            var formsWithVersion = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version)
                .Select(f => new FormSummaryDto
                {
                    FormID = f.FormID,
                    CreatedAt = f.CreatedAt,
                    HeaderData = f.HeaderData,
                    Observaciones = f.Observaciones
                })
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            // Si es la versión actual, usar el template actual
            if (version == currentTemplate.Version)
            {
                return new TemplateVersionDetailDto
                {
                    Version = currentTemplate.Version ?? "1.0",
                    TemplateID = currentTemplate.TemplateID,
                    Codigo = currentTemplate.Codigo ?? "",
                    Nombre = currentTemplate.Nombre ?? "",
                    Objetivo = currentTemplate.Objetivo,
                    Proceso = currentTemplate.Proceso,
                    HeaderFields = currentTemplate.HeaderFields,
                    BodyElements = currentTemplate.BodyElements,
                    Firmas = currentTemplate.Firmas,
                    AssociatedForms = formsWithVersion
                };
            }

            // Si es una versión antigua, buscar en el snapshot del primer formulario
            var firstFormWithVersion = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version && f.TemplateSnapshot != null)
                .OrderBy(f => f.CreatedAt)
                .FirstOrDefaultAsync();

            if (firstFormWithVersion?.TemplateSnapshot != null)
            {
                try
                {
                    // Deserializar el snapshot para obtener la estructura antigua
                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<Template>(firstFormWithVersion.TemplateSnapshot);
                    
                    if (snapshot != null)
                    {
                        return new TemplateVersionDetailDto
                        {
                            Version = version,
                            TemplateID = id,
                            Codigo = snapshot.Codigo ?? "",
                            Nombre = snapshot.Nombre ?? "",
                            Objetivo = snapshot.Objetivo,
                            Proceso = snapshot.Proceso,
                            HeaderFields = snapshot.HeaderFields,
                            BodyElements = snapshot.BodyElements,
                            Firmas = snapshot.Firmas,
                            AssociatedForms = formsWithVersion
                        };
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Si falla la deserialización, continuar con fallback
                }
            }

            // Fallback: devolver estructura básica con los formularios
            return new TemplateVersionDetailDto
            {
                Version = version,
                TemplateID = id,
                Codigo = currentTemplate.Codigo ?? "",
                Nombre = $"{currentTemplate.Nombre} (Versión {version})",
                Objetivo = "Snapshot no disponible - versión histórica",
                Proceso = currentTemplate.Proceso,
                HeaderFields = null,
                BodyElements = null,
                Firmas = null,
                AssociatedForms = formsWithVersion
            };
        }

        // GET: api/Templates/5/versions/02-01/forms
        // Obtiene SOLO los formularios de una versión específica (endpoint simplificado)
        [HttpGet("{id}/versions/{version}/forms")]
        public async Task<ActionResult<IEnumerable<FilledForm>>> GetFormsByVersion(int id, string version)
        {
            var forms = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return Ok(new List<FilledForm>()); // Devolver lista vacía en lugar de 404
            }

            return Ok(forms);
        }
    }
}