using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace FormBuilder.API.Controllers;

/// <summary>
/// Sirve las fotos y firmas de los formularios DESDE EL SERVIDOR LOCAL (misma IP),
/// dejando Cloudinary solo como respaldo.
///
/// Problema que resuelve: la red de la planta tiene bloqueado
/// res.cloudinary.com, así que el navegador no puede bajar las imágenes
/// directo de la nube (fotos tomadas en producción y firmas).
///
/// Cómo: el navegador de la planta pide la imagen a este servidor
/// (GET /api/Images/proxy?u=&lt;url&gt;). El servidor SÍ tiene salida a internet,
/// así que la baja de Cloudinary, la guarda en disco y la devuelve. Las
/// siguientes veces la sirve del disco, sin depender de internet.
///
/// El dato guardado en el formulario NO cambia: sigue siendo la URL de Cloudinary
/// (el respaldo). Lo que cambia es por dónde la pide el navegador.
///
/// 📁 Dos garantías para que esto funcione aunque el IIS no dé permisos:
///  1. La carpeta de guardado se busca sola entre varias opciones, quedándose con
///     la primera donde el servidor pueda escribir (no hace falta tocar permisos).
///     Se puede fijar con appsettings.json -> "Imagenes:Cache".
///  2. Si NO hay ninguna carpeta escribible, la imagen igual se sirve: se baja al
///     momento y se devuelve sin guardar copia. El caché es una optimización,
///     no un requisito para ver la foto.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    /// <summary>Solo se proxean URLs de Cloudinary (evita que el endpoint sirva
    /// como proxy abierto hacia cualquier sitio: SSRF).</summary>
    private const string PrefijoCloudinary = "https://res.cloudinary.com/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<ImagesController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _env = env;
        _config = config;
        _logger = logger;
    }

    private string? _carpetaResuelta;
    private bool _carpetaYaBuscada;

    /// <summary>Lugares que se prueban, en orden, hasta encontrar uno escribible.</summary>
    private List<string> CarpetasCandidatas()
    {
        var candidatas = new List<string>();

        void Agregar(string? ruta)
        {
            if (!string.IsNullOrWhiteSpace(ruta) && !candidatas.Contains(ruta!))
                candidatas.Add(ruta!);
        }

        Agregar(_config["Imagenes:Cache"]);

        // Dentro de la carpeta de la app (lo natural: queda junto al despliegue).
        Agregar(Path.Combine(_env.ContentRootPath, "App_Data", "imagenes-cache"));
        Agregar(Path.Combine(_env.ContentRootPath, "imagenes-cache"));
        Agregar(Path.Combine(Path.GetDirectoryName(_env.ContentRootPath) ?? _env.ContentRootPath, "imagenes-cache"));

        // Perfil del propio App Pool: siempre es suyo, casi siempre escribible.
        try { Agregar(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "frigo-imagenes-cache")); } catch { }
        try { Agregar(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "frigo-imagenes-cache")); } catch { }

        // Carpetas temporales del sistema.
        try { Agregar(Path.Combine(Path.GetTempPath(), "frigo-imagenes-cache")); } catch { }
        try { Agregar(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Frigo", "imagenes-cache")); } catch { }

        return candidatas;
    }

    /// <summary>Prueba real de escritura (no basta con que la carpeta exista).</summary>
    private static (bool ok, string? error) ProbarEscritura(string carpeta)
    {
        try
        {
            Directory.CreateDirectory(carpeta);
            var prueba = Path.Combine(carpeta, ".escritura-ok");
            System.IO.File.WriteAllText(prueba, "ok");
            System.IO.File.Delete(prueba);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Devuelve la primera carpeta donde se pueda escribir, o null si ninguna sirve.</summary>
    private string? ResolverCarpeta()
    {
        if (_carpetaYaBuscada) return _carpetaResuelta;
        _carpetaYaBuscada = true;

        foreach (var carpeta in CarpetasCandidatas())
        {
            var (ok, error) = ProbarEscritura(carpeta);
            if (ok)
            {
                _carpetaResuelta = carpeta;
                _logger.LogInformation("📁 Imágenes de formularios: guardando copias en {Carpeta}", carpeta);
                return carpeta;
            }

            _logger.LogWarning("⚠️ Imágenes: no se puede usar {Carpeta}: {Mensaje}", carpeta, error);
        }

        _logger.LogWarning("ℹ️ Imágenes: ninguna carpeta resultó escribible. Se servirán las imágenes SIN guardar copia local.");
        return null;
    }

    private static bool EsDeCloudinary(string? u) =>
        !string.IsNullOrWhiteSpace(u) &&
        u.StartsWith(PrefijoCloudinary, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nombre de archivo estable a partir de la URL (misma URL = mismo archivo).</summary>
    private static string Hash(string texto)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ExtensionDe(string url)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath);
            if (!string.IsNullOrWhiteSpace(ext) && ext.Length <= 6 && ext.IndexOf('/') < 0)
                return ext.ToLowerInvariant();
        }
        catch { /* URL rara: se cae al default */ }
        return ".jpg";
    }

    private static string TipoDe(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        ".avif" => "image/avif",
        ".tif" or ".tiff" => "image/tiff",
        _ => "image/jpeg",
    };

    /// <summary>
    /// GET /api/Images/proxy?u=&lt;url de cloudinary&gt;
    /// Devuelve la imagen desde el disco del servidor; si no está, la baja una vez.
    /// Si no se puede guardar copia, igual la devuelve (la foto nunca se bloquea).
    /// </summary>
    [HttpGet("proxy")]
    public async Task<IActionResult> Proxy([FromQuery] string? u)
    {
        if (!EsDeCloudinary(u))
            return BadRequest(new { error = "URL no permitida (solo res.cloudinary.com)" });

        var ext = ExtensionDe(u!);

        try
        {
            // 1) ¿Ya está guardada de antes? Se sirve al instante, sin internet.
            var carpeta = ResolverCarpeta();
            if (carpeta != null)
            {
                var ruta = Path.Combine(carpeta, Hash(u!) + ext);
                if (System.IO.File.Exists(ruta))
                {
                    Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return File(await System.IO.File.ReadAllBytesAsync(ruta), TipoDe(ext));
                }
            }

            // 2) No está: se baja de Cloudinary (el SERVIDOR sí tiene salida a internet).
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            using var resp = await client.GetAsync(u);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("☁️ Cloudinary respondió {Status} para {Url}", (int)resp.StatusCode, u);
                return StatusCode(502, new { error = $"Cloudinary respondió {(int)resp.StatusCode}" });
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync();

            // 3) Se intenta guardar la copia. Si el IIS no permite escribir, la
            //    imagen IGUAL se devuelve: el caché es una optimización, no un
            //    requisito para ver la foto en la planta.
            if (carpeta != null)
            {
                try
                {
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(carpeta, Hash(u!) + ext), bytes);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Imágenes: no se pudo guardar la copia ({Carpeta}): {Mensaje}", carpeta, ex.Message);
                }
            }

            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return File(bytes, TipoDe(ext));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error sirviendo imagen: {Url}", u);
            return StatusCode(502, new { error = "No se pudo obtener la imagen", detail = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/Images/prefetch — baja en lote una lista de URLs y las deja
    /// cacheadas (sirve para "traer" de una las imágenes ya subidas).
    /// </summary>
    [HttpPost("prefetch")]
    public async Task<IActionResult> Prefetch([FromBody] PrefetchRequest? request)
    {
        var urls = request?.Urls?.Where(EsDeCloudinary).Select(x => x!).Distinct().Take(2000).ToList() ?? new();
        if (urls.Count == 0) return BadRequest(new { error = "Sin URLs de Cloudinary" });

        var carpeta = ResolverCarpeta();
        if (carpeta == null)
        {
            return StatusCode(500, new
            {
                error = "El servidor no tiene ninguna carpeta donde guardar las imágenes",
                detalle = "Las imágenes igual se ven (se bajan al momento), pero no quedan guardadas. " +
                          "Se puede fijar una carpeta con appsettings.json -> Imagenes:Cache.",
                candidatas = CarpetasCandidatas()
                    .Select(c => new { carpeta = c, error = ProbarEscritura(c).error })
            });
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            int bajadas = 0, yaEstaban = 0, fallidas = 0;
            var errores = new List<string>();

            foreach (var u in urls)
            {
                var ext = ExtensionDe(u);
                var ruta = Path.Combine(carpeta, Hash(u) + ext);
                if (System.IO.File.Exists(ruta)) { yaEstaban++; continue; }

                try
                {
                    using var resp = await client.GetAsync(u);
                    if (!resp.IsSuccessStatusCode) { fallidas++; continue; }
                    var bytes = await resp.Content.ReadAsByteArrayAsync();
                    await System.IO.File.WriteAllBytesAsync(ruta, bytes);
                    bajadas++;
                }
                catch (Exception ex)
                {
                    fallidas++;
                    if (errores.Count < 5) errores.Add(ex.Message);
                }
            }

            return Ok(new { recibidas = urls.Count, bajadas, yaEstaban, fallidas, errores });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en prefetch de imágenes");
            return StatusCode(500, new { error = "No se pudieron traer las imágenes", detail = ex.Message });
        }
    }

    /// <summary>
    /// GET /api/Images/diagnostico — ¿el servidor tiene salida a internet para
    /// bajar de Cloudinary? ¿Dónde guarda, puede escribir y cuántas copias tiene?
    /// Sirve para revisar el servidor sin entrar por escritorio remoto.
    /// </summary>
    [HttpGet("diagnostico")]
    public async Task<IActionResult> Diagnostico()
    {
        var carpeta = ResolverCarpeta();
        var archivos = (carpeta != null && System.IO.Directory.Exists(carpeta))
            ? System.IO.Directory.GetFiles(carpeta).Length
            : 0;

        var candidatas = CarpetasCandidatas().Select(c =>
        {
            var (ok, error) = ProbarEscritura(c);
            return new { carpeta = c, escribible = ok, error };
        });

        var prueba = "https://res.cloudinary.com/dpczd4ufe/image/upload/sample.jpg";
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var resp = await client.GetAsync(prueba);
            return Ok(new
            {
                puedeBajarDeCloudinary = resp.IsSuccessStatusCode,
                status = (int)resp.StatusCode,
                puedeEscribir = carpeta != null,
                carpeta,
                archivos,
                version = "2",
                candidatas
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                puedeBajarDeCloudinary = false,
                error = ex.Message,
                puedeEscribir = carpeta != null,
                carpeta,
                archivos,
                version = "2",
                candidatas
            });
        }
    }

    public class PrefetchRequest
    {
        public List<string?>? Urls { get; set; }
    }
}
