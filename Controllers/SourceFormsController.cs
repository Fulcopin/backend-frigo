using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FormBuilder.API.Data;
using FormBuilder.API.Models;
using System.Text.Json;

namespace FormBuilder.API.Controllers
{
    /// <summary>
    /// Controlador para Formularios Fuente/Maestros
    /// Permite crear formularios básicos cuyos datos pueden ser reutilizados en otros formularios
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SourceFormsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SourceFormsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════════════
        // CRUD BÁSICO
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// GET: api/SourceForms
        /// Obtiene todos los formularios fuente activos
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SourceForm>>> GetSourceForms()
        {
            return await _context.SourceForms
                .Where(sf => sf.IsActive)
                .OrderByDescending(sf => sf.RecordDate)
                .ToListAsync();
        }

        /// <summary>
        /// GET: api/SourceForms/5
        /// Obtiene un formulario fuente por ID con datos parseados
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<SourceFormQueryDto>> GetSourceForm(int id)
        {
            var sourceForm = await _context.SourceForms.FindAsync(id);

            if (sourceForm == null)
            {
                return NotFound(new { message = $"Formulario fuente con ID {id} no encontrado" });
            }

            // Parsear JSON a objeto
            object? parsedData = null;
            try
            {
                parsedData = JsonSerializer.Deserialize<object>(sourceForm.DataJson);
            }
            catch (JsonException)
            {
                parsedData = new { error = "Error al parsear datos" };
            }

            object? parsedMetadata = null;
            if (!string.IsNullOrEmpty(sourceForm.Metadata))
            {
                try
                {
                    parsedMetadata = JsonSerializer.Deserialize<object>(sourceForm.Metadata);
                }
                catch (JsonException)
                {
                    parsedMetadata = null;
                }
            }

            var dto = new SourceFormQueryDto
            {
                SourceFormID = sourceForm.SourceFormID,
                FormType = sourceForm.FormType,
                RecordCode = sourceForm.RecordCode,
                RecordDate = sourceForm.RecordDate,
                Data = parsedData ?? new { },
                Metadata = parsedMetadata,
                CreatedAt = sourceForm.CreatedAt,
                CreatedBy = sourceForm.CreatedBy
            };

            return Ok(dto);
        }

