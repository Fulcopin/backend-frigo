using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FormBuilder.API.Models
{
    public class ProductionPlan
    {
        [Key]
        public int Id { get; set; }

        public DateTime FechaOperacion { get; set; }

        [StringLength(50)]
        public string? Turno { get; set; }

        // Definición de las columnas dinámicas creadas por el usuario en formato JSON
        [Column(TypeName = "nvarchar(max)")]
        public string? CustomFieldsDefinition { get; set; }

        // Datos de la planificación (categorías, procesos, plan, prod, res) en formato JSON
        [Column(TypeName = "nvarchar(max)")]
        public string? PlanData { get; set; }

        // Información de auditoría
        [StringLength(200)]
        public string? CreatedBy { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
