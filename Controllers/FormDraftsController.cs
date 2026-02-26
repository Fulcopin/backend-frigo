using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;

namespace FormBuilder.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FormDraftsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FormDraftsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/FormDrafts — Todos los borradores activos (para admin)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllDrafts()
        {
            var drafts = await _context.FormDrafts
                .Where(d => d.IsActive && d.ExpiresAt > DateTime.Now)
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new
                {
                    d.DraftID,
                    d.TemplateID,
                    d.TemplateName,
                    d.TemplateCodigo,
                    d.UserName,
                    d.UserEmail,
                    d.UserRole,
                    d.Progress,
                    d.Nota,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.ExpiresAt
                })
                .ToListAsync();

            return Ok(drafts);
        }

        // GET: api/FormDrafts/my/{userName} — Borradores del usuario actual
        [HttpGet("my/{userName}")]
        public async Task<ActionResult<IEnumerable<object>>> GetMyDrafts(string userName)
        {
            var drafts = await _context.FormDrafts
                .Where(d => d.IsActive && d.ExpiresAt > DateTime.Now &&
                       d.UserName != null && d.UserName.ToLower() == userName.ToLower())
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new
                {
                    d.DraftID,
                    d.TemplateID,
                    d.TemplateName,
                    d.TemplateCodigo,
                    d.UserName,
                    d.Progress,
                    d.Nota,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.ExpiresAt,
                    DaysLeft = (d.ExpiresAt - DateTime.Now).Days
                })
                .ToListAsync();

            return Ok(drafts);
        }

        // GET: api/FormDrafts/{id} — Obtener borrador completo (con datos)
        [HttpGet("{id}")]
        public async Task<ActionResult<FormDraft>> GetDraft(int id)
        {
            var draft = await _context.FormDrafts.FindAsync(id);

            if (draft == null || !draft.IsActive)
            {
                return NotFound(new { message = "Borrador no encontrado" });
            }

            if (draft.ExpiresAt <= DateTime.Now)
            {
                draft.IsActive = false;
                await _context.SaveChangesAsync();
                return NotFound(new { message = "Este borrador ha expirado" });
            }

            return Ok(draft);
        }

        // POST: api/FormDrafts — Crear nuevo borrador
        [HttpPost]
        public async Task<ActionResult<FormDraft>> CreateDraft([FromBody] FormDraft draft)
        {
            draft.CreatedAt = DateTime.Now;
            draft.UpdatedAt = DateTime.Now;
            draft.ExpiresAt = DateTime.Now.AddDays(7);
            draft.IsActive = true;

            _context.FormDrafts.Add(draft);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDraft), new { id = draft.DraftID }, draft);
        }

        // PUT: api/FormDrafts/{id} — Actualizar borrador existente
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDraft(int id, [FromBody] FormDraft updatedDraft)
        {
            var draft = await _context.FormDrafts.FindAsync(id);
            if (draft == null || !draft.IsActive)
            {
                return NotFound(new { message = "Borrador no encontrado" });
            }

            // Actualizar campos
            draft.HeaderData = updatedDraft.HeaderData;
            draft.BodyData = updatedDraft.BodyData;
            draft.FirmasData = updatedDraft.FirmasData;
            draft.Progress = updatedDraft.Progress;
            draft.Nota = updatedDraft.Nota;
            draft.UpdatedAt = DateTime.Now;
            // Renovar expiración a 7 días desde ahora
            draft.ExpiresAt = DateTime.Now.AddDays(7);

            await _context.SaveChangesAsync();

            return Ok(draft);
        }

        // DELETE: api/FormDrafts/{id} — Eliminar borrador
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(int id)
        {
            var draft = await _context.FormDrafts.FindAsync(id);
            if (draft == null)
            {
                return NotFound();
            }

            // Soft delete
            draft.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Borrador eliminado" });
        }

        // POST: api/FormDrafts/cleanup — Limpiar borradores expirados (mantenimiento)
        [HttpPost("cleanup")]
        public async Task<IActionResult> CleanupExpired()
        {
            var expiredDrafts = await _context.FormDrafts
                .Where(d => d.ExpiresAt <= DateTime.Now && d.IsActive)
                .ToListAsync();

            foreach (var draft in expiredDrafts)
            {
                draft.IsActive = false;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Se desactivaron {expiredDrafts.Count} borradores expirados" });
        }

        // GET: api/FormDrafts/check/{templateId}/{userName} — Verificar si hay borrador existente para un template+usuario
        [HttpGet("check/{templateId}/{userName}")]
        public async Task<ActionResult> CheckExistingDraft(int templateId, string userName)
        {
            var draft = await _context.FormDrafts
                .Where(d => d.IsActive && d.ExpiresAt > DateTime.Now &&
                       d.TemplateID == templateId &&
                       d.UserName != null && d.UserName.ToLower() == userName.ToLower())
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new
                {
                    d.DraftID,
                    d.TemplateName,
                    d.UpdatedAt,
                    d.Progress,
                    DaysLeft = (d.ExpiresAt - DateTime.Now).Days
                })
                .FirstOrDefaultAsync();

            if (draft == null)
            {
                return Ok(new { exists = false });
            }

            return Ok(new { exists = true, draft });
        }
    }
}
