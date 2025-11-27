using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
namespace VEA.API.Models
{
    public class Service
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        public string? Description { get; set; }
        [Required]
        public int Duration { get; set; } // in minutes
        [Required]
        public decimal Price { get; set; }
        [Required]
        public int CompanyId { get; set; }
        public Company? Company { get; set; }
        public List<Employee> Employees { get; set; } = new List<Employee>();
        public bool Active { get; set; } = true;
        public List<Appointment> Appointments { get; set; } = new List<Appointment>(); // Novo: req 4.4
    }
}