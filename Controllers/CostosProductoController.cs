using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Costos unitarios por producto. Los carga el área de Costos a mano y son
    /// los que le ponen valor al kardex: el inventario de lotes solo lleva libras.
    ///
    /// La clave real es el NOMBRE del producto (el mismo que guarda el inventario
    /// de lotes), no el Id: por eso el alta es un upsert por nombre.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CostosProductoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CostosProductoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── GET /api/CostosProducto ───────────────────────────────────────────
        /// <summary>Todos los costos cargados, ordenados por producto.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CostoProducto>>> GetAll([FromQuery] string? producto)
        {
            var query = _context.CostosProducto.AsQueryable();

            if (!string.IsNullOrWhiteSpace(producto))
                query = query.Where(c => c.Producto.Contains(producto));

            return await query.OrderBy(c => c.Producto).ToListAsync();
        }

        // ── PUT /api/CostosProducto ───────────────────────────────────────────
        /// <summary>
        /// Guarda el costo de un producto. Si ya existía se actualiza, si no se crea.
        /// Un costo en 0 o negativo se rechaza: deja el kardex valorizado en nada
        /// y es casi siempre un campo que quedó vacío por error.
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<CostoProducto>> Upsert([FromBody] CostoProductoUpsertDto dto)
        {
            var nombre = (dto.Producto ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                return BadRequest(new { message = "El producto es obligatorio." });

            if (dto.CostoUnitario <= 0)
                return BadRequest(new { message = "El costo unitario debe ser mayor que cero." });

            var existente = await _context.CostosProducto
                .FirstOrDefaultAsync(c => c.Producto == nombre);

            if (existente == null)
            {
                existente = new CostoProducto
                {
                    Producto = nombre,
                    CreadoEn = DateTime.UtcNow
                };
                _context.CostosProducto.Add(existente);
            }

            existente.CostoUnitario  = dto.CostoUnitario;
            existente.Moneda         = string.IsNullOrWhiteSpace(dto.Moneda) ? "USD" : dto.Moneda.Trim();
            existente.Unidad         = string.IsNullOrWhiteSpace(dto.Unidad) ? "Lb"  : dto.Unidad.Trim();
            existente.Notas          = dto.Notas;
            existente.ActualizadoPor = dto.ActualizadoPor;
            existente.ActualizadoEn  = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(existente);
        }

        // ── PUT /api/CostosProducto/bulk ──────────────────────────────────────
        /// <summary>Carga varios costos de una sola vez (pegar desde Excel).</summary>
        [HttpPut("bulk")]
        public async Task<ActionResult> UpsertBulk([FromBody] List<CostoProductoUpsertDto> lista)
        {
            if (lista == null || lista.Count == 0)
                return BadRequest(new { message = "No se recibió ningún costo." });

            var guardados = 0;
            var rechazados = new List<string>();

            foreach (var dto in lista)
            {
                var nombre = (dto.Producto ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(nombre) || dto.CostoUnitario <= 0)
                {
                    rechazados.Add(string.IsNullOrWhiteSpace(nombre) ? "(sin nombre)" : nombre);
                    continue;
                }

                var existente = await _context.CostosProducto
                    .FirstOrDefaultAsync(c => c.Producto == nombre);

                if (existente == null)
                {
                    existente = new CostoProducto { Producto = nombre, CreadoEn = DateTime.UtcNow };
                    _context.CostosProducto.Add(existente);
                }

                existente.CostoUnitario  = dto.CostoUnitario;
                existente.Moneda         = string.IsNullOrWhiteSpace(dto.Moneda) ? "USD" : dto.Moneda.Trim();
                existente.Unidad         = string.IsNullOrWhiteSpace(dto.Unidad) ? "Lb"  : dto.Unidad.Trim();
                existente.Notas          = dto.Notas;
                existente.ActualizadoPor = dto.ActualizadoPor;
                existente.ActualizadoEn  = DateTime.UtcNow;
                guardados++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { guardados, rechazados });
        }

        // ── DELETE /api/CostosProducto/{id} ───────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var costo = await _context.CostosProducto.FindAsync(id);
            if (costo == null) return NotFound(new { message = "Costo no encontrado." });

            _context.CostosProducto.Remove(costo);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Costo de '{costo.Producto}' eliminado." });
        }
    }
}
