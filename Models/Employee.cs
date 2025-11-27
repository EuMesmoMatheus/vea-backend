using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace VEA.API.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string? Name { get; set; }
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
        [Required]
        public string? Phone { get; set; }
        public int? RoleId { get; set; }  // NOVO: FK pra Role (opcional com ?)
        [ForeignKey("RoleId")]
        public Role? Role { get; set; }  // NOVO: Navigation
        [Required]
        public int CompanyId { get; set; }
        [ForeignKey("CompanyId")]
        public Company? Company { get; set; }
        [Required]
        public string? PasswordHash { get; set; }
        public bool IsActive { get; set; } = false;
        public bool EmailVerified { get; set; } = false;  // NOVO: Pra verificação
        public string? PhotoUrl { get; set; }  // NOVO: Pra foto
        public List<Service> Services { get; set; } = new List<Service>();

        public List<EmployeeBlock> Blocks { get; set; } = new List<EmployeeBlock>(); // Req 3.10: folgas/pausas
    }


}