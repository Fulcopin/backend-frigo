using FormBuilder.API.Data;
using FormBuilder.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// ⚙️ Horarios de turno (DÍA / NOCHE) compartidos por toda la planta.
    ///
    /// Lo usan los bloqueos del formulario. Es una sola fila para todo el
    /// sistema; GET la crea con los valores por defecto si todavía no existe y
    /// PUT la actualiza. Así una tablet y otra ven el mismo horario.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ConfiguracionHorariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ConfiguracionHorariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ConfiguracionHorarios
        [HttpGet]
        public async Task<ActionResult<ConfiguracionHorario>> Get()
        {
            var cfg = await _context.ConfiguracionHorarios.FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new ConfiguracionHorario();
                _context.ConfiguracionHorarios.Add(cfg);
                await _context.SaveChangesAsync();
            }
            return Ok(cfg);
        }

        // PUT: api/ConfiguracionHorarios
        [HttpPut]
        public async Task<ActionResult<ConfiguracionHorario>> Put([FromBody] ConfiguracionHorario dto)
        {
            var cfg = await _context.ConfiguracionHorarios.FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new ConfiguracionHorario();
                _context.ConfiguracionHorarios.Add(cfg);
            }

            // Solo se pisan los valores que vienen con hora válida (HH:MM).
            if (EsHora(dto.TurnoDiaInicio)) cfg.TurnoDiaInicio = dto.TurnoDiaInicio;
            if (EsHora(dto.TurnoDiaFin)) cfg.TurnoDiaFin = dto.TurnoDiaFin;
            if (EsHora(dto.TurnoNocheInicio)) cfg.TurnoNocheInicio = dto.TurnoNocheInicio;
            if (EsHora(dto.TurnoNocheFin)) cfg.TurnoNocheFin = dto.TurnoNocheFin;

            cfg.ActualizadoPor = dto.ActualizadoPor;
            cfg.ActualizadoEn = DateTime.Now;

            await _context.SaveChangesAsync();
            return Ok(cfg);
        }

        private static bool EsHora(string? valor) =>
            !string.IsNullOrWhiteSpace(valor)
            && System.Text.RegularExpressions.Regex.IsMatch(valor.Trim(), @"^\d{1,2}:\d{2}$");
    }
}
