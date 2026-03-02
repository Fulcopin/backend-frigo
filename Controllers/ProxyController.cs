using Microsoft.AspNetCore.Mvc;

namespace FormBuilder.API.Controllers;

/// <summary>
/// Proxy genérico para reenviar llamadas a la API externa (http) desde el backend (https).
/// Resuelve el problema de Mixed Content cuando el frontend está en HTTPS.
/// Todas las rutas bajo /api/Proxy/{**path} se reenvían a http://188.40.197.172:8094/api/{path}
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ProxyController> _logger;
    private const string ExternalApiBase = "http://188.40.197.172:8094/api";

    public ProxyController(IHttpClientFactory httpClientFactory, ILogger<ProxyController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Proxy para GET requests
    /// </summary>
    [HttpGet("{**path}")]
    public async Task<IActionResult> ProxyGet(string path)
    {
        return await ForwardRequest(path, HttpMethod.Get);
    }

    /// <summary>
    /// Proxy para POST requests
    /// </summary>
    [HttpPost("{**path}")]
    public async Task<IActionResult> ProxyPost(string path)
    {
        return await ForwardRequest(path, HttpMethod.Post);
    }

    /// <summary>
    /// Proxy para PUT requests
    /// </summary>
    [HttpPut("{**path}")]
    public async Task<IActionResult> ProxyPut(string path)
    {
        return await ForwardRequest(path, HttpMethod.Put);
    }

    /// <summary>
    /// Proxy para DELETE requests
    /// </summary>
    [HttpDelete("{**path}")]
    public async Task<IActionResult> ProxyDelete(string path)
    {
        return await ForwardRequest(path, HttpMethod.Delete);
    }

    private async Task<IActionResult> ForwardRequest(string path, HttpMethod method)
    {
        // Construir la URL destino conservando query string
        var queryString = Request.QueryString.Value ?? "";
        var targetUrl = $"{ExternalApiBase}/{path}{queryString}";

        _logger.LogInformation("🔀 Proxy {Method} → {Url}", method.Method, targetUrl);

        try
        {
            var client = _httpClientFactory.CreateClient("ExternalApi");
            var requestMessage = new HttpRequestMessage(method, targetUrl);

            // Reenviar el Authorization header si existe
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            // Reenviar el body para POST/PUT
            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                var body = await new StreamReader(Request.Body).ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    requestMessage.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                }
            }

            var response = await client.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";

            _logger.LogInformation("✅ Proxy respuesta: {StatusCode}", response.StatusCode);

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = responseContent,
                ContentType = contentType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en proxy hacia {Url}", targetUrl);
            return StatusCode(502, new { error = "Error al conectar con la API externa", detail = ex.Message });
        }
    }
}
