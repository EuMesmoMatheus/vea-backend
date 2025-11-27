using System;
using System.ComponentModel.DataAnnotations;

namespace VEA.API.Models.Dtos
{
    public class CreateAppointmentDto
    {
        [Required]
        public int CompanyId { get; set; }

        // ============ NOVO: aceita os dois jeitos ao mesmo tempo ============
        // Se vier ServiceId preenchido → usa só 1 serviço (compatibilidade total com código antigo)
        // Se vier ServiceIds preenchido → usa múltiplos serviços (novo recurso)
        // Os dois podem vir juntos, mas o código dá prioridade pro ServiceIds
        public int? ServiceId { get; set; }

        public int[]? ServiceIds { get; set; }
        // =====================================================================

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        // Continua nullable pra permitir agendamento sem login (guest)
        public int? ClientId { get; set; }

        // Opcional no futuro
        // public string? Notes { get; set; }
    }
}