        /// <summary>
        /// POST: api/SourceForms
        /// Crea un nuevo formulario fuente
        /// </summary>
        /// <example>
        /// {
        ///   "formType": "Registro Producción Básico",
        ///   "recordCode": "PROD-2025-001",
        ///   "recordDate": "2025-01-15",
        ///   "data": {
        ///     "rows": [
        ///       { "hora": "08:00", "tina": "T1", "pesoNeto": 120.5 },
        ///       { "hora": "09:00", "tina": "T2", "pesoNeto": 150.3 },
        ///       { "hora": "10:00", "tina": "T3", "pesoNeto": 180.0 }
        ///     ]
        ///   },
        ///   "metadata": {
        ///     "turno": "Mañana",
        ///     "responsable": "Juan Pérez"
        ///   },
        ///   "createdBy": "juan.perez",
        ///   "notes": "Registro completo sin observaciones"
        /// }
        /// </example>
        [HttpPost]
        public async Task<ActionResult<SourceForm>> CreateSourceForm([FromBody] CreateSourceFormDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Serializar data a JSON
            string dataJson;
            try
            {
                dataJson = JsonSerializer.Serialize(dto.Data);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error al serializar datos", error = ex.Message });
            }

            // Serializar metadata a JSON si existe
            string? metadataJson = null;
            if (dto.Metadata != null)
            {
                try
                {
                    metadataJson = JsonSerializer.Serialize(dto.Metadata);
                }
                catch (JsonException ex)
                {
                    return BadRequest(new { message = "Error al serializar metadata", error = ex.Message });
                }
            }

            var sourceForm = new SourceForm
            {
                FormType = dto.FormType,
                RecordCode = dto.RecordCode,
                RecordDate = dto.RecordDate,
                DataJson = dataJson,
                Metadata = metadataJson,
                CreatedBy = dto.CreatedBy,
                Notes = dto.Notes,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.SourceForms.Add(sourceForm);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSourceForm), new { id = sourceForm.SourceFormID }, sourceForm);
        }

        /// <summary>
        /// PUT: api/SourceForms/5
        /// Actualiza un formulario fuente existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSourceForm(int id, [FromBody] CreateSourceFormDto dto)
        {
            var sourceForm = await _context.SourceForms.FindAsync(id);

            if (sourceForm == null)
            {
                return NotFound(new { message = $"Formulario fuente con ID {id} no encontrado" });
            }

            // Serializar data
            try
            {
                sourceForm.DataJson = JsonSerializer.Serialize(dto.Data);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error al serializar datos", error = ex.Message });
            }

            // Serializar metadata
            if (dto.Metadata != null)
            {
                try
                {
                    sourceForm.Metadata = JsonSerializer.Serialize(dto.Metadata);
                }
                catch (JsonException ex)
                {
                    return BadRequest(new { message = "Error al serializar metadata", error = ex.Message });
                }
            }

            sourceForm.FormType = dto.FormType;
            sourceForm.RecordCode = dto.RecordCode;
            sourceForm.RecordDate = dto.RecordDate;
            sourceForm.Notes = dto.Notes;
            sourceForm.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// DELETE: api/SourceForms/5
        /// Desactiva (soft delete) un formulario fuente
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSourceForm(int id)
        {
            var sourceForm = await _context.SourceForms.FindAsync(id);

            if (sourceForm == null)
            {
                return NotFound(new { message = $"Formulario fuente con ID {id} no encontrado" });
            }

            // Soft delete
            sourceForm.IsActive = false;
            sourceForm.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ═══════════════════════════════════════════════════════════════
        // ENDPOINTS DE CONSULTA Y REUTILIZACIÓN
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// POST: api/SourceForms/query
        /// Consulta formularios fuente con filtros
        /// </summary>
        /// <example>
        /// {
        ///   "formType": "Registro Producción Básico",
        ///   "startDate": "2025-01-01",
        ///   "endDate": "2025-01-31",
        ///   "isActive": true
        /// }
        /// </example>
        [HttpPost("query")]
        public async Task<ActionResult<IEnumerable<SourceFormQueryDto>>> QuerySourceForms([FromBody] SourceFormFilterDto filter)
        {
            var query = _context.SourceForms.AsQueryable();

            // Aplicar filtros
            if (!string.IsNullOrEmpty(filter.FormType))
            {
                query = query.Where(sf => sf.FormType == filter.FormType);
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(sf => sf.RecordDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(sf => sf.RecordDate <= filter.EndDate.Value);
            }

            if (!string.IsNullOrEmpty(filter.RecordCode))
            {
                query = query.Where(sf => sf.RecordCode == filter.RecordCode);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(sf => sf.IsActive == filter.IsActive.Value);
            }

            var results = await query.OrderByDescending(sf => sf.RecordDate).ToListAsync();

            // Mapear a DTOs con datos parseados
            var dtos = results.Select(sf =>
            {
                object? parsedData = null;
                try
                {
                    parsedData = JsonSerializer.Deserialize<object>(sf.DataJson);
                }
                catch { }

                object? parsedMetadata = null;
                if (!string.IsNullOrEmpty(sf.Metadata))
                {
                    try
                    {
                        parsedMetadata = JsonSerializer.Deserialize<object>(sf.Metadata);
                    }
                    catch { }
                }

                return new SourceFormQueryDto
                {
                    SourceFormID = sf.SourceFormID,
                    FormType = sf.FormType,
                    RecordCode = sf.RecordCode,
                    RecordDate = sf.RecordDate,
                    Data = parsedData ?? new { },
                    Metadata = parsedMetadata,
                    CreatedAt = sf.CreatedAt,
                    CreatedBy = sf.CreatedBy
                };
            }).ToList();

            return Ok(dtos);
        }

        /// <summary>
        /// GET: api/SourceForms/types
        /// Obtiene la lista de tipos de formularios fuente disponibles
        /// </summary>
        [HttpGet("types")]
        public async Task<ActionResult<IEnumerable<string>>> GetFormTypes()
        {
            var types = await _context.SourceForms
                .Where(sf => sf.IsActive)
                .Select(sf => sf.FormType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();

            return Ok(types);
        }

        /// <summary>
        /// GET: api/SourceForms/by-type/{formType}
        /// Obtiene todos los formularios de un tipo específico
        /// </summary>
        /// <example>
        /// GET /api/SourceForms/by-type/Registro%20Producción%20Básico
        /// </example>
        [HttpGet("by-type/{formType}")]
        public async Task<ActionResult<IEnumerable<SourceFormQueryDto>>> GetByFormType(string formType)
        {
            var sourceForms = await _context.SourceForms
                .Where(sf => sf.FormType == formType && sf.IsActive)
                .OrderByDescending(sf => sf.RecordDate)
                .ToListAsync();

            if (!sourceForms.Any())
            {
                return Ok(new List<SourceFormQueryDto>());
            }

            var dtos = sourceForms.Select(sf =>
            {
                object? parsedData = null;
                try
                {
                    parsedData = JsonSerializer.Deserialize<object>(sf.DataJson);
                }
                catch { }

                object? parsedMetadata = null;
                if (!string.IsNullOrEmpty(sf.Metadata))
                {
                    try
                    {
                        parsedMetadata = JsonSerializer.Deserialize<object>(sf.Metadata);
                    }
                    catch { }
                }

                return new SourceFormQueryDto
                {
                    SourceFormID = sf.SourceFormID,
                    FormType = sf.FormType,
                    RecordCode = sf.RecordCode,
                    RecordDate = sf.RecordDate,
                    Data = parsedData ?? new { },
                    Metadata = parsedMetadata,
                    CreatedAt = sf.CreatedAt,
                    CreatedBy = sf.CreatedBy
                };
            }).ToList();

            return Ok(dtos);
        }

        /// <summary>
        /// GET: api/SourceForms/by-date/{date}
        /// Obtiene formularios fuente de una fecha específica
        /// </summary>
        /// <example>
        /// GET /api/SourceForms/by-date/2025-01-15
        /// </example>
        [HttpGet("by-date/{date}")]
        public async Task<ActionResult<IEnumerable<SourceFormQueryDto>>> GetByDate(DateTime date)
        {
            var sourceForms = await _context.SourceForms
                .Where(sf => sf.RecordDate.Date == date.Date && sf.IsActive)
                .OrderBy(sf => sf.FormType)
                .ToListAsync();

            var dtos = sourceForms.Select(sf =>
            {
                object? parsedData = null;
                try
                {
                    parsedData = JsonSerializer.Deserialize<object>(sf.DataJson);
                }
                catch { }

                return new SourceFormQueryDto
                {
                    SourceFormID = sf.SourceFormID,
                    FormType = sf.FormType,
                    RecordCode = sf.RecordCode,
                    RecordDate = sf.RecordDate,
                    Data = parsedData ?? new { },
                    CreatedAt = sf.CreatedAt,
                    CreatedBy = sf.CreatedBy
                };
            }).ToList();

            return Ok(dtos);
        }

        /// <summary>
        /// POST: api/SourceForms/{id}/select-rows
        /// Selecciona filas específicas de un formulario fuente para reutilizar
        /// Útil para importar solo ciertas filas a otro formulario
        /// </summary>
        /// <example>
        /// POST /api/SourceForms/5/select-rows
        /// {
        ///   "sourceFormID": 5,
        ///   "rowIndices": [0, 2, 4]  // Selecciona filas 1, 3 y 5
        /// }
        /// </example>
        [HttpPost("{id}/select-rows")]
        public async Task<ActionResult<object>> SelectRows(int id, [FromBody] SourceFormRowSelectionDto selection)
        {
            if (id != selection.SourceFormID)
            {
                return BadRequest(new { message = "El ID del formulario no coincide" });
            }

            var sourceForm = await _context.SourceForms.FindAsync(id);

            if (sourceForm == null)
            {
                return NotFound(new { message = $"Formulario fuente con ID {id} no encontrado" });
            }

            // Parsear datos
            object? parsedData;
            try
            {
                parsedData = JsonSerializer.Deserialize<JsonElement>(sourceForm.DataJson);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { message = "Error al parsear datos", error = ex.Message });
            }

            // Extraer filas seleccionadas
            var jsonElement = (JsonElement)parsedData;
            if (!jsonElement.TryGetProperty("rows", out JsonElement rowsElement))
            {
                return BadRequest(new { message = "El formulario no tiene estructura de 'rows'" });
            }

            var allRows = rowsElement.EnumerateArray().ToList();
            var selectedRows = new List<JsonElement>();

            foreach (var index in selection.RowIndices)
            {
                if (index >= 0 && index < allRows.Count)
                {
                    selectedRows.Add(allRows[index]);
                }
            }

            var result = new
            {
                sourceFormID = sourceForm.SourceFormID,
                formType = sourceForm.FormType,
                recordDate = sourceForm.RecordDate,
                selectedRows = selectedRows,
                totalSelected = selectedRows.Count
            };

            return Ok(result);
        }

        /// <summary>
        /// GET: api/SourceForms/latest/{formType}
        /// Obtiene el formulario fuente más reciente de un tipo específico
        /// </summary>
        [HttpGet("latest/{formType}")]
        public async Task<ActionResult<SourceFormQueryDto>> GetLatestByType(string formType)
        {
            var sourceForm = await _context.SourceForms
                .Where(sf => sf.FormType == formType && sf.IsActive)
                .OrderByDescending(sf => sf.RecordDate)
                .ThenByDescending(sf => sf.CreatedAt)
                .FirstOrDefaultAsync();

            if (sourceForm == null)
            {
                return NotFound(new { message = $"No se encontró formulario del tipo '{formType}'" });
            }

            object? parsedData = null;
            try
            {
                parsedData = JsonSerializer.Deserialize<object>(sourceForm.DataJson);
            }
            catch { }

            object? parsedMetadata = null;
            if (!string.IsNullOrEmpty(sourceForm.Metadata))
            {
                try
                {
                    parsedMetadata = JsonSerializer.Deserialize<object>(sourceForm.Metadata);
                }
                catch { }
            }

            var dto = new SourceFormQueryDto
            {
                SourceFormID = sourceForm.SourceFormID,
                FormType = sourceForm.FormType,
                RecordCode = sourceForm.RecordCode,
                RecordDate = sourceForm.RecordDate,
                Data = parsedData ?? new { },
                Metadata = parsedMetadata,
                CreatedAt = sourceForm.CreatedAt,
                CreatedBy = sourceForm.CreatedBy
            };

            return Ok(dto);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        private bool SourceFormExists(int id)
        {
            return _context.SourceForms.Any(e => e.SourceFormID == id);
        }
    }
}
