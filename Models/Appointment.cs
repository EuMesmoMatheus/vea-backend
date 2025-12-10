// VEA.API/Models/Appointment.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VEA.API.Models
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }

        [Required] public int ClientId { get; set; }
        public Client? Client { get; set; }

        [Required] public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        [Required] public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required] public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

        [Required] public string Status { get; set; } = "Scheduled";

        public string ServicesJson { get; set; } = string.Empty;
        [Required] public int TotalDurationMinutes { get; set; }

        /// <summary>
        /// Preço total do agendamento (soma dos serviços)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalPrice { get; set; } = 0;
    }
}