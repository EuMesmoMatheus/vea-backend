using System.ComponentModel.DataAnnotations;  // Pra [Required]
using System.Collections.Generic;  // <-- FIX: Esse era o missing pra List<int>

namespace VEA.API.Models
{
    public class ServiceDto  // DTO leve pra responses (sem navigation properties pesadas)
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public int Duration { get; set; }  // minutes

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int CompanyId { get; set; }  // Sempre setado (validação no controller)

        public bool Active { get; set; } = true;

        // Opcional: Array de IDs de employees atribuídos (leve, sem full objects)
        public List<int> EmployeeIds { get; set; } = new List<int>();
    }
}