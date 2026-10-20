using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using FormBuilder.API.Services;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// ✍️ FIRMA CON PIN EN UNA CELDA (columna tipo "firmaPin")
    ///
    ///   POST api/FirmasCelda/firmar            verifica el PIN y registra la firma
    ///   POST api/FirmasCelda/firmar-documento  persona SIN usuario: nombre + cédula/pasaporte
    ///   GET  api/FirmasCelda/{codigo}          ver / verificar una firma por su código
    ///   GET  api/FirmasCelda?formId=123        firmas registradas de un formulario
    ///   POST api/FirmasCelda/{codigo}/anular   la firma se quitó de la celda
    ///
    /// El PIN es el mismo de "Mi Firma" (CatalogoFirmas.PinHash). Se puede firmar
    /// solo con el nombre o también con la imagen de la firma guardada.
    /// Tras 5 PIN incorrectos seguidos, esa persona queda bloqueada 15 minutos.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class FirmasCeldaController : ControllerBase
    {
        // Sin letras que se confunden (0/O, 1/I/L) para poder dictarlo o tipearlo.
        private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int LargoCodigo = 8;

        private readonly ApplicationDbContext _context;
        private readonly ILogger<FirmasCeldaController> _logger;

        public FirmasCeldaController(ApplicationDbContext context, ILogger<FirmasCeldaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public class FirmarCeldaRequest
        {
            /// <summary>Nombre completo tal como está en el catálogo de firmas.</summary>
            public string? Nombre { get; set; }
            public string? Pin { get; set; }
            /// <summary>true: incluir la imagen de la firma guardada en "Mi Firma".</summary>
            public bool ConImagen { get; set; }

            public string? Campo { get; set; }
            public int? Fila { get; set; }
            public int? FormId { get; set; }
            public int? TemplateId { get; set; }
            public string? UsuarioSesion { get; set; }
        }

        /// <summary>Firma de una persona sin usuario, con su documento.</summary>
        public class FirmarConDocumentoRequest
        {
            public string? Nombre { get; set; }
            /// <summary>"cedula" (por defecto) o "pasaporte".</summary>
            public string? TipoDocumento { get; set; }
            public string? NumeroDocumento { get; set; }

            public string? Campo { get; set; }
            public int? Fila { get; set; }
            public int? FormId { get; set; }
            public int? TemplateId { get; set; }
            /// <summary>Quien tiene la sesión abierta: da fe de la firma. Obligatorio.</summary>
            public string? UsuarioSesion { get; set; }
        }

        public class AnularRequest
        {
            public string? Usuario { get; set; }
        }

        /// <summary>Texto que queda en la celda: se lee igual en pantalla, PDF y Excel.</summary>
        public static string TextoDeCelda(string nombre, DateTime fecha, string codigo) =>
            $"✍️ Firmado digitalmente por: {nombre} · {fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} · Cód. {codigo}";

        /// <summary>Texto de una firma con documento (persona sin usuario).</summary>
        public static string TextoDeCeldaDocumento(string nombre, string? tipoDocumento, string numero, DateTime fecha, string codigo) =>
            $"🪪 Firmado por: {nombre} · {DocumentoIdentidad.Etiqueta(tipoDocumento)} {numero} · {fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)} · Cód. {codigo}";

        private static string TextoDe(FirmaCelda f) =>
            f.TipoFirma == "documento"
                ? TextoDeCeldaDocumento(f.NombreCompleto, f.TipoDocumento, f.NumeroDocumento ?? "", f.FirmadoEn, f.Codigo)
                : TextoDeCelda(f.NombreCompleto, f.FirmadoEn, f.Codigo);

        private async Task<string> CodigoUnicoAsync()
        {
            var codigo = NuevoCodigo();
            // Reintenta en el caso rarísimo de repetirse.
            for (var intento = 0; intento < 5 && await _context.FirmasCelda.AnyAsync(f => f.Codigo == codigo); intento++)
                codigo = NuevoCodigo();
            return codigo;
        }

        private static string NuevoCodigo()
        {
            var chars = new char[LargoCodigo];
            for (var i = 0; i < chars.Length; i++)
                chars[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            return new string(chars);
        }

        private static object Vista(FirmaCelda f) => new
        {
            f.Codigo,
            f.NombreCompleto,
            f.Puesto,
            f.ConImagen,
            FirmaImageUrl = f.ConImagen ? f.FirmaImageUrl : null,
            f.TipoFirma,
            f.TipoDocumento,
            f.NumeroDocumento,
            f.FirmadoEn,
            f.Campo,
            f.Fila,
            f.FormId,
            f.TemplateId,
            f.Anulada,
            f.AnuladaEn,
            f.AnuladaPor,
            Texto = TextoDe(f)
        };

        [HttpPost("firmar")]
        public async Task<IActionResult> Firmar([FromBody] FirmarCeldaRequest request)
        {
            var nombre = request?.Nombre?.Trim() ?? "";
            var pin = request?.Pin?.Trim() ?? "";
            if (nombre.Length == 0 || pin.Length == 0)
                return BadRequest(new { message = "Elige quién firma e ingresa el PIN." });

            var bloqueo = PinFirma.Bloqueado(nombre);
            if (bloqueo.HasValue)
                return StatusCode(429, new
                {
                    message = $"Demasiados PIN incorrectos para {nombre}. Intenta de nuevo en {Math.Ceiling(bloqueo.Value.TotalMinutes)} minuto(s)."
                });

            var persona = await _context.CatalogoFirmas
                .FirstOrDefaultAsync(f => f.Activo && f.NombreCompleto != null
                                          && f.NombreCompleto.ToLower() == nombre.ToLower());
            if (persona == null)
                return NotFound(new { message = $"{nombre} no está en el catálogo de firmas." });

            if (string.IsNullOrEmpty(persona.PinHash))
                return BadRequest(new { message = $"{nombre} no tiene PIN. Debe crearlo en 'Mi Firma'." });

            if (!string.Equals(persona.PinHash, PinFirma.Hash(pin), StringComparison.OrdinalIgnoreCase))
            {
                var quedan = PinFirma.Fallo(nombre);
                _logger.LogWarning("✍️ PIN incorrecto al firmar celda como {Nombre} (quedan {Quedan})", nombre, quedan);
                return quedan == 0
                    ? StatusCode(429, new { message = $"PIN incorrecto. {nombre} quedó bloqueado {PinFirma.Bloqueo.TotalMinutes:0} minutos." })
                    : Unauthorized(new { message = $"PIN incorrecto. Quedan {quedan} intento(s)." });
            }
            PinFirma.Exito(nombre);

            if (request!.ConImagen && string.IsNullOrEmpty(persona.FirmaImageUrl))
                return BadRequest(new
                {
                    message = $"{nombre} no tiene una imagen de firma guardada. Firma solo con el nombre o súbela en 'Mi Firma'."
                });

            var firma = new FirmaCelda
            {
                CatalogoFirmaID = persona.CatalogoFirmaID,
                NombreCompleto = persona.NombreCompleto!,
                Puesto = persona.Puesto,
                Correo = persona.Correo,
                ConImagen = request.ConImagen,
                FirmaImageUrl = request.ConImagen ? persona.FirmaImageUrl : null,
                Campo = request.Campo?.Trim(),
                Fila = request.Fila,
                FormId = request.FormId,
                TemplateId = request.TemplateId,
                UsuarioSesion = request.UsuarioSesion?.Trim(),
                Ip = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                FirmadoEn = DateTime.Now
            };

            firma.Codigo = await CodigoUnicoAsync();

            try
            {
                _context.FirmasCelda.Add(firma);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar firma de celda");
                return StatusCode(500, new { message = "No se pudo registrar la firma.", detalle = ex.GetBaseException().Message });
            }

            _logger.LogInformation("✍️ Celda firmada por {Nombre} ({Codigo}) campo={Campo} fila={Fila}",
                firma.NombreCompleto, firma.Codigo, firma.Campo, firma.Fila);
            return Ok(Vista(firma));
        }

        /// <summary>
        /// 🪪 Firma de una persona SIN usuario (visitante, contratista, operario
        /// sin cuenta): nombre completo + cédula o pasaporte.
        ///
        /// No hay PIN que la verifique. Se valida el documento (dígito
        /// verificador de la cédula) y queda registrado quién tenía la sesión
        /// abierta en el equipo, que es quien da fe de la firma.
        /// </summary>
        [HttpPost("firmar-documento")]
        public async Task<IActionResult> FirmarConDocumento([FromBody] FirmarConDocumentoRequest request)
        {
            var nombre = System.Text.RegularExpressions.Regex.Replace(request?.Nombre ?? "", @"\s+", " ").Trim();
            var tipo = string.IsNullOrWhiteSpace(request?.TipoDocumento)
                ? DocumentoIdentidad.Cedula
                : request!.TipoDocumento!.Trim().ToLowerInvariant();
            var numero = DocumentoIdentidad.Limpiar(request?.NumeroDocumento);
            var sesion = request?.UsuarioSesion?.Trim() ?? "";

            if (sesion.Length == 0)
                return BadRequest(new { message = "Tiene que haber una sesión abierta: quien la tiene da fe de esta firma." });

            if (nombre.Length < 5 || nombre.Length > 200 || nombre.Split(' ').Length < 2
                || !System.Text.RegularExpressions.Regex.IsMatch(nombre, @"^[\p{L}][\p{L} .'\-]*$"))
                return BadRequest(new { message = "Escribe el nombre y el apellido de quien firma (solo letras)." });

            var error = DocumentoIdentidad.Validar(tipo, numero);
            if (error != null) return BadRequest(new { message = error });

            var firma = new FirmaCelda
            {
                TipoFirma = "documento",
                TipoDocumento = tipo,
                NumeroDocumento = numero,
                NombreCompleto = nombre,
                ConImagen = false,
                Campo = request!.Campo?.Trim(),
                Fila = request.Fila,
                FormId = request.FormId,
                TemplateId = request.TemplateId,
                UsuarioSesion = sesion,
                Ip = HttpContext?.Connection?.RemoteIpAddress?.ToString(),
                FirmadoEn = DateTime.Now,
                Codigo = await CodigoUnicoAsync(),
            };

            try
            {
                _context.FirmasCelda.Add(firma);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar firma con documento");
                return StatusCode(500, new { message = "No se pudo registrar la firma.", detalle = ex.GetBaseException().Message });
            }

            _logger.LogInformation("🪪 Celda firmada con documento por {Nombre} ({Codigo}), da fe {Sesion}",
                firma.NombreCompleto, firma.Codigo, sesion);
            return Ok(Vista(firma));
        }

        /// <summary>Ver / verificar una firma por el código que aparece en la celda.</summary>
        [HttpGet("{codigo}")]
        public async Task<IActionResult> Ver(string codigo)
        {
            var c = (codigo ?? "").Trim().ToUpperInvariant();
            var firma = await _context.FirmasCelda.AsNoTracking().FirstOrDefaultAsync(f => f.Codigo == c);
            return firma == null
                ? NotFound(new { message = "No existe una firma con ese código." })
                : Ok(Vista(firma));
        }

        /// <summary>Firmas registradas de un formulario (las que se hicieron editándolo).</summary>
        [HttpGet]
        public async Task<IActionResult> DeFormulario([FromQuery] int formId)
        {
            var lista = await _context.FirmasCelda.AsNoTracking()
                .Where(f => f.FormId == formId)
                .OrderBy(f => f.FirmadoEn)
                .ToListAsync();
            return Ok(lista.Select(Vista));
        }

        /// <summary>La firma se quitó de la celda: queda registrada como anulada (no se borra).</summary>
        [HttpPost("{codigo}/anular")]
        public async Task<IActionResult> Anular(string codigo, [FromBody] AnularRequest? request)
        {
            var c = (codigo ?? "").Trim().ToUpperInvariant();
            var firma = await _context.FirmasCelda.FirstOrDefaultAsync(f => f.Codigo == c);
            if (firma == null) return NotFound(new { message = "No existe una firma con ese código." });

            if (!firma.Anulada)
            {
                firma.Anulada = true;
                firma.AnuladaEn = DateTime.Now;
                firma.AnuladaPor = request?.Usuario?.Trim();
                await _context.SaveChangesAsync();
            }
            return Ok(Vista(firma));
        }
    }
}