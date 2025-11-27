using System.ComponentModel.DataAnnotations;

namespace VEA.API.Models
{
    public class Client
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

        [Required]
        public string? PasswordHash { get; set; }

        public bool IsActive { get; set; } = false;

        // <<< FIX: Nullable pra clients (null = não vinculado a empresa)
        public int? CompanyId { get; set; } = null;

        public Company? Company { get; set; } // Navigation property (opcional)
    }
}