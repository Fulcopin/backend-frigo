using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogoFirmasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CatalogoFirmasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CatalogoFirmas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatalogoFirma>>> GetCatalogoFirmas([FromQuery] bool soloActivos = true)
        {
            var query = _context.CatalogoFirmas.AsQueryable();

            if (soloActivos)
            {
                query = query.Where(f => f.Activo);
            }

            var firmas = await query
                .OrderBy(f => f.Puesto)
                .ThenBy(f => f.NombreCompleto)
                .ToListAsync();

            return Ok(firmas);
        }

        // GET: api/CatalogoFirmas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CatalogoFirma>> GetCatalogoFirma(int id)
        {
            var firma = await _context.CatalogoFirmas.FindAsync(id);

            if (firma == null)
            {
                return NotFound();
            }

            return Ok(firma);
        }

        // POST: api/CatalogoFirmas
        [HttpPost]
        public async Task<ActionResult<CatalogoFirma>> PostCatalogoFirma(CatalogoFirma firma)
        {
            firma.FechaCreacion = DateTime.Now;
            _context.CatalogoFirmas.Add(firma);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCatalogoFirma), new { id = firma.CatalogoFirmaID }, firma);
        }

        // PUT: api/CatalogoFirmas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCatalogoFirma(int id, CatalogoFirma firma)
        {
            if (id != firma.CatalogoFirmaID)
            {
                return BadRequest();
            }

            _context.Entry(firma).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CatalogoFirmaExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/CatalogoFirmas/5 (Borrado lógico)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCatalogoFirma(int id)
        {
            var firma = await _context.CatalogoFirmas.FindAsync(id);
            if (firma == null)
            {
                return NotFound();
            }

            // Borrado lógico
            firma.Activo = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/CatalogoFirmas/by-email/{email} — Buscar firma guardada por correo
        [HttpGet("by-email/{email}")]
        public async Task<ActionResult<CatalogoFirma>> GetByEmail(string email)
        {
            var firma = await _context.CatalogoFirmas
                .Where(f => f.Activo && f.Correo != null && f.Correo.ToLower() == email.ToLower())
                .FirstOrDefaultAsync();

            if (firma == null)
            {
                return NotFound(new { message = "No se encontró firma para este correo" });
            }

            return Ok(firma);
        }

        // GET: api/CatalogoFirmas/by-nombre/{nombre} — Buscar por nombre completo
        [HttpGet("by-nombre/{nombre}")]
        public async Task<ActionResult<CatalogoFirma>> GetByNombre(string nombre)
        {
            var firma = await _context.CatalogoFirmas
                .Where(f => f.Activo && f.NombreCompleto != null && f.NombreCompleto.ToLower() == nombre.ToLower())
                .FirstOrDefaultAsync();

            if (firma == null)
            {
                return NotFound(new { message = "No se encontró firma para este nombre" });
            }

            return Ok(firma);
        }

        // POST: api/CatalogoFirmas/guardar-firma — Guardar/actualizar firma de usuario (upsert)
        [HttpPost("guardar-firma")]
        public async Task<ActionResult<CatalogoFirma>> GuardarFirma([FromBody] GuardarFirmaRequest request)
        {
            if (string.IsNullOrEmpty(request.FirmaImageUrl))
            {
                return BadRequest(new { message = "La URL de la firma es requerida" });
            }

            // Buscar registro existente por correo o nombre
            CatalogoFirma? firma = null;

            if (!string.IsNullOrEmpty(request.Correo))
            {
                firma = await _context.CatalogoFirmas
                    .Where(f => f.Activo && f.Correo != null && f.Correo.ToLower() == request.Correo.ToLower())
                    .FirstOrDefaultAsync();
            }

            if (firma == null && !string.IsNullOrEmpty(request.NombreCompleto))
            {
                firma = await _context.CatalogoFirmas
                    .Where(f => f.Activo && f.NombreCompleto != null && f.NombreCompleto.ToLower() == request.NombreCompleto.ToLower())
                    .FirstOrDefaultAsync();
            }

            if (firma != null)
            {
                // Actualizar registro existente
                firma.FirmaImageUrl = request.FirmaImageUrl;
                if (!string.IsNullOrEmpty(request.NombreCompleto))
                    firma.NombreCompleto = request.NombreCompleto;
                if (!string.IsNullOrEmpty(request.Correo))
                    firma.Correo = request.Correo;
            }
            else
            {
                // Crear nuevo registro
                firma = new CatalogoFirma
                {
                    Puesto = request.Puesto ?? "Sin asignar",
                    NombreCompleto = request.NombreCompleto,
                    Correo = request.Correo,
                    FirmaImageUrl = request.FirmaImageUrl,
                    Activo = true,
                    FechaCreacion = DateTime.Now
                };
                _context.CatalogoFirmas.Add(firma);
            }

            await _context.SaveChangesAsync();

            return Ok(firma);
        }

        // POST: api/CatalogoFirmas/set-pin — El usuario configura/actualiza su PIN personal (autoservicio)
        [HttpPost("set-pin")]
        public async Task<ActionResult> SetPin([FromBody] SetPinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Trim().Length < 4)
            {
                return BadRequest(new { message = "El PIN debe tener al menos 4 dígitos" });
            }

            // Buscar registro existente por correo, luego por nombre (mismo upsert que guardar-firma)
            CatalogoFirma? firma = null;

            if (!string.IsNullOrEmpty(request.Correo))
            {
                firma = await _context.CatalogoFirmas
                    .Where(f => f.Activo && f.Correo != null && f.Correo.ToLower() == request.Correo.ToLower())
                    .FirstOrDefaultAsync();
            }

            if (firma == null && !string.IsNullOrEmpty(request.NombreCompleto))
            {
                firma = await _context.CatalogoFirmas
                    .Where(f => f.Activo && f.NombreCompleto != null && f.NombreCompleto.ToLower() == request.NombreCompleto.ToLower())
                    .FirstOrDefaultAsync();
            }

            if (firma != null)
            {
                firma.PinHash = HashPin(request.Pin.Trim());
                if (!string.IsNullOrEmpty(request.NombreCompleto)) firma.NombreCompleto = request.NombreCompleto;
                if (!string.IsNullOrEmpty(request.Correo)) firma.Correo = request.Correo;
            }
            else
            {
                firma = new CatalogoFirma
                {
                    Puesto = "Sin asignar",
                    NombreCompleto = request.NombreCompleto,
                    Correo = request.Correo,
                    Activo = true,
                    FechaCreacion = DateTime.Now,
                    PinHash = HashPin(request.Pin.Trim())
                };
                _context.CatalogoFirmas.Add(firma);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "PIN guardado correctamente" });
        }

        // POST: api/CatalogoFirmas/verify-pin — Verifica el PIN de una persona y devuelve su firma guardada
        [HttpPost("verify-pin")]
        public async Task<ActionResult> VerifyPin([FromBody] VerifyPinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Pin))
            {
                return BadRequest(new { message = "Nombre y PIN son requeridos" });
            }

            var firma = await _context.CatalogoFirmas
                .Where(f => f.Activo && f.NombreCompleto != null && f.NombreCompleto.ToLower() == request.Nombre.Trim().ToLower())
                .FirstOrDefaultAsync();

            if (firma == null)
            {
                return NotFound(new { message = "No se encontró un registro de firma para esta persona" });
            }

            if (string.IsNullOrEmpty(firma.PinHash))
            {
                return BadRequest(new { message = "Esta persona no tiene un PIN configurado. Debe configurarlo en 'Mi Firma'." });
            }

            if (firma.PinHash != HashPin(request.Pin.Trim()))
            {
                return Unauthorized(new { message = "PIN incorrecto" });
            }

            if (string.IsNullOrEmpty(firma.FirmaImageUrl))
            {
                return BadRequest(new { message = "Esta persona no tiene una firma guardada. Debe subirla en 'Mi Firma'." });
            }

            return Ok(new { nombreCompleto = firma.NombreCompleto, firmaImageUrl = firma.FirmaImageUrl });
        }

        // Hash SHA-256 (hex) del PIN. El PIN nunca se guarda ni se devuelve en texto plano.
        private static string HashPin(string pin)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pin));
            return Convert.ToHexString(bytes);
        }

        private bool CatalogoFirmaExists(int id)
        {
            return _context.CatalogoFirmas.Any(e => e.CatalogoFirmaID == id);
        }
    }

    // DTO para guardar firma
    public class GuardarFirmaRequest
    {
        public string? Puesto { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Correo { get; set; }
        public string FirmaImageUrl { get; set; } = string.Empty;
    }

    // DTO para configurar el PIN personal
    public class SetPinRequest
    {
        public string? NombreCompleto { get; set; }
        public string? Correo { get; set; }
        public string Pin { get; set; } = string.Empty;
    }

    // DTO para verificar el PIN de una persona
    public class VerifyPinRequest
    {
        public string Nombre { get; set; } = string.Empty;
        public string Pin { get; set; } = string.Empty;
    }
}
