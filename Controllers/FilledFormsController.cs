using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services; // ✅ Para IEmailService
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilledFormsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FilledFormsController> _logger; // ✅ Para logging
        private readonly IEmailService _emailService; // ✅ Para enviar emails

        public FilledFormsController(
            ApplicationDbContext context,
            ILogger<FilledFormsController> logger,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetFilledForms()
        {
            var forms = await _context.FilledForms
                                 .Include(f => f.Template)
                                 .OrderByDescending(f => f.FechaRegistro).ThenByDescending(f => f.CreatedAt)
                                 .ToListAsync();
            
            // Mapear a objeto anónimo con el nombre del template y datos de auditoría
            var result = forms.Select(f => new
            {
                f.FormID,
                f.TemplateID,
                TemplateName = f.Template?.Nombre ?? "Sin nombre",
                f.TemplateVersion,
                f.FechaVersion,
                
                // ✅ AUDITORÍA
                f.FilledBy,
                f.FilledByEmail,
                f.FilledByRole,
                
                // 🦐🐟 TIPO DE PRODUCTO
                f.TipoProducto,
                
                f.HeaderData,
                f.BodyData,
                f.FirmasData,
                f.Observaciones,
                f.CreatedAt,
                f.UpdatedAt
            });
            
            return Ok(result);
        }

        // 🆕 ENDPOINT LIGERO: Lista de formularios sin datos pesados (para importador de columnas)
        [HttpGet("list")]
        public async Task<ActionResult<IEnumerable<object>>> GetFilledFormsList()
        {
            var forms = await _context.FilledForms
                                 .Include(f => f.Template)
                                 .OrderByDescending(f => f.FechaRegistro).ThenByDescending(f => f.CreatedAt)
                                 .Take(100)
                                 .Select(f => new
                                 {
                                     f.FormID,
                                     f.TemplateID,
                                     TemplateName = f.Template != null ? f.Template.Nombre : "Sin nombre",
                                     f.TipoProducto,
                                     f.CreatedAt,
                                     f.UpdatedAt,
                                     f.FilledBy
                                 })
                                 .ToListAsync();
            
            return Ok(forms);
        }

        /// <summary>
        /// Busca registros para poder saltar de uno a otro mientras se edita.
        ///
        /// Antes la pantalla de edición se bajaba TODOS los formularios con su HeaderData,
        /// BodyData y FirmasData completos solo para poder filtrar por código en el navegador:
        /// con la cantidad de registros que ya hay, eso tarda muchísimo o directamente no
        /// termina, y el buscador quedaba inservible. Acá se filtra en la base y se devuelve
        /// lo justo para mostrar la lista.
        ///
        /// Busca por código y nombre de la plantilla, por quién lo llenó y por lote.
        /// </summary>
        [HttpGet("buscar")]
        public async Task<ActionResult<IEnumerable<object>>> BuscarFilledForms(
            [FromQuery] string? q = null,
            [FromQuery] int? templateId = null,
            [FromQuery] int limite = 50)
        {
            try
            {
                if (limite <= 0 || limite > 200) limite = 50;

                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsNoTracking()
                    .AsQueryable();

                if (templateId.HasValue && templateId.Value > 0)
                    query = query.Where(f => f.TemplateID == templateId.Value);

                if (!string.IsNullOrWhiteSpace(q))
                {
                    var texto = q.Trim();
                    query = query.Where(f =>
                        (f.Template != null && f.Template.Codigo.Contains(texto)) ||
                        (f.Template != null && f.Template.Nombre.Contains(texto)) ||
                        (f.FilledBy != null && f.FilledBy.Contains(texto)) ||
                        // El lote y la fecha del operario viven dentro del encabezado
                        (f.HeaderData != null && f.HeaderData.Contains(texto)));
                }

                var forms = await query
                    .OrderByDescending(f => f.FechaRegistro).ThenByDescending(f => f.CreatedAt)
                    .Take(limite)
                    .Select(f => new
                    {
                        f.FormID,
                        f.TemplateID,
                        Codigo = f.Template != null ? f.Template.Codigo : "N/A",
                        TemplateName = f.Template != null ? f.Template.Nombre : "Sin nombre",
                        f.FilledBy,
                        f.CreatedAt,
                        f.FechaRegistro,
                        f.UpdatedAt,
                        f.HeaderData,   // solo para sacar el lote; se descarta en el cliente
                    })
                    .ToListAsync();

                // El lote se saca acá para no mandar el encabezado entero al navegador
                var result = forms.Select(f => new
                {
                    f.FormID,
                    f.TemplateID,
                    f.Codigo,
                    f.TemplateName,
                    f.FilledBy,
                    f.CreatedAt,
                    f.UpdatedAt,
                    Lote = ExtraerLote(f.HeaderData),
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al buscar formularios");
                return StatusCode(500, new { message = "Error al buscar formularios" });
            }
        }

        /// <summary>
        /// Primer campo del encabezado que se llame "lote" y traiga algo. Sirve para
        /// distinguir dos registros de la misma plantilla en la lista de búsqueda.
        /// </summary>
        private static string? ExtraerLote(string? headerData)
        {
            if (string.IsNullOrWhiteSpace(headerData)) return null;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerData);
                if (header == null) return null;

                foreach (var kvp in header)
                {
                    if (kvp.Key.IndexOf("lote", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (kvp.Value.ValueKind != JsonValueKind.String) continue;

                    var valor = kvp.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(valor)) return valor.Trim();
                }
            }
            catch (JsonException)
            {
                // Encabezado ilegible: la lista se muestra igual, solo sin el lote.
            }

            return null;
        }

        // ── ¿ESTE CÓDIGO YA SE USÓ EN OTRO FORMULARIO? ──────────────────────
        /// <summary>
        /// Dice cuáles de los códigos enviados ya están en OTRO formulario del
        /// mismo tipo, mirando tanto los guardados como los BORRADORES.
        ///
        /// El bloqueo que había en el navegador solo comparaba contra el
        /// formulario abierto y contra el "usado" de la API externa. Como los
        /// borradores no marcan nada en esa API, dos personas (o la misma en
        /// dos pestañas) podían meter el mismo código de materia prima y recién
        /// se descubría al revisar los reportes: pasó el 24/08 con el código
        /// A26235-002-066, que quedó en el PD-04 de Blue Marlin y en el de
        /// Swordfish. Este endpoint es la fuente de verdad que faltaba.
        ///
        /// GET api/FilledForms/codigos-en-uso?codigos=A26235-002-066,A26236-001-112
        ///     &amp;templateId=101&amp;excluirFormId=1776&amp;excluirDraftId=12&amp;dias=60
        /// </summary>
        [HttpGet("codigos-en-uso")]
        public async Task<ActionResult<object>> GetCodigosEnUso(
            [FromQuery] string? codigos,
            [FromQuery] int? templateId = null,
            [FromQuery] int? excluirFormId = null,
            [FromQuery] int? excluirDraftId = null,
            [FromQuery] int dias = 60)
        {
            var buscados = (codigos ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();

            if (buscados.Count == 0) return Ok(new { usados = Array.Empty<object>() });

            try
            {
                // La ventana de fechas acota el trabajo sin perder el caso real:
                // el choque siempre es entre formularios de estos días.
                var desde = DateTime.Now.AddDays(-Math.Abs(dias));
                var indice = new HashSet<string>(buscados, StringComparer.OrdinalIgnoreCase);
                var usados = new List<object>();

                var formsQuery = _context.FilledForms.AsNoTracking().Where(f => f.CreatedAt >= desde);
                if (templateId.HasValue) formsQuery = formsQuery.Where(f => f.TemplateID == templateId.Value);
                if (excluirFormId.HasValue) formsQuery = formsQuery.Where(f => f.FormID != excluirFormId.Value);

                var forms = await formsQuery
                    .Select(f => new { f.FormID, f.TemplateID, f.HeaderData, f.BodyData, f.FilledBy, f.CreatedAt })
                    .ToListAsync();

                foreach (var f in forms)
                {
                    foreach (var cod in CodigosDelCuerpo(f.BodyData, indice))
                    {
                        var (especie, lote) = EspecieYLote(f.HeaderData);
                        usados.Add(new
                        {
                            codigo = cod, origen = "formulario", id = f.FormID,
                            templateId = f.TemplateID, especie, lote,
                            usuario = f.FilledBy, fecha = f.CreatedAt,
                        });
                    }
                }

                // Solo borradores VIVOS. Al guardar un borrador como formulario, la
                // fila del borrador no se borra: queda con IsActive = false (borrado
                // lógico). Sin este filtro el mismo código se informaba dos veces —
                // una como "formulario" y otra como "borrador" del que salió— y el
                // consumo quedaba duplicado. Los vencidos (7 días) tampoco cuentan:
                // ya no los puede retomar nadie.
                var draftsQuery = _context.FormDrafts.AsNoTracking()
                    .Where(d => d.CreatedAt >= desde && d.IsActive && d.ExpiresAt > DateTime.Now);
                if (templateId.HasValue) draftsQuery = draftsQuery.Where(d => d.TemplateID == templateId.Value);
                if (excluirDraftId.HasValue) draftsQuery = draftsQuery.Where(d => d.DraftID != excluirDraftId.Value);

                var drafts = await draftsQuery
                    .Select(d => new { d.DraftID, d.TemplateID, d.HeaderData, d.BodyData, d.UserName, d.CreatedAt })
                    .ToListAsync();

                foreach (var d in drafts)
                {
                    foreach (var cod in CodigosDelCuerpo(d.BodyData, indice))
                    {
                        var (especie, lote) = EspecieYLote(d.HeaderData);
                        usados.Add(new
                        {
                            codigo = cod, origen = "borrador", id = d.DraftID,
                            templateId = d.TemplateID, especie, lote,
                            usuario = d.UserName, fecha = d.CreatedAt,
                        });
                    }
                }

                return Ok(new { usados, revisados = forms.Count + drafts.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando códigos en uso");
                // Que falle esta consulta no puede impedir guardar: el front lo
                // toma como "no se pudo verificar" y sigue.
                return StatusCode(500, new { message = "No se pudieron verificar los códigos" });
            }
        }

        /// <summary>
        /// Los códigos buscados que aparecen en el cuerpo de un formulario.
        /// Se recorren TODOS los valores del JSON (los códigos viven en
        /// columnas distintas según la plantilla) y se cruzan contra el índice.
        /// </summary>
        private static IEnumerable<string> CodigosDelCuerpo(string? bodyData, HashSet<string> buscados)
        {
            if (string.IsNullOrWhiteSpace(bodyData)) yield break;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(bodyData); }
            catch { yield break; }   // cuerpo ilegible: no se opina

            var encontrados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (doc)
            {
                var pila = new Stack<JsonElement>();
                pila.Push(doc.RootElement);
                while (pila.Count > 0)
                {
                    var el = pila.Pop();
                    switch (el.ValueKind)
                    {
                        case JsonValueKind.Object:
                            foreach (var prop in el.EnumerateObject()) pila.Push(prop.Value);
                            break;
                        case JsonValueKind.Array:
                            foreach (var item in el.EnumerateArray()) pila.Push(item);
                            break;
                        case JsonValueKind.String:
                            var v = el.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(v) && buscados.Contains(v)) encontrados.Add(v);
                            break;
                    }
                }
            }

            foreach (var c in encontrados) yield return c;
        }

        /// <summary>Especie y lote del encabezado, para que el aviso diga dónde está el código.</summary>
        private static (string? especie, string? lote) EspecieYLote(string? headerData)
        {
            if (string.IsNullOrWhiteSpace(headerData)) return (null, null);
            try
            {
                using var doc = JsonDocument.Parse(headerData);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return (null, null);
                string? especie = null, lote = null;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.String) continue;
                    var nombre = prop.Name.Trim().ToLowerInvariant();
                    if (nombre.StartsWith("especie")) especie = prop.Value.GetString();
                    else if (nombre.StartsWith("lote")) lote = prop.Value.GetString();
                }
                return (especie, lote);
            }
            catch { return (null, null); }
        }

        // 🆕 NUEVO ENDPOINT: Obtener datos simples y parseados para importación
        [HttpGet("{id}/simple")]
        public async Task<ActionResult<object>> GetFilledFormSimple(int id)
        {
            Console.WriteLine($"📥 GetFilledFormSimple: Buscando formulario ID={id}");
            
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                Console.WriteLine($"❌ Formulario ID={id} no encontrado");
                return NotFound(new { message = $"Formulario {id} no encontrado" });
            }

            Console.WriteLine($"✅ Formulario encontrado: ID={id}, TemplateID={filledForm.TemplateID}");

            // Parsear HeaderData y BodyData directamente
            object? headerDataParsed = null;
            object? bodyDataParsed = null;

            try {
                if (!string.IsNullOrEmpty(filledForm.HeaderData)) {
                    headerDataParsed = JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                }
            } catch {
                headerDataParsed = new { raw = filledForm.HeaderData };
            }

            try {
                if (!string.IsNullOrEmpty(filledForm.BodyData)) {
                    bodyDataParsed = JsonSerializer.Deserialize<object>(filledForm.BodyData);
                }
            } catch {
                bodyDataParsed = new { raw = filledForm.BodyData };
            }

            // Parsear BodyElements del TemplateSnapshot para obtener títulos de tablas
            object? templateBodyElements = null;
            try {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot)) {
                    using var snapshotDoc = JsonDocument.Parse(filledForm.TemplateSnapshot);
                    if (snapshotDoc.RootElement.TryGetProperty("BodyElements", out var bodyElProp)) {
                        var bodyElStr = bodyElProp.GetString();
                        if (!string.IsNullOrEmpty(bodyElStr)) {
                            templateBodyElements = JsonSerializer.Deserialize<object>(bodyElStr);
                        }
                    }
                }
            } catch {
                // Si falla, intentar con la plantilla directamente
                try {
                    if (filledForm.Template != null && !string.IsNullOrEmpty(filledForm.Template.BodyElements)) {
                        templateBodyElements = JsonSerializer.Deserialize<object>(filledForm.Template.BodyElements);
                    }
                } catch { }
            }

            // Extraer BodyElements RAW del TemplateSnapshot como string
            string? templateBodyElementsRaw = null;
            try {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot)) {
                    using var snapshotDoc2 = JsonDocument.Parse(filledForm.TemplateSnapshot);
                    if (snapshotDoc2.RootElement.TryGetProperty("BodyElements", out var bodyElProp2)) {
                        templateBodyElementsRaw = bodyElProp2.GetString();
                    }
                }
            } catch {
                try {
                    if (filledForm.Template != null) {
                        templateBodyElementsRaw = filledForm.Template.BodyElements;
                    }
                } catch { }
            }

            // Devolver datos simples y directos
            // bodyDataRaw y templateBodyElementsRaw son strings puros que NO pasan por ReferenceHandler.Preserve
            var response = new {
                formID = filledForm.FormID,
                templateID = filledForm.TemplateID,
                templateName = filledForm.Template?.Nombre ?? "Sin nombre",
                templateVersion = filledForm.TemplateVersion,
                headerData = headerDataParsed,
                bodyData = bodyDataParsed,
                bodyDataRaw = filledForm.BodyData ?? "",
                templateBodyElements = templateBodyElements,
                templateBodyElementsRaw = templateBodyElementsRaw ?? "",
                createdAt = filledForm.CreatedAt,
                updatedAt = filledForm.UpdatedAt
            };

            Console.WriteLine($"📤 Retornando datos simples para FormID={id}");
            return Ok(response);
        }
        /// <summary>
        /// GET: api/FilledForms/lista
        ///
        /// Listado liviano para la pantalla "Ver Formularios": trae todo lo que
        /// la lista necesita para pintarse y filtrar, PERO SIN BodyData.
        ///
        /// BodyData es el JSON de todas las tablas del formulario y puede pesar
        /// cientos de KB por registro. La pantalla lo descargaba para los ~1900
        /// formularios y lo descartaba enseguida, porque al abrir uno vuelve a
        /// pedir el completo a /{id}. Eran decenas de megas por cada visita.
        ///
        /// El HeaderData sí viaja: de ahí sale la fecha real del formulario, que
        /// es por la que se filtra, y pesa poco.
        /// </summary>
        [HttpGet("lista")]
        public async Task<ActionResult<object>> GetLista(
            [FromQuery] int? templateId,
            [FromQuery] DateTime? desde,
            [FromQuery] DateTime? hasta,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamano = 200)
        {
            try
            {
                var query = _context.FilledForms
                    .Include(f => f.Template)
                    .AsNoTracking()
                    .AsQueryable();

                if (templateId.HasValue && templateId.Value > 0)
                    query = query.Where(f => f.TemplateID == templateId.Value);

                // Día completo en los dos extremos: sin esto, buscar "del 7 al 7"
                // no devuelve nada porque la fecha llega a las 00:00:00.
                if (desde.HasValue)
                    // Por la fecha del REGISTRO, no la de guardado: el usuario
                    // busca por lo que dice el papel.
                    query = query.Where(f => f.FechaRegistro >= desde.Value.Date);
                if (hasta.HasValue)
                    query = query.Where(f => f.FechaRegistro < hasta.Value.Date.AddDays(1));

                var total = await query.CountAsync();

                var tam = tamano <= 0 ? total : Math.Min(tamano, 1000);
                var pag = Math.Max(pagina, 1);

                var items = await query
                    .OrderByDescending(f => f.FechaRegistro).ThenByDescending(f => f.CreatedAt)
                    .Skip((pag - 1) * tam)
                    .Take(tam)
                    .Select(f => new
                    {
                        f.FormID,
                        f.TemplateID,
                        f.CreatedAt,
                        f.FechaRegistro,
                        f.UpdatedAt,
                        // Se llama FilledBy, no CreatedBy: es el nombre del
                        // operario que llenó el formulario.
                        f.FilledBy,
                        f.FilledByEmail,
                        f.FilledByRole,
                        f.HeaderData,
                        f.FirmasData,
                        TemplateNombre = f.Template != null ? f.Template.Nombre : null,
                        TemplateCodigo = f.Template != null ? f.Template.Codigo : null,
                        TemplateObsoleta = f.Template != null && f.Template.IsObsolete,
                    })
                    .ToListAsync();

                return Ok(new
                {
                    total,
                    pagina = pag,
                    tamano = tam,
                    paginas = tam > 0 ? (int)Math.Ceiling((double)total / tam) : 1,
                    items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al armar el listado liviano de formularios");
                return StatusCode(500, new { message = "Error al cargar el listado." });
            }
        }

/// <summary>
/// Datos para "Descargar Datos". Devuelve el encabezado, el cuerpo y las firmas
/// completos de cada registro, así que un rango largo son muchos megas: por eso
/// se sirve de a tandas (parámetros pagina/tamano) en vez de todo de una.
/// El navegador va pidiendo las tandas hasta juntar el rango completo.
///
/// Con tamano = 0 devuelve todo junto, como antes.
/// </summary>
[HttpGet("erp-report")]
public async Task<ActionResult<object>> GetErpReport(
    [FromQuery] DateTime? inicio,
    [FromQuery] DateTime? fin,
    [FromQuery] int? templateId,
    [FromQuery] string? lote,
    [FromQuery] int pagina = 1,
    [FromQuery] int tamano = 0)
{
    try 
    {
        // 1. Iniciar consulta
        var query = _context.FilledForms
            .Include(f => f.Template)
            .AsNoTracking()
            .AsQueryable();

        // 2. Filtros
        // Por la fecha del REGISTRO en todos los filtros de rango.
        if (inicio.HasValue) query = query.Where(f => f.FechaRegistro >= inicio.Value.Date);
        if (fin.HasValue) 
        {
            var fechaFin = fin.Value.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            query = query.Where(f => f.FechaRegistro <= fechaFin);
        }
        if (templateId.HasValue && templateId.Value > 0)
        {
            query = query.Where(f => f.TemplateID == templateId.Value);
        }
        if (!string.IsNullOrEmpty(lote))
        {
            query = query.Where(f => f.HeaderData.Contains(lote) || f.BodyData.Contains(lote));
        }

        // 3. Ejecutar. Se cuenta primero para que el navegador sepa cuántas
        //    tandas faltan y pueda mostrar el avance.
        var total = await query.CountAsync();

        var ordenada = query.OrderByDescending(f => f.FechaRegistro).ThenByDescending(f => f.CreatedAt);

        // Tope duro por pedido: sin esto un rango de meses trae miles de
        // registros con todo su JSON y tumba la respuesta.
        const int TAMANO_MAXIMO = 500;
        if (tamano > TAMANO_MAXIMO) tamano = TAMANO_MAXIMO;
        if (pagina < 1) pagina = 1;

        var forms = tamano > 0
            ? await ordenada.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync()
            : await ordenada.ToListAsync();

        // 4. Mapeo COMPLETO (Incluyendo Firmas y Observaciones)
        var result = forms.Select(f => new
        {
            formID = f.FormID,
            templateID = f.TemplateID,
            templateName = f.Template?.Nombre ?? "Sin nombre",
            // El código (FOR-PD-14, PD-04…) es como se identifica el formulario en
            // planta; sin él el reporte solo puede mostrar el nombre largo.
            formCode = f.Template?.Codigo ?? "",
            createdAt = f.CreatedAt,
            headerData = f.HeaderData, 
            bodyData = f.BodyData,
            // 👇 ESTOS SON LOS CAMPOS QUE FALTABAN 👇
            firmasData = f.FirmasData,
            observaciones = f.Observaciones
        });

        // Sin paginar se devuelve el array pelado, como venía haciéndose.
        if (tamano <= 0) return Ok(result);

        return Ok(new
        {
            total,
            pagina,
            tamano,
            hayMas = pagina * tamano < total,
            datos = result,
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Error interno: {ex.Message}");
    }
}
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetFilledForm(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound();
            }

            Console.WriteLine($"📋 GetFilledForm ID={id}, CreatedAt={filledForm.CreatedAt:yyyy-MM-dd}, TemplateID={filledForm.TemplateID}");

            // 🎯 LÓGICA DE VERSIONAMIENTO POR FECHA
            object? templateData = null;
            string versionUsada = filledForm.TemplateVersion ?? "1";
            bool versionCorrecta = false;

            // 1️⃣ Determinar qué versión DEBERÍA usar según la fecha de creación
            if (filledForm.CreatedAt != default(DateTime))
            {
                string versionVigente = await GetVersionVigenteEnFecha(filledForm.TemplateID, filledForm.CreatedAt);
                Console.WriteLine($"🔍 Versión vigente en {filledForm.CreatedAt:yyyy-MM-dd}: {versionVigente}");

                // 2️⃣ Si la versión guardada NO coincide con la vigente, buscar la estructura correcta
                if (filledForm.TemplateVersion != versionVigente)
                {
                    Console.WriteLine($"⚠️ Versión guardada ({filledForm.TemplateVersion}) != Versión vigente ({versionVigente})");
                    templateData = await GetTemplateStructureByVersion(filledForm.TemplateID, versionVigente);
                    versionUsada = versionVigente;
                    versionCorrecta = false; // Indica que se corrigió la versión
                }
                else
                {
                    Console.WriteLine($"✅ Versión guardada coincide con la vigente");
                    versionCorrecta = true;
                }
            }

            // 3️⃣ Si no se pudo determinar por fecha, usar snapshot o template actual
            if (templateData == null)
            {
                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
                {
                    try
                    {
                        templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
                        Console.WriteLine($"📸 Usando TemplateSnapshot");
                    }
                    catch
                    {
                        templateData = GetCurrentTemplateData(filledForm.Template);
                        Console.WriteLine($"⚠️ Fallback a template actual (error deserialización)");
                    }
                }
                else
                {
                    templateData = GetCurrentTemplateData(filledForm.Template);
                    Console.WriteLine($"⚠️ Fallback a template actual (sin snapshot)");
                }
            }

            // 4️⃣ Crear respuesta con metadata de versión
            var response = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion, // Versión GUARDADA
                VersionUsada = versionUsada, // Versión REALMENTE usada
                VersionCorrecta = versionCorrecta, // TRUE si la guardada coincide con la vigente
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                HeaderData = filledForm.HeaderData,
                BodyData = filledForm.BodyData,
                FirmasData = filledForm.FirmasData,
                Observaciones = filledForm.Observaciones,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = templateData,
                IsHistorical = !string.IsNullOrEmpty(filledForm.TemplateSnapshot)
            };

            return Ok(response);
        }

        [HttpGet("{id}/edit")]
        public async Task<ActionResult<object>> GetFilledFormForEdit(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound(new { message = "Formulario no encontrado" });
            }

            // VERSIONAMIENTO: Para edición, SIEMPRE usar el snapshot guardado
            object? templateData = null;
            
            if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
            {
                // Usar el snapshot histórico
                try
                {
                    templateData = JsonSerializer.Deserialize<object>(filledForm.TemplateSnapshot);
                }
                catch
                {
                    // Fallback si falla la deserialización
                    templateData = GetCurrentTemplateData(filledForm.Template);
                }
            }
            else
            {
                // Fallback para datos antiguos
                templateData = GetCurrentTemplateData(filledForm.Template);
            }

            // Preparar datos para edición - asegurar que los JSON strings estén correctos
            var editData = new
            {
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion, // Versión guardada
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                HeaderData = !string.IsNullOrEmpty(filledForm.HeaderData) ? filledForm.HeaderData : "{}",
                BodyData = !string.IsNullOrEmpty(filledForm.BodyData) ? filledForm.BodyData : "{}",
                FirmasData = !string.IsNullOrEmpty(filledForm.FirmasData) ? filledForm.FirmasData : "{}",
                Observaciones = filledForm.Observaciones ?? "",
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Template = templateData, // Usar snapshot histórico
                IsHistorical = !string.IsNullOrEmpty(filledForm.TemplateSnapshot)
            };

            return Ok(editData);
        }

        /// <summary>
        /// Fecha del registro leída del encabezado.
        ///
        /// Es la que el operario escribió, no la de guardado. Un formulario del
        /// día 9 puede guardarse el 10, y los filtros tienen que encontrarlo
        /// por el 9, que es lo que dice el papel.
        ///
        /// Se descartan los campos de vencimiento, caducidad y versión: dicen
        /// "fecha" pero no son la del registro.
        /// </summary>
        private static DateTime? FechaDelHeader(string? headerData)
        {
            if (string.IsNullOrWhiteSpace(headerData)) return null;

            try
            {
                var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerData);
                if (header == null) return null;

                foreach (var (clave, valor) in header)
                {
                    var k = clave.ToUpperInvariant();
                    if (!k.Contains("FECHA") && !k.Contains("DATE")) continue;
                    if (k.Contains("VENCIM") || k.Contains("CADUC") || k.Contains("VERSION")
                        || k.Contains("EXPIR") || k.Contains("NACIM")) continue;

                    var texto = valor.ValueKind == JsonValueKind.String
                        ? valor.GetString()
                        : valor.ToString();
                    if (string.IsNullOrWhiteSpace(texto)) continue;

                    if (DateTime.TryParse(texto, System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var fecha))
                        return fecha.Date;

                    // dd/MM/yyyy, que es como se escribe en planta.
                    if (DateTime.TryParseExact(texto.Trim(),
                            new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy" },
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var dmy))
                        return dmy.Date;
                }
            }
            catch
            {
                // HeaderData corrupto: se cae a CreatedAt en el llamador.
            }

            return null;
        }

       [HttpPost]
        public async Task<ActionResult<FilledForm>> PostFilledForm([FromBody] FilledFormInputDto dto)
        {
            // Obtener el template completo para crear el snapshot
            var template = await _context.Templates.FindAsync(dto.TemplateID);
            if (template == null)
            {
                return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
            }

            // VERSIONAMIENTO: Crear snapshot del template al momento de creación
            var templateSnapshot = new
            {
                TemplateID = template.TemplateID,
                Codigo = template.Codigo,
                Nombre = template.Nombre,
                Version = template.Version,
                FechaVersion = template.FechaVersion,
                Objetivo = template.Supervisa,
                Proceso = template.Proceso,
                CuandoSeUsa = template.CuandoSeUsa,
                QuienLoLlena = template.QuienLoLlena,
                HeaderFields = template.HeaderFields,
                BodyElements = template.BodyElements,
                Firmas = template.Firmas,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
            
            // Mapeo actualizado para usar BodyData y guardar snapshot
            var filledForm = new FilledForm
            {
                TemplateID = dto.TemplateID,
                TemplateVersion = template.Version, // Guardar la versión específica usada
                TemplateSnapshot = JsonSerializer.Serialize(templateSnapshot), // Guardar snapshot completo
                FechaVersion = DateTime.Now, // ✅ Hora local del servidor
                
                // ✅ AUDITORÍA: Guardar quién creó el formulario
                FilledBy = dto.FilledBy,
                FilledByEmail = dto.FilledByEmail,
                FilledByRole = dto.FilledByRole,
                
                HeaderData = dto.HeaderData,
                BodyData = dto.BodyData,
                FirmasData = dto.FirmasData,
                TipoProducto = dto.TipoProducto, // 🦐🐟 NUEVO: Guardar tipo de producto
                Observaciones = dto.Observaciones,
                CreatedAt = DateTime.Now,
                // La fecha del encabezado, que es por la que se busca. Si el
                // formulario no trae ninguna, se usa la de guardado: dejarla en
                // NULL lo sacaría de todos los filtros por rango.
                FechaRegistro = FechaDelHeader(dto.HeaderData) ?? DateTime.Now.Date
            };

            _context.FilledForms.Add(filledForm);
            await _context.SaveChangesAsync();
            
            // ✅ INDEXAR PARA TRAZABILIDAD RÁPIDA
            await IndexTraceabilityRecords(filledForm, template);

            _logger.LogInformation("✅ Formulario {FormId} creado por {User} ({Email})", 
                filledForm.FormID, filledForm.FilledBy ?? "Desconocido", filledForm.FilledByEmail ?? "Sin email");

            // ✅ CREAR ALERTAS INMEDIATAS para todos los firmantes
            await CreateInitialSignatureAlerts(filledForm, template);
            
            return CreatedAtAction(nameof(GetFilledForm), new { id = filledForm.FormID }, filledForm);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutFilledForm(int id, [FromBody] FilledFormInputDto dto)
        {
            // Verificar que el formulario existe
            var existingForm = await _context.FilledForms.FindAsync(id);
            if (existingForm == null)
            {
                return NotFound(new { message = "El formulario llenado no fue encontrado." });
            }

            // Verificar que el TemplateID es válido
            var templateExists = await _context.Templates.AnyAsync(t => t.TemplateID == dto.TemplateID);
            if (!templateExists)
            {
                return BadRequest(new { message = "El TemplateID proporcionado no es válido." });
            }

            // 🔒 BLOQUEO POR ANTIGÜEDAD: Ver Formularios y Editar Formulario Llenado guardan las
            // firmas por acá (no por /Signatures/sign), así que el límite de horas también tiene
            // que revisarse acá. Si no, el mismo registro vencido se bloquea en Gestión de Firmas
            // y se firma sin problema entrando por Editar.
            // Solo frena cuando el guardado AGREGA una firma nueva: corregir datos de un registro
            // viejo sigue permitido.
            if (AgregaFirmaNueva(existingForm.FirmasData, dto.FirmasData)
                && await EstaBloqueadoParaFirmarAsync(existingForm))
            {
                var lockThreshold = await GetLockThresholdHoursAsync();
                _logger.LogWarning(
                    "🔒 Firma rechazada en formulario {FormId}: supera el límite de {Limite}h desde su creación",
                    id, lockThreshold);
                return BadRequest(new
                {
                    message = $"🔒 El registro superó el límite de {lockThreshold} horas (sin contar sábados ni domingos) desde su creación y está bloqueado para firma. Un Administrador debe habilitarlo en Supervisión General."
                });
            }

            // 🔍 AUDITORÍA: se compara ANTES de pisar los datos viejos.
            await RegistrarCambiosAsync(
                existingForm, dto.HeaderData, dto.BodyData,
                dto.FilledBy, dto.FilledByEmail, dto.FilledByRole);

            // Actualizar los campos del formulario existente
            existingForm.TemplateID = dto.TemplateID;
            existingForm.HeaderData = dto.HeaderData;
            // Si el encabezado cambió, la fecha del registro puede haber
            // cambiado con él: se recalcula para que los filtros lo encuentren
            // por la fecha nueva. Se conserva la anterior si el encabezado
            // nuevo no trae ninguna.
            existingForm.FechaRegistro = FechaDelHeader(dto.HeaderData)
                ?? existingForm.FechaRegistro
                ?? existingForm.CreatedAt.Date;
            existingForm.BodyData = dto.BodyData;
            existingForm.FirmasData = dto.FirmasData;
            existingForm.TipoProducto = dto.TipoProducto; // 🦐🐟 NUEVO: Actualizar tipo de producto
            existingForm.Observaciones = dto.Observaciones;
            existingForm.UpdatedAt = DateTime.Now; // ✅ Hora local del servidor
            // CreatedAt se mantiene sin cambios

            _context.Entry(existingForm).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                
                // ✅ RE-INDEXAR PARA TRAZABILIDAD RÁPIDA
                var template = await _context.Templates.FindAsync(dto.TemplateID);
                if (template != null) {
                    await IndexTraceabilityRecords(existingForm, template);
                }
                
                return Ok(new { 
                    message = "Formulario actualizado exitosamente", 
                    formId = id,
                    updatedAt = existingForm.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FilledFormExists(id))
                {
                    return NotFound(new { message = "El formulario ya no existe." });
                }
                else
                {
                    return BadRequest(new { message = "Error de concurrencia al actualizar. Intente nuevamente." });
                }
            }
        }

        /// <summary>
        /// Lee el umbral de horas para bloqueo desde AlertConfiguration (lo configura el admin en
        /// Gestión de Alertas). Mismo criterio que SignaturesController: si no hay config o el valor
        /// es inválido, 36 por defecto.
        /// </summary>
        private async Task<int> GetLockThresholdHoursAsync()
        {
            try
            {
                var config = await _context.AlertConfigurations.FirstOrDefaultAsync();
                if (config != null && config.LockThresholdHours > 0)
                    return config.LockThresholdHours;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo leer LockThresholdHours, usando 36 por defecto");
            }
            return 36;
        }

        /// <summary>
        /// ¿El registro pasó el límite de horas y ningún Administrador lo habilitó?
        /// Mismo criterio que /Signatures/sign: se cuenta desde CreatedAt y no se cuentan
        /// sábados ni domingos. La habilitación manual vive dentro de HeaderData.
        /// </summary>
        private async Task<bool> EstaBloqueadoParaFirmarAsync(FilledForm form)
        {
            bool estaHabilitado = !string.IsNullOrEmpty(form.HeaderData)
                                  && form.HeaderData.Contains("unlocked36h");
            if (estaHabilitado) return false;

            var lockThreshold = await GetLockThresholdHoursAsync();
            var diffHours = SignaturesController.BusinessHoursBetween(form.CreatedAt, DateTime.Now);
            return diffHours > lockThreshold;
        }

        /// <summary>
        /// ¿El guardado agrega la imagen de una firma que antes no estaba?
        ///
        /// Se compara puesto por puesto: solo interesa que aparezca una firma donde no había.
        /// Cambiar el nombre del firmante, el cargo o una observación NO cuenta como firmar, para
        /// no trabar la corrección de registros viejos.
        /// </summary>
        private static bool AgregaFirmaNueva(string? firmasAnteriores, string? firmasNuevas)
        {
            var nuevas = PuestosConFirma(firmasNuevas);
            if (nuevas.Count == 0) return false;

            var anteriores = PuestosConFirma(firmasAnteriores);
            return nuevas.Any(puesto => !anteriores.Contains(puesto));
        }

        /// <summary>Puestos que ya tienen imagen de firma (url o base64) dentro de FirmasData.</summary>
        private static HashSet<string> PuestosConFirma(string? firmasData)
        {
            var puestos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(firmasData)) return puestos;

            try
            {
                var firmas = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firmasData);
                if (firmas == null) return puestos;

                foreach (var kvp in firmas)
                {
                    if (kvp.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!kvp.Value.TryGetProperty("firma", out var firma) || firma.ValueKind != JsonValueKind.Object)
                        continue;

                    bool tieneImagen =
                        (firma.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String
                         && !string.IsNullOrWhiteSpace(url.GetString())) ||
                        (firma.TryGetProperty("base64", out var b64) && b64.ValueKind == JsonValueKind.String
                         && !string.IsNullOrWhiteSpace(b64.GetString()));

                    if (tieneImagen) puestos.Add(kvp.Key);
                }
            }
            catch (JsonException)
            {
                // FirmasData ilegible: se trata como "sin firmas" y decide el resto de la validación.
            }

            return puestos;
        }

        // PATCH: api/FilledForms/{id}/autosave - Para autoguardado específico
        [HttpPatch("{id}/autosave")]
        public async Task<IActionResult> AutosaveFilledForm(int id, [FromBody] AutosaveDto dto)
        {
            var existingForm = await _context.FilledForms.FindAsync(id);
            if (existingForm == null)
            {
                return NotFound(new { message = "El formulario llenado no fue encontrado." });
            }

            // 🔍 AUDITORÍA: el autoguardado también modifica valores ya guardados.
            // Se compara contra lo que hay, y los cambios seguidos del mismo
            // usuario se agrupan en un solo registro.
            await RegistrarCambiosAsync(
                existingForm,
                string.IsNullOrEmpty(dto.HeaderData) ? existingForm.HeaderData : dto.HeaderData,
                string.IsNullOrEmpty(dto.BodyData) ? existingForm.BodyData : dto.BodyData,
                dto.FilledBy, dto.FilledByEmail, dto.FilledByRole);

            // Solo actualizar datos para autoguardado (sin cambiar template)
            if (!string.IsNullOrEmpty(dto.HeaderData))
                existingForm.HeaderData = dto.HeaderData;
                existingForm.FechaRegistro = FechaDelHeader(dto.HeaderData)
                    ?? existingForm.FechaRegistro
                    ?? existingForm.CreatedAt.Date;
            // Si el encabezado cambió, la fecha del registro puede haber
            // cambiado con él: se recalcula para que los filtros lo encuentren
            // por la fecha nueva. Se conserva la anterior si el encabezado
            // nuevo no trae ninguna.
            existingForm.FechaRegistro = FechaDelHeader(dto.HeaderData)
                ?? existingForm.FechaRegistro
                ?? existingForm.CreatedAt.Date;
                
            if (!string.IsNullOrEmpty(dto.BodyData))
                existingForm.BodyData = dto.BodyData;
                
            // 🔒 El autoguardado no puede meter una firma nueva en un registro ya bloqueado por
            // antigüedad. No se devuelve error (cortaría el autoguardado de los datos): se guarda
            // todo lo demás y se deja la firma afuera. El usuario recibe el aviso al guardar.
            if (!string.IsNullOrEmpty(dto.FirmasData))
            {
                if (AgregaFirmaNueva(existingForm.FirmasData, dto.FirmasData)
                    && await EstaBloqueadoParaFirmarAsync(existingForm))
                {
                    _logger.LogWarning(
                        "🔒 Autoguardado del formulario {FormId}: se ignoró una firma nueva, el registro está bloqueado por antigüedad", id);
                }
                else
                {
                    existingForm.FirmasData = dto.FirmasData;
                }
            }

            // 🦐🐟 NUEVO: Actualizar tipo de producto en autoguardado
            if (!string.IsNullOrEmpty(dto.TipoProducto))
                existingForm.TipoProducto = dto.TipoProducto;
                
            if (!string.IsNullOrEmpty(dto.Observaciones))
                existingForm.Observaciones = dto.Observaciones;

            existingForm.UpdatedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();
                
                // ✅ RE-INDEXAR PARA TRAZABILIDAD RÁPIDA
                var template = await _context.Templates.FindAsync(existingForm.TemplateID);
                if (template != null) {
                    await IndexTraceabilityRecords(existingForm, template);
                }
                
                return Ok(new { 
                    message = "Autoguardado exitoso", 
                    formId = id,
                    timestamp = existingForm.UpdatedAt
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                return BadRequest(new { message = "Error en autoguardado. Intente nuevamente." });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 🔍 AUDITORÍA DE VALORES: qué se modificó de un formulario ya guardado
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Los cambios de una misma persona hechos dentro de esta ventana se
        /// juntan en un solo registro, así un autoguardado no genera cien filas.
        /// </summary>
        private const int VENTANA_AGRUPADO_MIN = 30;

        /// <summary>
        /// Aplana un JSON de formulario a "clave → valor de texto".
        ///
        /// Encabezado:  h:CAMPO
        /// Tablas:      b:&lt;elemento&gt;:&lt;fila&gt;:COLUMNA
        /// Secciones:   b:&lt;elemento&gt;:CAMPO
        ///
        /// Las claves internas (las que empiezan con "_", como _deleted o
        /// _rowSpan) no son datos del operario y se ignoran.
        /// </summary>
        private static Dictionary<string, string> AplanarValores(string? headerJson, string? bodyJson)
        {
            var plano = new Dictionary<string, string>();

            static string Texto(JsonElement v) => v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => "",
                _ => v.GetRawText()
            };

            static bool EsInterna(string clave) => string.IsNullOrEmpty(clave) || clave.StartsWith("_");

            static bool EsEscalar(JsonElement v) =>
                v.ValueKind is JsonValueKind.String or JsonValueKind.Number
                    or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;

            // ── Encabezado ──
            if (!string.IsNullOrWhiteSpace(headerJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(headerJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (EsInterna(prop.Name) || !EsEscalar(prop.Value)) continue;
                            plano[$"h:{prop.Name}"] = Texto(prop.Value);
                        }
                    }
                }
                catch (JsonException) { /* JSON roto: se ignora, no se audita */ }
            }

            // ── Cuerpo ──
            if (!string.IsNullOrWhiteSpace(bodyJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(bodyJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        int elemIdx = -1;
                        foreach (var elemento in doc.RootElement.EnumerateArray())
                        {
                            elemIdx++;
                            if (elemento.ValueKind != JsonValueKind.Object) continue;
                            if (!elemento.TryGetProperty("data", out var data)) continue;

                            if (data.ValueKind == JsonValueKind.Array)
                            {
                                // Tabla: una entrada por celda
                                int filaIdx = -1;
                                foreach (var fila in data.EnumerateArray())
                                {
                                    filaIdx++;
                                    if (fila.ValueKind != JsonValueKind.Object) continue;
                                    foreach (var celda in fila.EnumerateObject())
                                    {
                                        if (EsInterna(celda.Name) || !EsEscalar(celda.Value)) continue;
                                        plano[$"b:{elemIdx}:{filaIdx}:{celda.Name}"] = Texto(celda.Value);
                                    }
                                }
                            }
                            else if (data.ValueKind == JsonValueKind.Object)
                            {
                                // Sección de campos sueltos
                                foreach (var campo in data.EnumerateObject())
                                {
                                    if (EsInterna(campo.Name) || !EsEscalar(campo.Value)) continue;
                                    plano[$"b:{elemIdx}:{campo.Name}"] = Texto(campo.Value);
                                }
                            }
                        }
                    }
                }
                catch (JsonException) { /* JSON roto: se ignora, no se audita */ }
            }

            return plano;
        }

        /// <summary>Convierte una clave aplanada en un cambio con sus partes separadas.</summary>
        private static CambioValorDto ArmarCambio(string clave, string? antes, string? despues)
        {
            var cambio = new CambioValorDto { Clave = clave, Antes = antes, Despues = despues };

            if (clave.StartsWith("h:"))
            {
                cambio.Ambito = "encabezado";
                cambio.Campo = clave.Substring(2);
                return cambio;
            }

            // b:<elemento>[:<fila>]:<campo>  — el campo puede tener ":" adentro,
            // así que se parte solo por las primeras posiciones.
            var partes = clave.Split(':');
            cambio.Ambito = "seccion";
            if (partes.Length >= 3 && int.TryParse(partes[1], out var elem))
            {
                cambio.Elemento = elem;
                if (partes.Length >= 4 && int.TryParse(partes[2], out var fila))
                {
                    cambio.Ambito = "tabla";
                    cambio.Fila = fila;
                    cambio.Campo = string.Join(":", partes.Skip(3));
                }
                else
                {
                    cambio.Campo = string.Join(":", partes.Skip(2));
                }
            }
            else
            {
                cambio.Campo = clave;
            }
            return cambio;
        }

        /// <summary>
        /// Compara lo guardado contra lo que llega y devuelve solo los valores
        /// que cambiaron. Un campo que pasa de vacío a vacío (null vs "") no
        /// cuenta como cambio.
        /// </summary>
        private static List<CambioValorDto> CompararValores(
            string? headerViejo, string? bodyViejo, string? headerNuevo, string? bodyNuevo)
        {
            var antes = AplanarValores(headerViejo, bodyViejo);
            var despues = AplanarValores(headerNuevo, bodyNuevo);
            var cambios = new List<CambioValorDto>();

            foreach (var clave in antes.Keys.Union(despues.Keys))
            {
                antes.TryGetValue(clave, out var v1);
                despues.TryGetValue(clave, out var v2);
                var a = (v1 ?? "").Trim();
                var d = (v2 ?? "").Trim();
                if (a == d) continue;
                cambios.Add(ArmarCambio(clave, a, d));
            }

            return cambios;
        }

        /// <summary>
        /// Registra en FilledFormChanges los valores que cambiaron. Si la misma
        /// persona ya tenía una tanda abierta en los últimos VENTANA_AGRUPADO_MIN
        /// minutos, se acumula ahí conservando el valor ORIGINAL: si algo pasó de
        /// A → B y después de B → C, queda registrado A → C.
        /// </summary>
        private async Task RegistrarCambiosAsync(
            FilledForm formulario, string? headerNuevo, string? bodyNuevo,
            string? usuario, string? email, string? rol)
        {
            try
            {
                var cambios = CompararValores(formulario.HeaderData, formulario.BodyData, headerNuevo, bodyNuevo);
                if (cambios.Count == 0) return;

                var ahora = DateTime.Now;
                var desde = ahora.AddMinutes(-VENTANA_AGRUPADO_MIN);
                var quien = string.IsNullOrWhiteSpace(usuario) ? "(sin identificar)" : usuario.Trim();

                var tanda = await _context.FilledFormChanges
                    .Where(c => c.FormID == formulario.FormID && c.ChangedBy == quien && c.UpdatedAt >= desde)
                    .OrderByDescending(c => c.UpdatedAt)
                    .FirstOrDefaultAsync();

                var opciones = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                if (tanda == null)
                {
                    _context.FilledFormChanges.Add(new FilledFormChange
                    {
                        FormID = formulario.FormID,
                        ChangedBy = quien,
                        ChangedByEmail = email,
                        ChangedByRole = rol,
                        ChangedAt = ahora,
                        UpdatedAt = ahora,
                        Cambios = JsonSerializer.Serialize(cambios, opciones),
                        TotalCambios = cambios.Count
                    });
                }
                else
                {
                    var previos = new List<CambioValorDto>();
                    if (!string.IsNullOrWhiteSpace(tanda.Cambios))
                    {
                        try
                        {
                            previos = JsonSerializer.Deserialize<List<CambioValorDto>>(tanda.Cambios, opciones)
                                      ?? new List<CambioValorDto>();
                        }
                        catch (JsonException) { previos = new List<CambioValorDto>(); }
                    }

                    var porClave = previos.ToDictionary(c => c.Clave, c => c);
                    foreach (var c in cambios)
                    {
                        if (porClave.TryGetValue(c.Clave, out var existente))
                        {
                            existente.Despues = c.Despues; // el "antes" original se conserva
                        }
                        else
                        {
                            porClave[c.Clave] = c;
                        }
                    }

                    // Si volvió al valor original deja de ser un cambio.
                    var finales = porClave.Values
                        .Where(c => (c.Antes ?? "") != (c.Despues ?? ""))
                        .ToList();

                    tanda.Cambios = JsonSerializer.Serialize(finales, opciones);
                    tanda.TotalCambios = finales.Count;
                    tanda.UpdatedAt = ahora;
                    _context.Entry(tanda).State = EntityState.Modified;
                }
            }
            catch (Exception ex)
            {
                // La auditoría NUNCA debe impedir que se guarde el formulario.
                _logger.LogError(ex, "No se pudieron registrar los cambios del formulario {FormId}", formulario.FormID);
            }
        }

        /// <summary>
        /// Historial de valores modificados de un formulario, del más reciente al
        /// más viejo. Lo consume la pantalla VER para marcar las celdas tocadas.
        /// </summary>
        [HttpGet("{id}/cambios")]
        public async Task<IActionResult> GetCambios(int id)
        {
            if (!FilledFormExists(id))
                return NotFound(new { message = "El formulario no existe." });

            var opciones = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            List<FilledFormChange> filas;
            try
            {
                filas = await _context.FilledFormChanges
                    .Where(c => c.FormID == id)
                    .OrderByDescending(c => c.ChangedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // La tabla se crea con un script aparte. Mientras no se haya
                // corrido, la pantalla VER se muestra sin marcas en vez de fallar.
                _logger.LogWarning(ex,
                    "No se pudo leer FilledFormChanges. ¿Falta correr "
                    + "backend-frigo/Migrations/CreateFilledFormChangesTable.sql?");
                return Ok(new { formId = id, totalTandas = 0, totalCambios = 0, tandas = Array.Empty<object>() });
            }

            var tandas = filas.Select(c =>
            {
                List<CambioValorDto> detalle;
                try
                {
                    detalle = string.IsNullOrWhiteSpace(c.Cambios)
                        ? new List<CambioValorDto>()
                        : JsonSerializer.Deserialize<List<CambioValorDto>>(c.Cambios, opciones) ?? new List<CambioValorDto>();
                }
                catch (JsonException) { detalle = new List<CambioValorDto>(); }

                return new
                {
                    id = c.Id,
                    changedBy = c.ChangedBy,
                    changedByEmail = c.ChangedByEmail,
                    changedByRole = c.ChangedByRole,
                    changedAt = c.ChangedAt,
                    updatedAt = c.UpdatedAt,
                    totalCambios = c.TotalCambios,
                    cambios = detalle
                };
            }).ToList();

            return Ok(new
            {
                formId = id,
                totalTandas = tandas.Count,
                totalCambios = tandas.Sum(t => t.totalCambios),
                tandas
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFilledForm(int id)
        {
            var filledForm = await _context.FilledForms.FindAsync(id);
            if (filledForm == null)
            {
                return NotFound();
            }

            // Eliminar registros relacionados para evitar error de FK
            var relatedSignatures = await _context.Signatures
                .Where(s => s.FilledFormId == id)
                .ToListAsync();
            if (relatedSignatures.Any())
                _context.Signatures.RemoveRange(relatedSignatures);

            var relatedRejections = await _context.SignatureRejections
                .Where(r => r.FilledFormId == id)
                .ToListAsync();
            if (relatedRejections.Any())
                _context.SignatureRejections.RemoveRange(relatedRejections);

            var relatedAlerts = await _context.Alerts
                .Where(a => a.FormId == id)
                .ToListAsync();
            if (relatedAlerts.Any())
                _context.Alerts.RemoveRange(relatedAlerts);

            _context.FilledForms.Remove(filledForm);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Función de ayuda para verificar si existe un formulario
        private bool FilledFormExists(int id)
        {
            return _context.FilledForms.Any(e => e.FormID == id);
        }

        // Función helper para obtener datos del template actual
        private object? GetCurrentTemplateData(Template? template)
        {
            if (template == null) return null;

            return new
            {
                TemplateID = template.TemplateID,
                Codigo = template.Codigo,
                Nombre = template.Nombre,
                Version = template.Version,
                Objetivo = template.Supervisa,
                Proceso = template.Proceso,
                CuandoSeUsa = template.CuandoSeUsa,
                QuienLoLlena = template.QuienLoLlena,
                HeaderFields = template.HeaderFields,
                BodyElements = template.BodyElements,
                Firmas = template.Firmas,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        //==============================================================
        // ENDPOINTS PARA EXPORTACIÓN PDF/EXCEL
        //==============================================================

        /// <summary>
        /// GET: api/FilledForms/5/with-template
        /// Obtiene el formulario con su template completo incluido y parseado (para PDF/Excel)
        /// </summary>
        [HttpGet("{id}/with-template")]
        public async Task<ActionResult<object>> GetFilledFormWithTemplate(int id)
        {
            var filledForm = await _context.FilledForms
                .Include(f => f.Template)
                .FirstOrDefaultAsync(f => f.FormID == id);

            if (filledForm == null)
            {
                return NotFound(new { message = "Formulario no encontrado" });
            }

            // VERSIONAMIENTO: Usar snapshot si existe, si no usar template actual
            Template? templateToUse = null;
            bool isHistorical = false;

            if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
            {
                try
                {
                    templateToUse = JsonSerializer.Deserialize<Template>(filledForm.TemplateSnapshot);
                    isHistorical = true;
                }
                catch
                {
                    templateToUse = filledForm.Template;
                }
            }
            else
            {
                templateToUse = filledForm.Template;
            }

            if (templateToUse == null)
            {
                return NotFound(new { message = "Template no encontrado" });
            }
            
            // Si el snapshot no tenía FechaVersion, usar la del template actual
            if (templateToUse.FechaVersion == null && filledForm.Template?.FechaVersion != null)
            {
                templateToUse.FechaVersion = filledForm.Template.FechaVersion;
            }

            // Parsear datos JSON del formulario
            object? headerData = null;
            object? bodyData = null;
            object? firmasData = null;
            
            try
            {
                headerData = string.IsNullOrEmpty(filledForm.HeaderData) 
                    ? new { } 
                    : JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                    
                bodyData = string.IsNullOrEmpty(filledForm.BodyData) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(filledForm.BodyData);
                    
                firmasData = string.IsNullOrEmpty(filledForm.FirmasData) 
                    ? new { } 
                    : JsonSerializer.Deserialize<object>(filledForm.FirmasData);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error parseando datos del formulario", error = ex.Message });
            }

            // Parsear estructura JSON del template
            object? headerFields = null;
            object? bodyElements = null;
            object? firmas = null;
            
            try
            {
                // Para headerFields usar SIEMPRE el template actual (tiene los defaultValues actualizados)
                // Para bodyElements usar el snapshot (tiene la estructura correcta del momento del llenado)
                var currentTemplate = filledForm.Template;
                var headerFieldsSource = (!string.IsNullOrEmpty(currentTemplate?.HeaderFields))
                    ? currentTemplate.HeaderFields
                    : templateToUse.HeaderFields;

                headerFields = string.IsNullOrEmpty(headerFieldsSource) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(headerFieldsSource);
                    
                bodyElements = string.IsNullOrEmpty(templateToUse.BodyElements) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(templateToUse.BodyElements);
                    
                firmas = string.IsNullOrEmpty(templateToUse.Firmas) 
                    ? new object[] { } 
                    : JsonSerializer.Deserialize<object>(templateToUse.Firmas);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error parseando estructura del template", error = ex.Message });
            }

            // Respuesta completa optimizada para exportación
            var response = new
            {
                // Metadatos del formulario
                FormID = filledForm.FormID,
                TemplateID = filledForm.TemplateID,
                TemplateVersion = filledForm.TemplateVersion,
                FechaVersion = filledForm.FechaVersion,
                CreatedAt = filledForm.CreatedAt,
                UpdatedAt = filledForm.UpdatedAt,
                Observaciones = filledForm.Observaciones,
                IsHistorical = isHistorical,
                
                // 🦐🐟 TIPO DE PRODUCTO
                TipoProducto = filledForm.TipoProducto,
                
                // Datos del formulario (parseados)
                Data = new
                {
                    Header = headerData,
                    Body = bodyData,
                    Firmas = firmasData
                },
                
                // Estructura del template (parseada)
                Template = new
                {
                    TemplateID = templateToUse.TemplateID,
                    Codigo = templateToUse.Codigo,
                    Nombre = templateToUse.Nombre,
                    Version = templateToUse.Version,
                    FechaVersion = templateToUse.FechaVersion,
                    CreatedAt = templateToUse.CreatedAt,
                    Objetivo = templateToUse.Supervisa,
                    Proceso = templateToUse.Proceso,
                    CuandoSeUsa = templateToUse.CuandoSeUsa,
                    QuienLoLlena = templateToUse.QuienLoLlena,
                    Structure = new
                    {
                        HeaderFields = headerFields,
                        BodyElements = bodyElements,
                        Firmas = firmas
                    }
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// POST: api/FilledForms/export-multiple
        /// Obtiene múltiples formularios con sus templates para exportación masiva
        /// Body: [1, 2, 3, 4, 5]
        /// </summary>
        [HttpPost("export-multiple")]
        public async Task<ActionResult<object>> ExportMultipleForms([FromBody] int[] formIds)
        {
            if (formIds == null || formIds.Length == 0)
            {
                return BadRequest(new { message = "Debe proporcionar al menos un FormID" });
            }

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => formIds.Contains(f.FormID))
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return NotFound(new { message = "No se encontraron formularios con los IDs proporcionados" });
            }

            var result = forms.Select(filledForm =>
            {
                // VERSIONAMIENTO: Usar snapshot si existe
                Template? templateToUse = null;
                bool isHistorical = false;

                if (!string.IsNullOrEmpty(filledForm.TemplateSnapshot))
                {
                    try
                    {
                        templateToUse = JsonSerializer.Deserialize<Template>(filledForm.TemplateSnapshot);
                        isHistorical = true;
                    }
                    catch
                    {
                        templateToUse = filledForm.Template;
                    }
                }
                else
                {
                    templateToUse = filledForm.Template;
                }

                // Parsear datos del formulario
                object? headerData = null;
                object? bodyData = null;
                object? firmasData = null;
                
                try
                {
                    headerData = string.IsNullOrEmpty(filledForm.HeaderData) 
                        ? new { } 
                        : JsonSerializer.Deserialize<object>(filledForm.HeaderData);
                        
                    bodyData = string.IsNullOrEmpty(filledForm.BodyData) 
                        ? new object[] { } 
                        : JsonSerializer.Deserialize<object>(filledForm.BodyData);
                        
                    firmasData = string.IsNullOrEmpty(filledForm.FirmasData) 
                        ? new { } 
                        : JsonSerializer.Deserialize<object>(filledForm.FirmasData);
                }
                catch { }

                // Parsear estructura del template
                object? headerFields = null;
                object? bodyElements = null;
                object? firmas = null;
                
                if (templateToUse != null)
                {
                    try
                    {
                        // Para headerFields usar SIEMPRE el template actual (tiene los defaultValues actualizados)
                        // Para bodyElements usar el snapshot (tiene la estructura correcta del momento del llenado)
                        var headerFieldsSource = (!string.IsNullOrEmpty(filledForm.Template?.HeaderFields))
                            ? filledForm.Template.HeaderFields
                            : templateToUse.HeaderFields;

                        headerFields = string.IsNullOrEmpty(headerFieldsSource) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(headerFieldsSource);
                            
                        bodyElements = string.IsNullOrEmpty(templateToUse.BodyElements) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(templateToUse.BodyElements);
                            
                        firmas = string.IsNullOrEmpty(templateToUse.Firmas) 
                            ? new object[] { } 
                            : JsonSerializer.Deserialize<object>(templateToUse.Firmas);
                    }
                    catch { }
                }

                return new
                {
                    FormID = filledForm.FormID,
                    TemplateID = filledForm.TemplateID,
                    TemplateVersion = filledForm.TemplateVersion,
                    CreatedAt = filledForm.CreatedAt,
                    UpdatedAt = filledForm.UpdatedAt,
                    Observaciones = filledForm.Observaciones,
                    IsHistorical = isHistorical,
                    Data = new
                    {
                        Header = headerData,
                        Body = bodyData,
                        Firmas = firmasData
                    },
                    Template = templateToUse == null ? null : new
                    {
                        TemplateID = templateToUse.TemplateID,
                        Codigo = templateToUse.Codigo,
                        Nombre = templateToUse.Nombre,
                        Version = templateToUse.Version,
                        FechaVersion = templateToUse.FechaVersion,
                        CreatedAt = templateToUse.CreatedAt,
                        Objetivo = templateToUse.Supervisa,
                        Proceso = templateToUse.Proceso,
                        Structure = new
                        {
                            HeaderFields = headerFields,
                            BodyElements = bodyElements,
                            Firmas = firmas
                        }
                    }
                };
            }).ToList();

            return Ok(new 
            { 
                count = result.Count, 
                forms = result,
                message = $"{result.Count} formulario(s) listos para exportación"
            });
        }

        /// <summary>
        /// GET: api/FilledForms/export-by-template/9
        /// Obtiene todos los formularios de un template específico para exportación masiva
        /// </summary>
        [HttpGet("export-by-template/{templateId}")]
        public async Task<ActionResult<object>> ExportFormsByTemplate(int templateId)
        {
            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.TemplateID == templateId)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return Ok(new 
                { 
                    count = 0, 
                    forms = new object[] { },
                    message = "No hay formularios para este template"
                });
            }

            var formIds = forms.Select(f => f.FormID).ToArray();
            
            // Reutilizar la lógica de export-multiple
            return await ExportMultipleForms(formIds);
        }

        /// <summary>
        /// GET: api/FilledForms/export-by-date-range?startDate=2025-01-01&endDate=2025-12-31
        /// Obtiene formularios por rango de fechas para exportación
        /// </summary>
        [HttpGet("export-by-date-range")]
        public async Task<ActionResult<object>> ExportFormsByDateRange(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest(new { message = "La fecha de inicio no puede ser posterior a la fecha final" });
            }

            // El navegador manda solo la fecha, así que endDate llega a las
            // 00:00:00 y todo lo creado ese día después de medianoche quedaba
            // fuera: buscar "del 7 al 7" no devolvía nada y había que estirar el
            // rango hasta el 8. Se toma el día completo.
            //
            // AddDays(1).AddTicks(-1) y no 23:59:59, para no perder un registro
            // guardado a las 23:59:59.500.
            var rangoDesde = startDate.Date;
            var rangoHasta = endDate.Date.AddDays(1).AddTicks(-1);

            var forms = await _context.FilledForms
                .Include(f => f.Template)
                .Where(f => f.FechaRegistro >= rangoDesde && f.FechaRegistro <= rangoHasta)
                .OrderBy(f => f.CreatedAt)
                .ToListAsync();

            if (!forms.Any())
            {
                return Ok(new 
                { 
                    count = 0, 
                    forms = new object[] { },
                    message = $"No hay formularios entre {startDate:yyyy-MM-dd} y {endDate:yyyy-MM-dd}"
                });
            }

            var formIds = forms.Select(f => f.FormID).ToArray();
            
            return await ExportMultipleForms(formIds);
        }

        // 🎯 MÉTODO HELPER: Determina qué versión estaba vigente en una fecha específica
        private async Task<string> GetVersionVigenteEnFecha(int templateId, DateTime fecha)
        {
            // Obtener todas las versiones con fecha asignada
            var versiones = await _context.TemplateVersions
                .Where(tv => tv.TemplateID == templateId && tv.FechaVersion != null)
                .OrderByDescending(tv => tv.FechaVersion)
                .ToListAsync();

            Console.WriteLine($"🔍 DEBUG - GetVersionVigenteEnFecha: TemplateID={templateId}, Fecha={fecha:yyyy-MM-dd}");
            Console.WriteLine($"🔍 DEBUG - Versiones encontradas: {versiones.Count}");

            // Buscar la versión más reciente que sea <= a la fecha del formulario
            var versionVigente = versiones
                .Where(v => v.FechaVersion <= fecha)
                .OrderByDescending(v => v.FechaVersion)
                .FirstOrDefault();

            if (versionVigente != null)
            {
                Console.WriteLine($"✅ Versión vigente encontrada: {versionVigente.Version} (FechaVersion: {versionVigente.FechaVersion:yyyy-MM-dd})");
                return versionVigente.Version;
            }

            // Si no hay versión vigente, usar la versión actual del template
            var currentTemplate = await _context.Templates.FindAsync(templateId);
            var fallbackVersion = currentTemplate?.Version ?? "1";
            Console.WriteLine($"⚠️ No se encontró versión vigente, usando fallback: {fallbackVersion}");
            return fallbackVersion;
        }

        /// <summary>
        /// Lista JSON lista para enviar. Devuelve siempre un arreglo (vacío si el
        /// texto no existe, está dañado o no es una lista) y tolera el JSON
        /// guardado dos veces como texto. Al ser JsonElement, Preserve no le
        /// agrega "$id"/"$values".
        /// </summary>
        private static JsonElement ListaJson(string? json)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var raiz = doc.RootElement;

                    if (raiz.ValueKind == JsonValueKind.Array)
                        return raiz.Clone();

                    if (raiz.ValueKind == JsonValueKind.String)
                    {
                        using var interno = JsonDocument.Parse(raiz.GetString() ?? "[]");
                        if (interno.RootElement.ValueKind == JsonValueKind.Array)
                            return interno.RootElement.Clone();
                    }
                }
                catch (JsonException) { /* JSON dañado: se devuelve lista vacía */ }
            }

            using var vacio = JsonDocument.Parse("[]");
            return vacio.RootElement.Clone();
        }

        // 🎯 MÉTODO HELPER: Obtiene la estructura de una versión específica
        private async Task<object?> GetTemplateStructureByVersion(int templateId, string version)
        {
            Console.WriteLine($"🔍 DEBUG - GetTemplateStructureByVersion: TemplateID={templateId}, Version={version}");

            // Buscar la versión específica
            var templateVersion = await _context.TemplateVersions
                .FirstOrDefaultAsync(tv => tv.TemplateID == templateId && tv.Version == version);

            if (templateVersion == null)
            {
                Console.WriteLine($"⚠️ No se encontró TemplateVersion para version={version}");
                return null;
            }

            // ⚠️ Program.cs usa ReferenceHandler.Preserve: una List<object> se
            // envía como {"$id":"3","$values":[...]} y no como [...]. El frontend
            // hacía .map() sobre eso y la edición reventaba con
            // "bodyElements.map is not a function" (form #2369).
            // Un JsonElement se escribe tal cual, sin esos metadatos.
            var template = await _context.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateID == templateId);

            // Mismas claves que GetCurrentTemplateData y el snapshot: antes esta
            // rama no mandaba código ni nombre y la pantalla mostraba "N/A".
            var structure = new
            {
                TemplateID = templateId,
                Codigo = string.IsNullOrWhiteSpace(templateVersion.Codigo) ? template?.Codigo : templateVersion.Codigo,
                Nombre = string.IsNullOrWhiteSpace(templateVersion.Nombre) ? template?.Nombre : templateVersion.Nombre,
                Version = templateVersion.Version,
                Objetivo = templateVersion.Supervisa ?? template?.Supervisa,
                Proceso = templateVersion.Proceso ?? template?.Proceso,
                CuandoSeUsa = templateVersion.CuandoSeUsa ?? template?.CuandoSeUsa,
                QuienLoLlena = templateVersion.QuienLoLlena ?? template?.QuienLoLlena,
                headerFields = ListaJson(templateVersion.HeaderFields),
                bodyElements = ListaJson(templateVersion.BodyElements),
                firmas = ListaJson(templateVersion.Firmas)
            };

            Console.WriteLine($"✅ Estructura recuperada para version={version}");
            return structure;
        }

        /// <summary>
        /// ✨ NUEVO: Crea alertas para TODOS los firmantes cuando se crea el formulario
        /// </summary>
        private async Task CreateInitialSignatureAlerts(FilledForm form, Template template)
        {
            try
            {
                if (string.IsNullOrEmpty(form.FirmasData))
                {
                    _logger.LogInformation("⚠️ Formulario {FormId} no tiene FirmasData, no se crean alertas", form.FormID);
                    return;
                }

                var firmasDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(form.FirmasData);
                if (firmasDict == null || firmasDict.Count == 0)
                {
                    _logger.LogInformation("⚠️ Formulario {FormId} - FirmasData vacío", form.FormID);
                    return;
                }

                var templateName = template.Nombre ?? "Formulario";
                var formCode = template.Codigo ?? "N/A";

                _logger.LogInformation("📋 Creando alertas iniciales para formulario {FormId} ({FormCode}). Total puestos: {Count}", 
                    form.FormID, formCode, firmasDict.Count);

                int alertasCreadas = 0;
                var emailsFirmantesNotificados = new List<string>();

                foreach (var kvp in firmasDict)
                {
                    string puesto = kvp.Key;
                    var firmaData = kvp.Value;

                    if (firmaData.ValueKind != JsonValueKind.Object)
                    {
                        _logger.LogWarning("  ⚠️ Puesto {Puesto} no es un objeto JSON válido", puesto);
                        continue;
                    }

                    // Extraer email del usuario asignado
                    string? targetEmail = null;
                    string? targetName = null;
                    
                    if (firmaData.TryGetProperty("email", out var emailProp))
                    {
                        targetEmail = emailProp.GetString();
                    }

                    if (firmaData.TryGetProperty("nombre", out var nombreProp))
                    {
                        targetName = nombreProp.GetString();
                    }

                    // Fallback 1: buscar en nombre si contiene @
                    if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName) && targetName.Contains("@"))
                    {
                        targetEmail = targetName;
                    }

                    // Fallback 2: Buscar en CatalogoFirmas por nombre
                    if (string.IsNullOrEmpty(targetEmail) && !string.IsNullOrEmpty(targetName))
                    {
                        var catalogoEntry = await _context.Set<CatalogoFirma>()
                            .FirstOrDefaultAsync(c => 
                                c.Activo && 
                                (c.NombreCompleto.ToLower() == targetName.ToLower() || 
                                 c.NombreCompleto.ToLower().Contains(targetName.ToLower())));
                        
                        if (catalogoEntry != null && !string.IsNullOrEmpty(catalogoEntry.Correo))
                        {
                            targetEmail = catalogoEntry.Correo;
                            _logger.LogInformation("  🔎 Email encontrado en CatalogoFirmas para {Name}: {Email}", 
                                targetName, targetEmail);
                        }
                    }

                    // Fallback 3: Buscar en CatalogoFirmas por puesto
                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        var catalogoEntries = await _context.Set<CatalogoFirma>()
                            .Where(c => c.Activo && c.Puesto.ToLower().Contains(puesto.ToLower()))
                            .ToListAsync();
                        
                        if (catalogoEntries.Any(c => !string.IsNullOrEmpty(c.Correo)))
                        {
                            targetEmail = catalogoEntries.First(c => !string.IsNullOrEmpty(c.Correo)).Correo;
                            targetName = catalogoEntries.First(c => !string.IsNullOrEmpty(c.Correo)).NombreCompleto;
                            _logger.LogInformation("  🔎 Email encontrado en CatalogoFirmas para puesto {Puesto}: {Email}", 
                                puesto, targetEmail);
                        }
                    }

                    // Si no hay email asignado, saltar este puesto
                    if (string.IsNullOrEmpty(targetEmail))
                    {
                        _logger.LogWarning("  ⚠️ Puesto {Puesto}: No se encontró email en FirmasData ni en CatalogoFirmas, saltando", puesto);
                        continue;
                    }

                    _logger.LogInformation("  🔍 Puesto {Puesto}: Usuario asignado = {Name} ({Email})", 
                        puesto, targetName ?? "Sin nombre", targetEmail);

                    // Verificar si ya firmó (en caso de formularios pre-firmados)
                    bool yaFirmo = false;
                    if (firmaData.TryGetProperty("firma", out var firmaObj) && firmaObj.ValueKind == JsonValueKind.Object)
                    {
                        bool tieneUrl = firmaObj.TryGetProperty("url", out var urlProp) && !string.IsNullOrEmpty(urlProp.GetString());
                        bool tieneBase64 = firmaObj.TryGetProperty("base64", out var b64Prop) && !string.IsNullOrEmpty(b64Prop.GetString());
                        yaFirmo = tieneUrl || tieneBase64;

                        if (yaFirmo)
                        {
                            _logger.LogInformation("  ✅ Puesto {Puesto} ({Email}): Ya tiene firma, no se crea alerta", puesto, targetEmail);
                            continue;
                        }
                    }

                    // Verificar si ya existe alerta (evitar duplicados)
                    var existingAlert = await _context.Set<Alert>()
                        .FirstOrDefaultAsync(a =>
                            a.FormId == form.FormID &&
                            a.TargetEmail == targetEmail &&
                            a.Type == "signature" &&
                            a.Status == "pending");

                    if (existingAlert != null)
                    {
                        _logger.LogInformation("  ℹ️ Ya existe alerta para {Email} en formulario {FormId}", targetEmail, form.FormID);
                        continue;
                    }

                    // ✅ CREAR ALERTA
                    var alert = new Alert
                    {
                        Type = "signature",
                        Priority = "high",
                        Title = $"Firma requerida: {templateName}",
                        Message = $"Se ha creado el formulario {formCode} ({templateName}) que requiere tu firma en el puesto: {puesto}. Por favor revisa y firma el formulario lo antes posible.",
                        TargetEmail = targetEmail,
                        FormId = form.FormID,
                        FormCode = formCode,
                        CreatedDate = DateTime.Now,
                        IsRead = false,
                        Status = "pending"
                    };

                    _context.Set<Alert>().Add(alert);
                    alertasCreadas++;
                    emailsFirmantesNotificados.Add(targetEmail);
                    
                    _logger.LogInformation("  ✅ ALERTA CREADA para {Email} en puesto {Puesto}", targetEmail, puesto);

                    // 📧 ENVIAR EMAIL (con estado real, sin fire-and-forget)
                    try
                    {
                        var emailSubject = $"✍️ Firma Requerida - {templateName}";
                        var emailBody = $@"
                                <html>
                                <head>
                                    <style>
                                        body {{ margin: 0; padding: 0; font-family: Arial, sans-serif; }}
                                        .container {{ max-width: 600px; margin: 0 auto; }}
                                    </style>
                                </head>
                                <body>
                                    <div class='container'>
                                        <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                                    padding: 30px; 
                                                    border-radius: 10px; 
                                                    color: white; 
                                                    margin-bottom: 20px;'>
                                            <h1 style='margin: 0;'>✍️ Nuevo Formulario Requiere tu Firma</h1>
                                            <p style='margin: 10px 0 0 0; font-size: 18px;'>Sistema de Gestión Frigolab</p>
                                        </div>
                                        
                                        <div style='background: #f8f9fa; 
                                                    padding: 20px; 
                                                    border-radius: 10px; 
                                                    margin-bottom: 20px;'>
                                            <h2 style='color: #333; margin-top: 0;'>Hola {targetName ?? "Usuario"},</h2>
                                            <p style='color: #555; font-size: 16px; line-height: 1.6;'>
                                                Se ha creado un nuevo formulario que requiere tu firma digital:
                                            </p>
                                            
                                            <table style='width: 100%; 
                                                        margin: 20px 0; 
                                                        background: white; 
                                                        border-radius: 8px; 
                                                        overflow: hidden;
                                                        box-shadow: 0 2px 8px rgba(0,0,0,0.1);'>
                                                <tr style='background: #667eea; color: white;'>
                                                    <td style='padding: 12px; font-weight: bold; width: 40%;'>Formulario</td>
                                                    <td style='padding: 12px;'>{templateName}</td>
                                                </tr>
                                                <tr>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Código</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{formCode}</td>
                                                </tr>
                                                <tr style='background: #f8f9fa;'>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Tu puesto</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{puesto}</td>
                                                </tr>
                                                <tr>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd; font-weight: bold;'>Creado por</td>
                                                    <td style='padding: 12px; border-bottom: 1px solid #ddd;'>{form.FilledBy ?? "Sistema"}</td>
                                                </tr>
                                                <tr style='background: #f8f9fa;'>
                                                    <td style='padding: 12px; font-weight: bold;'>Fecha de creación</td>
                                                    <td style='padding: 12px;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td>
                                                </tr>
                                            </table>
                                            
                                            <div style='margin: 30px 0; text-align: center;'>
                                                <a href='http://localhost:5173/signatures' 
                                                   style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                                                          color: white; 
                                                          padding: 15px 40px; 
                                                          text-decoration: none; 
                                                          border-radius: 8px; 
                                                          font-size: 16px; 
                                                          font-weight: bold;
                                                          display: inline-block;
                                                          box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);'>
                                                    ✍️ Ir a Firmar Ahora
                                                </a>
                                            </div>
                                            
                                            <p style='color: #777; font-size: 14px; margin-top: 20px;'>
                                                <strong>Nota:</strong> Por favor firma este formulario lo antes posible.
                                            </p>
                                        </div>
                                        
                                        <div style='color: #999; 
                                                    font-size: 12px; 
                                                    text-align: center; 
                                                    margin-top: 30px; 
                                                    padding: 20px;
                                                    border-top: 1px solid #ddd;'>
                                            <p style='margin: 5px 0;'>Este es un mensaje automático del Sistema de Gestión Frigolab.</p>
                                            <p style='margin: 5px 0;'>Por favor no responder a este correo.</p>
                                            <p style='margin: 5px 0; color: #bbb;'>© 2026 Frigolab - Todos los derechos reservados</p>
                                        </div>
                                    </div>
                                </body>
                                </html>
                            ";

                        var sent = await _emailService.SendAlertEmailAsync(targetEmail, emailSubject, emailBody);
                        alert.Status = sent ? "sent" : "failed";
                        _logger.LogInformation("  📧 EMAIL {Result} a {Email} ({Name})", sent ? "ENVIADO" : "FALLIDO", targetEmail, targetName ?? "Sin nombre");
                    }
                    catch (Exception emailEx)
                    {
                        alert.Status = "failed";
                        _logger.LogError(emailEx, "  ❌ Error al enviar email a {Email}", targetEmail);
                    }
                }

                // 📣 Notificar al responsable (quien creó/envió el formulario) solo internamente en el sistema (sin enviar correo)
                if (!string.IsNullOrWhiteSpace(form.FilledByEmail) && alertasCreadas > 0)
                {
                    var responsableEmail = form.FilledByEmail.Trim();
                    var responsableAlert = new Alert
                    {
                        Type = "signature_creator_notice",
                        Priority = "medium",
                        Title = $"Solicitudes de firma enviadas: {templateName}",
                        Message = $"Se enviaron {alertasCreadas} solicitudes de firma para el formulario {formCode}.",
                        TargetEmail = responsableEmail,
                        FormId = form.FormID,
                        FormCode = formCode,
                        CreatedDate = DateTime.Now,
                        IsRead = false,
                        Status = "sent_internal"
                    };

                    _context.Set<Alert>().Add(responsableAlert);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("📨 {Count} alertas creadas para formulario {FormId}", alertasCreadas, form.FormID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al crear alertas iniciales para formulario {FormId}", form.FormID);
                // No lanzar excepción para no bloquear la creación del formulario
            }
        }
        
        private async Task IndexTraceabilityRecords(FilledForm filledForm, Template template)
        {
            // Clear existing records for this form if any (for Put/Patch)
            var existingRecords = await _context.LotesTrazabilidad.Where(x => x.FormID == filledForm.FormID).ToListAsync();
            if (existingRecords.Any())
            {
                _context.LotesTrazabilidad.RemoveRange(existingRecords);
            }

            var lotesEncontrados = new HashSet<string>();
            string? producto = filledForm.TipoProducto;
            string? subproducto = null;

            if (!string.IsNullOrEmpty(filledForm.HeaderData))
            {
                try
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, object>>(filledForm.HeaderData);
                    if (headerDict != null)
                    {
                        foreach (var kvp in headerDict)
                        {
                            if (kvp.Key.Contains("lote", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                            {
                                // Si el valor es un JsonElement (puede ser array lote_entrante o string simple)
                                if (kvp.Value is JsonElement je)
                                {
                                    if (je.ValueKind == JsonValueKind.Array)
                                    {
                                        // lote_entrante: array de objetos con campo "lote"
                                        foreach (var entry in je.EnumerateArray())
                                        {
                                            if (entry.ValueKind != JsonValueKind.Object) continue;
                                            foreach (var prop in entry.EnumerateObject())
                                            {
                                                if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var lv = prop.Value.GetString()?.Trim();
                                                    if (!string.IsNullOrEmpty(lv) && lv.Length <= 100)
                                                        lotesEncontrados.Add(lv);
                                                }
                                            }
                                        }
                                    }
                                    else if (je.ValueKind == JsonValueKind.String)
                                    {
                                        var lv = je.GetString()?.Trim();
                                        if (!string.IsNullOrEmpty(lv) && lv.Length <= 100)
                                            lotesEncontrados.Add(lv);
                                    }
                                }
                                else
                                {
                                    var val = kvp.Value.ToString()?.Trim();
                                    if (!string.IsNullOrEmpty(val) && val.Length <= 100)
                                        lotesEncontrados.Add(val);
                                }
                            }
                            else if (kvp.Key.Contains("producto", StringComparison.OrdinalIgnoreCase) && 
                                     !kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && 
                                     kvp.Value != null && producto == null)
                            {
                                producto = kvp.Value is JsonElement jeProd && jeProd.ValueKind == JsonValueKind.String
                                    ? jeProd.GetString()?.Trim()
                                    : kvp.Value.ToString()?.Trim();
                            }
                            else if (kvp.Key.Contains("subproducto", StringComparison.OrdinalIgnoreCase) && kvp.Value != null)
                            {
                                subproducto = kvp.Value is JsonElement jeSub && jeSub.ValueKind == JsonValueKind.String
                                    ? jeSub.GetString()?.Trim()
                                    : kvp.Value.ToString()?.Trim();
                            }
                        }
                    }
                }
                catch { }
            }

            // Search in BodyData
            if (!string.IsNullOrEmpty(filledForm.BodyData))
            {
                try
                {
                    var bodyData = JsonSerializer.Deserialize<List<JsonElement>>(filledForm.BodyData);
                    if (bodyData != null)
                    {
                        foreach (var element in bodyData)
                        {
                            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "table")
                            {
                                if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var row in dataProp.EnumerateArray())
                                    {
                                        if (row.ValueKind == JsonValueKind.Object)
                                        {
                                            foreach (var prop in row.EnumerateObject())
                                            {
                                                if (prop.Name.Contains("lote", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var val = prop.Value.ToString()?.Trim();
                                                    if (!string.IsNullOrEmpty(val))
                                                        lotesEncontrados.Add(val);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Guardar en la tabla LotesTrazabilidad
            foreach (var lote in lotesEncontrados)
            {
                _context.LotesTrazabilidad.Add(new LoteTrazabilidad
                {
                    FormID = filledForm.FormID,
                    LoteOrigen = lote,
                    Proceso = template.Proceso ?? "Desconocido",
                    Producto = producto,
                    CantidadEntrada = 0,
                    CantidadSalida = 0,
                    FechaRegistro = filledForm.CreatedAt
                });
            }

            await _context.SaveChangesAsync();
        }
    }

    // DTO para recibir datos de entrada
    public class FilledFormInputDto
    {
        public int TemplateID { get; set; }
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? TipoProducto { get; set; } // 🦐🐟 NUEVO: Tipo de producto
        public string? Observaciones { get; set; }
        
        // ✅ AUDITORÍA: Datos del usuario que crea el formulario
        public string? FilledBy { get; set; }
        public string? FilledByEmail { get; set; }
        public string? FilledByRole { get; set; }
    }

    // DTO para autoguardado (campos opcionales)
    public class AutosaveDto
    {
        public string? HeaderData { get; set; }
        public string? BodyData { get; set; }
        public string? FirmasData { get; set; }
        public string? TipoProducto { get; set; } // 🦐🐟 NUEVO
        public string? Observaciones { get; set; }

        // ✅ AUDITORÍA: quién está modificando (para el historial de cambios)
        public string? FilledBy { get; set; }
        public string? FilledByEmail { get; set; }
        public string? FilledByRole { get; set; }
    }

    /// <summary>Un valor que cambió dentro de un formulario ya guardado.</summary>
    public class CambioValorDto
    {
        /// <summary>Clave para ubicar la celda desde el front: "h:CAMPO" o "b:0:3:COLUMNA".</summary>
        public string Clave { get; set; } = string.Empty;
        /// <summary>"encabezado" | "tabla" | "seccion"</summary>
        public string Ambito { get; set; } = string.Empty;
        public int? Elemento { get; set; }
        public int? Fila { get; set; }
        public string Campo { get; set; } = string.Empty;
        public string? Antes { get; set; }
        public string? Despues { get; set; }
    }
}