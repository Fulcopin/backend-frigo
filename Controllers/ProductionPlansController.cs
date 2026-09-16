using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionPlansController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductionPlansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ProductionPlans?fecha=YYYY-MM-DD&turno=DIA
        [HttpGet]
        public async Task<ActionResult<ProductionPlan>> GetProductionPlan([FromQuery] string fecha, [FromQuery] string turno)
        {
            if (!DateTime.TryParse(fecha, out DateTime fechaBusqueda))
            {
                return BadRequest("Formato de fecha inválido.");
            }

            var plan = await _context.ProductionPlans
                .FirstOrDefaultAsync(p => p.FechaOperacion.Date == fechaBusqueda.Date && p.Turno == turno);

            if (plan == null)
            {
                // En lugar de NotFound (que puede causar que IIS intercepte y quite los headers CORS),
                // devolvemos un 200 OK con un objeto vacío o indicación de que no existe.
                return Ok(new { isNew = true });
            }

            return Ok(plan);
        }

        // GET: api/ProductionPlans/historial?desde=2026-08-01&hasta=2026-08-20&turno=DIA
        /// <summary>
        /// Planes guardados en un rango de fechas, para el dashboard comparativo.
        /// Devuelve el PlanData completo de cada día: el frontend calcula los
        /// totales plan vs real por día y por actividad.
        /// </summary>
        [HttpGet("historial")]
        public async Task<ActionResult> GetHistorial(
            [FromQuery] string desde, [FromQuery] string hasta, [FromQuery] string? turno = null)
        {
            if (!DateTime.TryParse(desde, out var d1) || !DateTime.TryParse(hasta, out var d2))
                return BadRequest("Formato de fecha inválido (usar AAAA-MM-DD).");

            var query = _context.ProductionPlans
                .Where(p => p.FechaOperacion.Date >= d1.Date && p.FechaOperacion.Date <= d2.Date);

            if (!string.IsNullOrWhiteSpace(turno))
                query = query.Where(p => p.Turno == turno);

            var planes = await query
                .OrderBy(p => p.FechaOperacion).ThenBy(p => p.Turno)
                .Select(p => new { p.Id, p.FechaOperacion, p.Turno, p.PlanData, p.UpdatedAt })
                .ToListAsync();

            return Ok(planes);
        }

        // POST: api/ProductionPlans
        [HttpPost]
        public async Task<ActionResult<ProductionPlan>> SaveProductionPlan([FromBody] ProductionPlan request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Buscar si ya existe un plan para esa fecha y turno
            var existingPlan = await _context.ProductionPlans
                .FirstOrDefaultAsync(p => p.FechaOperacion.Date == request.FechaOperacion.Date && p.Turno == request.Turno);

            if (existingPlan != null)
            {
                // Actualizar el plan existente
                existingPlan.CustomFieldsDefinition = request.CustomFieldsDefinition;
                existingPlan.PlanData = request.PlanData;
                existingPlan.UpdatedAt = DateTime.Now;
                existingPlan.CreatedBy = request.CreatedBy ?? existingPlan.CreatedBy;

                _context.Entry(existingPlan).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                return Ok(existingPlan);
            }
            else
            {
                // Crear un nuevo plan
                request.CreatedAt = DateTime.Now;
                _context.ProductionPlans.Add(request);
                await _context.SaveChangesAsync();

                return Ok(request);
            }
        }
    }
}
