// Estos 'using' le dicen al archivo qué herramientas necesita usar.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;
using System.Text.Json; // ✅ Esto quita el error de JsonSerializer
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
        private readonly IEmailService _emailService;
        private readonly ILogger<TemplatesController> _logger;

        // Este es el "constructor". Cuando se crea el controlador para manejar
        // una petición, .NET automáticamente le pasa la conexión a la base de datos
        // (esto se configuró en Program.cs).
        public TemplatesController(ApplicationDbContext context, IEmailService emailService, ILogger<TemplatesController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        //==============================================================
        // MÉTODO GET - Para obtener todas las plantillas
        // URL: GET /api/Templates
        // ✅ MODIFICADO: Solo devuelve plantillas publicadas (no borradores)
        //==============================================================
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Template>>> GetTemplates()
        {
            // ✅ Filtra borradores y obsoletas: solo devuelve plantillas activas
            return await _context.Templates
                .Where(t => !t.IsDraft && !t.IsObsolete)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        //==============================================================
        // ✅ NUEVO: Endpoint para obtener TODAS las plantillas (incluyendo obsoletas)
        // URL: GET /api/Templates/all
        // Usado por ManageTemplates para que el admin vea todo
        //==============================================================
        [HttpGet("all")]
        public async Task<ActionResult<IEnumerable<Template>>> GetAllTemplates()
        {
            return await _context.Templates
                .Where(t => !t.IsDraft)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        //==============================================================
        // ✅ NUEVO: Endpoint para obtener solo borradores
        // URL: GET /api/Templates/drafts
        //==============================================================
        [HttpGet("drafts")]
        public async Task<ActionResult<IEnumerable<Template>>> GetDrafts()
        {
            return await _context.Templates
                .Where(t => t.IsDraft)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
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
                template.Supervisa,
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
    if (id != template.TemplateID) return BadRequest();

    // Obtener la versión que está actualmente en la base de datos (sin tracking para comparar)
    var oldTemplate = await _context.Templates.AsNoTracking().FirstOrDefaultAsync(t => t.TemplateID == id);
    if (oldTemplate == null) return NotFound();

    // 🎯 DETECCIÓN TOTAL DE CAMBIOS
    // Comparamos cada propiedad. Si el JSON cambió (aunque sea una coma), se detecta.
    bool cambioAlgo = 
        oldTemplate.Nombre != template.Nombre ||
        oldTemplate.Supervisa != template.Supervisa ||
        oldTemplate.Proceso != template.Proceso ||
        oldTemplate.FechaVersion != template.FechaVersion ||
        oldTemplate.Version != template.Version ||
        oldTemplate.HeaderFields != template.HeaderFields ||
        oldTemplate.BodyElements != template.BodyElements ||
        oldTemplate.Firmas != template.Firmas ||
        oldTemplate.IsMasterForm != template.IsMasterForm;

    if (cambioAlgo)
    {
        // Guardamos el estado ANTERIOR en el historial antes de actualizar a lo NUEVO
        var historyEntry = new TemplateVersion
        {
            TemplateID = oldTemplate.TemplateID,
            Version = oldTemplate.Version,
            FechaVersion = oldTemplate.FechaVersion,
            Codigo = oldTemplate.Codigo,
            Nombre = oldTemplate.Nombre,
            Supervisa = oldTemplate.Supervisa,
            Proceso = oldTemplate.Proceso,
            HeaderFields = oldTemplate.HeaderFields,
            BodyElements = oldTemplate.BodyElements,
            Firmas = oldTemplate.Firmas,
            CreatedAt = DateTime.Now,
            ChangeDescription = "Actualización de estructura/datos detectada"
        };
        _context.TemplateVersions.Add(historyEntry);
    }

    template.UpdatedAt = DateTime.Now;
    _context.Entry(template).State = EntityState.Modified;
    await _context.SaveChangesAsync();

    // 🔔 ALERTAS: Notificar a todos los firmantes cuando cambia la versión
    if (cambioAlgo)
    {
        try
        {
            var firmasJson = template.Firmas;
            if (!string.IsNullOrEmpty(firmasJson))
            {
                var firmas = JsonSerializer.Deserialize<List<FirmaAlertInfo>>(firmasJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (firmas != null)
                {
                    var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var f in firmas)
                    {
                        if (!string.IsNullOrWhiteSpace(f.NombreCompleto)) nombres.Add(f.NombreCompleto);
                        if (f.JefeAlerta != null)
                            foreach (var j in f.JefeAlerta)
                                if (!string.IsNullOrWhiteSpace(j)) nombres.Add(j);
                        if (f.Reemplazos != null)
                            foreach (var r in f.Reemplazos)
                                if (!string.IsNullOrWhiteSpace(r)) nombres.Add(r);
                    }

                    if (nombres.Count > 0)
                    {
                        var subject = $"🔄 Plantilla Actualizada: {template.Codigo} - {template.Nombre} (v{template.Version})";
                        var body = $"<html><body style='font-family:Arial;padding:20px;'>"
                            + $"<div style='background:#1e40af;color:white;padding:20px;border-radius:8px 8px 0 0;'>"
                            + $"<h2 style='margin:0;'>🔄 Modificación de Plantilla</h2></div>"
                            + $"<div style='border:1px solid #e5e7eb;padding:20px;border-radius:0 0 8px 8px;'>"
                            + $"<p><strong>Código:</strong> {template.Codigo}</p>"
                            + $"<p><strong>Nombre:</strong> {template.Nombre}</p>"
                            + $"<p><strong>Nueva Versión:</strong> {template.Version}</p>"
                            + $"<p><strong>Fecha:</strong> {DateTime.Now:dd/MM/yyyy HH:mm}</p>"
                            + $"<p><strong>Motivo:</strong> Actualización de estructura/datos detectada</p>"
                            + $"<hr style='border:1px solid #e5e7eb;'/>"
                            + $"<p style='color:#6b7280;font-size:12px;'>Este correo se genera automáticamente cuando se modifica una plantilla. Por favor revise los cambios.</p>"
                            + $"</div></body></html>";

                        // Buscar emails reales de los nombres via CatalogoFirmas
                        var catalogo = await _context.CatalogoFirmas.ToListAsync();

                        foreach (var nombre in nombres)
                        {
                            var entry = catalogo.FirstOrDefault(c =>
                                (c.NombreCompleto ?? "").Equals(nombre, StringComparison.OrdinalIgnoreCase));
                            var correo = entry?.Correo;
                            if (!string.IsNullOrWhiteSpace(correo))
                            {
                                await _emailService.SendAlertEmailAsync(correo, subject, body);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log silencioso - no bloquear el guardado por fallo de email
            _logger.LogWarning(ex, "Error enviando alertas de versión para plantilla {Id}", id);
        }
    }

    return NoContent();
}
        //==============================================================
        // MÉTODO DELETE - Para borrar una plantilla
        // URL: DELETE /api/Templates/5
        //==============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTemplate(int id)
        {
            var template = await _context.Templates.FindAsync(id);
            if (template == null)
            {
                return NotFound();
            }

            // Obtener IDs de formularios llenados que pertenecen a esta plantilla
            var relatedFormIds = await _context.FilledForms
                .Where(f => f.TemplateID == id)
                .Select(f => f.FormID)
                .ToListAsync();

            if (relatedFormIds.Any())
            {
                // Eliminar alertas que apuntan a estos formularios (no tienen CASCADE)
                var relatedAlerts = await _context.Alerts
                    .Where(a => a.FormId != null && relatedFormIds.Contains(a.FormId.Value))
                    .ToListAsync();
                if (relatedAlerts.Any())
                    _context.Alerts.RemoveRange(relatedAlerts);

                // Eliminar firmas relacionadas
                var relatedSignatures = await _context.Signatures
                    .Where(s => relatedFormIds.Contains(s.FilledFormId))
                    .ToListAsync();
                if (relatedSignatures.Any())
                    _context.Signatures.RemoveRange(relatedSignatures);

                // Eliminar rechazos de firma relacionados
                var relatedRejections = await _context.SignatureRejections
                    .Where(r => relatedFormIds.Contains(r.FilledFormId))
                    .ToListAsync();
                if (relatedRejections.Any())
                    _context.SignatureRejections.RemoveRange(relatedRejections);

                // Eliminar los formularios llenados
                var relatedForms = await _context.FilledForms
                    .Where(f => f.TemplateID == id)
                    .ToListAsync();
                _context.FilledForms.RemoveRange(relatedForms);
            }

            // Eliminar borradores asociados a esta plantilla
            var relatedDrafts = await _context.FormDrafts
                .Where(d => d.TemplateID == id)
                .ToListAsync();
            if (relatedDrafts.Any())
                _context.FormDrafts.RemoveRange(relatedDrafts);

            // Eliminar versiones del template
            var relatedVersions = await _context.TemplateVersions
                .Where(v => v.TemplateID == id)
                .ToListAsync();
            if (relatedVersions.Any())
                _context.TemplateVersions.RemoveRange(relatedVersions);

            _context.Templates.Remove(template);
            await _context.SaveChangesAsync();

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
        // Obtiene el historial de todas las versiones de un template
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

            // ✅ NUEVO: Obtener el historial desde la tabla TemplateVersions
            var versionHistory = await _context.TemplateVersions
                .Where(tv => tv.TemplateID == id)
                .GroupBy(tv => tv.Version)
                .Select(g => new TemplateVersionHistoryDto
                {
                    Version = g.Key,
                    VersionCreatedAt = g.Max(tv => tv.CreatedAt),
                    FechaVersion = g.OrderByDescending(tv => tv.CreatedAt).FirstOrDefault()!.FechaVersion, // ✅ Fecha de versión
                    ChangeDescription = g.OrderByDescending(tv => tv.CreatedAt).FirstOrDefault()!.ChangeDescription,
                    IsCurrentVersion = false, // Se marcará después
                    // Contar formularios que usan esta versión
                    FormCount = _context.FilledForms.Count(f => f.TemplateID == id && f.TemplateVersion == g.Key),
                    FirstUsedDate = _context.FilledForms
                        .Where(f => f.TemplateID == id && f.TemplateVersion == g.Key)
                        .Min(f => (DateTime?)f.CreatedAt),
                    LastUsedDate = _context.FilledForms
                        .Where(f => f.TemplateID == id && f.TemplateVersion == g.Key)
                        .Max(f => (DateTime?)f.CreatedAt)
                })
                .ToListAsync();

            // ✅ NUEVO: Agregar la versión actual si no está en el historial
            var currentVersionExists = versionHistory.Any(v => v.Version == currentVersion);
            if (!currentVersionExists)
            {
                versionHistory.Insert(0, new TemplateVersionHistoryDto
                {
                    Version = currentVersion,
                    VersionCreatedAt = currentTemplate?.UpdatedAt ?? currentTemplate?.CreatedAt,
                    FechaVersion = currentTemplate?.FechaVersion, // ✅ Fecha de versión actual
                    ChangeDescription = "Versión actual en uso",
                    IsCurrentVersion = true,
                    FormCount = _context.FilledForms.Count(f => f.TemplateID == id && f.TemplateVersion == currentVersion),
                    FirstUsedDate = _context.FilledForms
                        .Where(f => f.TemplateID == id && f.TemplateVersion == currentVersion)
                        .Min(f => (DateTime?)f.CreatedAt),
                    LastUsedDate = _context.FilledForms
                        .Where(f => f.TemplateID == id && f.TemplateVersion == currentVersion)
                        .Max(f => (DateTime?)f.CreatedAt)
                });
            }
            else
            {
                // Marcar la versión actual
                var current = versionHistory.First(v => v.Version == currentVersion);
                current.IsCurrentVersion = true;
            }

            // Ordenar por fecha de creación descendente (más reciente primero)
            versionHistory = versionHistory.OrderByDescending(v => v.VersionCreatedAt).ToList();

            // Si no hay ninguna versión (plantilla completamente nueva sin historial)
            if (!versionHistory.Any())
            {
                versionHistory.Add(new TemplateVersionHistoryDto
                {
                    Version = currentVersion,
                    VersionCreatedAt = currentTemplate?.CreatedAt,
                    ChangeDescription = "Versión inicial",
                    FirstUsedDate = null,
                    LastUsedDate = null,
                    FormCount = 0,
                    IsCurrentVersion = true
                });
            }

            return Ok(versionHistory);
        }

        
        [HttpGet("{id}/versions/{version}")]
        public async Task<ActionResult<TemplateVersionDetailDto>> GetVersionDetail(int id, string version)
        {
            // Obtener template actual
            var currentTemplate = await _context.Templates.FindAsync(id);
            if (currentTemplate == null)
            {
                return NotFound(new { message = $"Template con ID {id} no encontrado" });
            }

            // ✅ NUEVO: Buscar en TemplateVersions primero
            var versionSnapshot = await _context.TemplateVersions
                .Where(tv => tv.TemplateID == id && tv.Version == version)
                .OrderByDescending(tv => tv.CreatedAt)
                .FirstOrDefaultAsync();

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
                Dictionary<string, object>? headerData = null;
                var firstForm = formsWithVersion.FirstOrDefault();
                if (firstForm?.HeaderData != null)
                {
                    try
                    {
                        headerData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(firstForm.HeaderData);
                    }
                    catch { }
                }

                return Ok(new TemplateVersionDetailDto
                {
                    Version = currentTemplate.Version ?? "1.0",
                    TemplateID = currentTemplate.TemplateID,
                    Codigo = currentTemplate.Codigo ?? "",
                    Nombre = currentTemplate.Nombre ?? "",
                    Supervisa = currentTemplate.Supervisa,
                    Proceso = currentTemplate.Proceso,
                    HeaderFields = currentTemplate.HeaderFields,
                    BodyElements = currentTemplate.BodyElements,
                    Firmas = currentTemplate.Firmas,
                    AssociatedForms = formsWithVersion,
                    HeaderFieldsData = headerData
                });
            }

            // ✅ NUEVO: Si existe snapshot en TemplateVersions, usar ese
            if (versionSnapshot != null)
            {
                Dictionary<string, object>? headerData = null;
                var firstForm = formsWithVersion.FirstOrDefault();
                if (firstForm?.HeaderData != null)
                {
                    try
                    {
                        headerData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(firstForm.HeaderData);
                    }
                    catch { }
                }

                return Ok(new TemplateVersionDetailDto
                {
                    Version = versionSnapshot.Version,
                    TemplateID = versionSnapshot.TemplateID,
                    Codigo = versionSnapshot.Codigo,
                    Nombre = versionSnapshot.Nombre,
                    Supervisa = versionSnapshot.Supervisa,
                    Proceso = versionSnapshot.Proceso,
                    HeaderFields = versionSnapshot.HeaderFields,
                    BodyElements = versionSnapshot.BodyElements,
                    Firmas = versionSnapshot.Firmas,
                    AssociatedForms = formsWithVersion,
                    HeaderFieldsData = headerData
                });
            }

            // Fallback: buscar en el snapshot del primer formulario (sistema anterior)
            var firstFormWithVersion = await _context.FilledForms
                .Where(f => f.TemplateID == id && f.TemplateVersion == version && f.TemplateSnapshot != null)
                .OrderBy(f => f.CreatedAt)
                .FirstOrDefaultAsync();

            if (firstFormWithVersion?.TemplateSnapshot != null)
            {
                try
                {
                    var snapshot = System.Text.Json.JsonSerializer.Deserialize<Template>(firstFormWithVersion.TemplateSnapshot);
                    
                    Dictionary<string, object>? headerData = null;
                    if (firstFormWithVersion.HeaderData != null)
                    {
                        try
                        {
                            headerData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(firstFormWithVersion.HeaderData);
                        }
                        catch { }
                    }
                    
                    if (snapshot != null)
                    {
                        return Ok(new TemplateVersionDetailDto
                        {
                            Version = version,
                            TemplateID = id,
                            Codigo = snapshot.Codigo ?? "",
                            Nombre = snapshot.Nombre ?? "",
                            Supervisa = snapshot.Supervisa,
                            Proceso = snapshot.Proceso,
                            HeaderFields = snapshot.HeaderFields,
                            BodyElements = snapshot.BodyElements,
                            Firmas = snapshot.Firmas,
                            AssociatedForms = formsWithVersion,
                            HeaderFieldsData = headerData // ⬅️ NUEVO
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
                Supervisa = "Snapshot no disponible - versión histórica",
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

    var currentTemplate = await _context.Templates.FindAsync(id);
    if (currentTemplate == null)
    {
        return NotFound(new { message = $"Template con ID {id} no encontrado" });
    }

    // ✅ Resolvemos el error CS0103: Definimos oldData y newData
    var oldData = await GetVersionDetailInternal(id, oldVersion, currentTemplate);
    var newData = await GetVersionDetailInternal(id, newVersion, currentTemplate);

    if (oldData == null || newData == null)
    {
        return NotFound(new { message = "Una o ambas versiones no encontradas" });
    }

    var comparison = new VersionComparisonDto
    {
        OldVersion = oldVersion,
        NewVersion = newVersion,
        ComparisonDate = DateTime.Now,
        Changes = new List<string>(),
        DetailedChanges = new DetailedChanges()
    };

    // 1. Comparar Metadatos
    if (oldData.Nombre != newData.Nombre)
        comparison.DetailedChanges.MetadataChanges.Add($"Nombre modificado: '{oldData.Nombre}' → '{newData.Nombre}'");

    if (oldData.Supervisa != newData.Supervisa)
        comparison.DetailedChanges.MetadataChanges.Add($"Supervisa: Qui�n supervisa ha cambiado ha cambiado.");

    // 2. Comparar HeaderFields usando tu método existente
    var headerChanges = CompareHeaderFields(oldData.HeaderFields, newData.HeaderFields);
    if (headerChanges.Any())
    {
        comparison.DetailedChanges.HeaderFieldsChanges.AddRange(headerChanges);
        comparison.Changes.Add($"Campos de encabezado: {headerChanges.Count} cambio(s)");
    }

    // 3. Comparar BodyElements usando tu método existente
    var bodyChanges = CompareBodyElements(oldData.BodyElements, newData.BodyElements);
    if (bodyChanges.Any())
    {
        comparison.DetailedChanges.BodyElementsChanges.AddRange(bodyChanges);
        comparison.Changes.Add($"Estructura de tablas: {bodyChanges.Count} cambio(s)");
    }

    // 4. ✅ NUEVO: Comparar Firmas
    var signatureChanges = CompareSignatures(oldData.Firmas, newData.Firmas);
    if (signatureChanges.Any())
    {
        comparison.DetailedChanges.SignaturesChanges.AddRange(signatureChanges);
        comparison.Changes.Add($"Firmas: {signatureChanges.Count} cambio(s)");
    }

    if (!comparison.Changes.Any()) comparison.Changes.Add("No se detectaron cambios");

    return Ok(comparison);
}

// Helper para comparar las Firmas


        // =====================================================
        // MÉTODO HELPER: Comparar HeaderFields detalladamente
        // =====================================================
        private List<FieldChangeDto> CompareHeaderFields(string? oldHeaderFieldsJson, string? newHeaderFieldsJson)
        {
            var changes = new List<FieldChangeDto>();

            try
            {
                // Parsear ambos JSONs
                var oldFields = string.IsNullOrEmpty(oldHeaderFieldsJson) 
                    ? new List<Dictionary<string, object>>() 
                    : System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(oldHeaderFieldsJson) ?? new List<Dictionary<string, object>>();

                var newFields = string.IsNullOrEmpty(newHeaderFieldsJson) 
                    ? new List<Dictionary<string, object>>() 
                    : System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(newHeaderFieldsJson) ?? new List<Dictionary<string, object>>();

                // ✅ CORREGIDO: Usar 'label' como clave única (no 'name')
                var oldFieldsDict = new Dictionary<string, Dictionary<string, object>>();
                foreach (var field in oldFields)
                {
                    var label = field.ContainsKey("label") && field["label"] != null ? field["label"].ToString() ?? "" : "";
                    if (!string.IsNullOrEmpty(label) && !oldFieldsDict.ContainsKey(label))
                    {
                        oldFieldsDict[label] = field;
                    }
                }

                var newFieldsDict = new Dictionary<string, Dictionary<string, object>>();
                foreach (var field in newFields)
                {
                    var label = field.ContainsKey("label") && field["label"] != null ? field["label"].ToString() ?? "" : "";
                    if (!string.IsNullOrEmpty(label) && !newFieldsDict.ContainsKey(label))
                    {
                        newFieldsDict[label] = field;
                    }
                }

                // Detectar campos AGREGADOS
                foreach (var fieldLabel in newFieldsDict.Keys)
                {
                    if (!oldFieldsDict.ContainsKey(fieldLabel))
                    {
                        var field = newFieldsDict[fieldLabel];
                        var type = field.ContainsKey("type") && field["type"] != null ? field["type"].ToString() : "text";

                        changes.Add(new FieldChangeDto
                        {
                            ChangeType = "added",
                            FieldName = fieldLabel,
                            NewValue = $"{fieldLabel} ({type})",
                            Description = $"Campo agregado: '{fieldLabel}' (tipo: {type})"
                        });
                    }
                }

                // Detectar campos ELIMINADOS
                foreach (var fieldLabel in oldFieldsDict.Keys)
                {
                    if (!newFieldsDict.ContainsKey(fieldLabel))
                    {
                        changes.Add(new FieldChangeDto
                        {
                            ChangeType = "removed",
                            FieldName = fieldLabel,
                            OldValue = fieldLabel,
                            Description = $"Campo eliminado: '{fieldLabel}'"
                        });
                    }
                }

                // Detectar campos MODIFICADOS
                foreach (var fieldLabel in newFieldsDict.Keys)
                {
                    if (oldFieldsDict.ContainsKey(fieldLabel))
                    {
                        var oldField = oldFieldsDict[fieldLabel];
                        var newField = newFieldsDict[fieldLabel];

                        var modifications = new List<string>();

                        // Comparar type
                        var oldType = oldField.ContainsKey("type") && oldField["type"] != null ? oldField["type"].ToString() : "text";
                        var newType = newField.ContainsKey("type") && newField["type"] != null ? newField["type"].ToString() : "text";
                        if (oldType != newType)
                        {
                            modifications.Add($"tipo: '{oldType}' → '{newType}'");
                        }

                        // Comparar required
                        var oldRequired = oldField.ContainsKey("required") && oldField["required"] != null && oldField["required"].ToString()?.ToLower() == "true";
                        var newRequired = newField.ContainsKey("required") && newField["required"] != null && newField["required"].ToString()?.ToLower() == "true";
                        if (oldRequired != newRequired)
                        {
                            modifications.Add($"requerido: {(oldRequired ? "Sí" : "No")} → {(newRequired ? "Sí" : "No")}");
                        }

                        if (modifications.Any())
                        {
                            changes.Add(new FieldChangeDto
                            {
                                ChangeType = "modified",
                                FieldName = fieldLabel,
                                Description = $"Campo modificado '{fieldLabel}': {string.Join(", ", modifications)}"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                changes.Add(new FieldChangeDto
                {
                    ChangeType = "error",
                    FieldName = "HeaderFields",
                    Description = $"Error al comparar campos de encabezado: {ex.Message}"
                });
            }

            return changes;
        }

        // =====================================================
        // MÉTODO HELPER: Comparar BodyElements detalladamente
        // =====================================================
        private List<FieldChangeDto> CompareBodyElements(string? oldBodyElementsJson, string? newBodyElementsJson)
        {
            var changes = new List<FieldChangeDto>();

            try
            {
                // Parsear ambos JSONs
                using var oldDoc = string.IsNullOrEmpty(oldBodyElementsJson) 
                    ? System.Text.Json.JsonDocument.Parse("[]")
                    : System.Text.Json.JsonDocument.Parse(oldBodyElementsJson);
                
                using var newDoc = string.IsNullOrEmpty(newBodyElementsJson) 
                    ? System.Text.Json.JsonDocument.Parse("[]")
                    : System.Text.Json.JsonDocument.Parse(newBodyElementsJson);

                var oldElements = oldDoc.RootElement;
                var newElements = newDoc.RootElement;

                // Obtener todas las columnas de ambas versiones
                var oldColumns = new Dictionary<string, string>(); // label -> type
                var newColumns = new Dictionary<string, string>(); // label -> type

                // Extraer columnas de versión antigua
                foreach (var element in oldElements.EnumerateArray())
                {
                    if (element.TryGetProperty("columns", out var columns))
                    {
                        foreach (var column in columns.EnumerateArray())
                        {
                            var label = column.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? "" : "";
                            var type = column.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "text" : "text";
                            
                            if (!string.IsNullOrEmpty(label) && !oldColumns.ContainsKey(label))
                            {
                                oldColumns[label] = type;
                            }
                        }
                    }
                    // También verificar 'fields' en secciones
                    if (element.TryGetProperty("fields", out var fields))
                    {
                        foreach (var field in fields.EnumerateArray())
                        {
                            var label = field.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? "" : "";
                            var type = field.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "text" : "text";
                            
                            if (!string.IsNullOrEmpty(label) && !oldColumns.ContainsKey(label))
                            {
                                oldColumns[label] = type;
                            }
                        }
                    }
                }

                // Extraer columnas de versión nueva
                foreach (var element in newElements.EnumerateArray())
                {
                    if (element.TryGetProperty("columns", out var columns))
                    {
                        foreach (var column in columns.EnumerateArray())
                        {
                            var label = column.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? "" : "";
                            var type = column.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "text" : "text";
                            
                            if (!string.IsNullOrEmpty(label) && !newColumns.ContainsKey(label))
                            {
                                newColumns[label] = type;
                            }
                        }
                    }
                    // También verificar 'fields' en secciones
                    if (element.TryGetProperty("fields", out var fields))
                    {
                        foreach (var field in fields.EnumerateArray())
                        {
                            var label = field.TryGetProperty("label", out var labelProp) ? labelProp.GetString() ?? "" : "";
                            var type = field.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "text" : "text";
                            
                            if (!string.IsNullOrEmpty(label) && !newColumns.ContainsKey(label))
                            {
                                newColumns[label] = type;
                            }
                        }
                    }
                }

                // Detectar columnas AGREGADAS
                foreach (var columnLabel in newColumns.Keys)
                {
                    if (!oldColumns.ContainsKey(columnLabel))
                    {
                        var type = newColumns[columnLabel];
                        changes.Add(new FieldChangeDto
                        {
                            ChangeType = "added",
                            FieldName = columnLabel,
                            NewValue = $"{columnLabel} ({type})",
                            Description = $"Columna agregada: '{columnLabel}' (tipo: {type})"
                        });
                    }
                }

                // Detectar columnas ELIMINADAS
                foreach (var columnLabel in oldColumns.Keys)
                {
                    if (!newColumns.ContainsKey(columnLabel))
                    {
                        changes.Add(new FieldChangeDto
                        {
                            ChangeType = "removed",
                            FieldName = columnLabel,
                            OldValue = columnLabel,
                            Description = $"Columna eliminada: '{columnLabel}'"
                        });
                    }
                }

                // Detectar columnas MODIFICADAS
                foreach (var columnLabel in newColumns.Keys)
                {
                    if (oldColumns.ContainsKey(columnLabel))
                    {
                        var oldType = oldColumns[columnLabel];
                        var newType = newColumns[columnLabel];
                        
                        if (oldType != newType)
                        {
                            changes.Add(new FieldChangeDto
                            {
                                ChangeType = "modified",
                                FieldName = columnLabel,
                                OldValue = oldType,
                                NewValue = newType,
                                Description = $"Columna modificada '{columnLabel}': tipo '{oldType}' → '{newType}'"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                changes.Add(new FieldChangeDto
                {
                    ChangeType = "error",
                    FieldName = "BodyElements",
                    Description = $"Error al comparar elementos del cuerpo: {ex.Message}"
                });
            }

            return changes;
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
                    Supervisa = currentTemplate.Supervisa,
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
                            Supervisa = snapshot.Supervisa,
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
                Supervisa = "Snapshot no disponible - versión histórica",
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
        // Método para comparar firmas detalladamente
private List<FieldChangeDto> CompareSignatures(string? json1, string? json2)
{
    var changes = new List<FieldChangeDto>();
    try 
    {
        // ✅ Resolvemos error JsonSerializer
        var list1 = string.IsNullOrEmpty(json1) ? new List<FirmaItem>() : JsonSerializer.Deserialize<List<FirmaItem>>(json1, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var list2 = string.IsNullOrEmpty(json2) ? new List<FirmaItem>() : JsonSerializer.Deserialize<List<FirmaItem>>(json2, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        foreach (var item in list2 ?? new()) {
            if (!list1?.Any(x => x.puesto == item.puesto) ?? true)
                changes.Add(new FieldChangeDto { ChangeType = "added", FieldName = item.puesto, Description = $"✅ Firma agregada para: {item.puesto}" });
        }
        foreach (var item in list1 ?? new()) {
            if (!list2?.Any(x => x.puesto == item.puesto) ?? true)
                changes.Add(new FieldChangeDto { ChangeType = "removed", FieldName = item.puesto, Description = $"❌ Firma eliminada: {item.puesto}" });
        }
    } catch { }
    return changes;
}

// Clase interna para procesar el JSON de firmas
public class FirmaItem { public string puesto { get; set; } = ""; }
    }
}


