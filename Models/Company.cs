using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace VEA.API.Models
{
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string? Name { get; set; }

        // NOVO: Campo pra tipo de serviço/negócio (req. custom: foco da empresa)
        [Required(ErrorMessage = "Tipo de negócio é obrigatório")]
        [StringLength(50, ErrorMessage = "Tipo de negócio deve ter no máximo 50 caracteres.")]
        public string? BusinessType { get; set; } // Ex: "Barbearia", "Estética", etc. (string livre pra flexibilidade)

        [StringLength(500, ErrorMessage = "Path do logo deve ter no máximo 500 caracteres.")]
        public string? Logo { get; set; } // Ex: "/uploads/logos/abc123.png"

        [StringLength(500, ErrorMessage = "Path da capa deve ter no máximo 500 caracteres.")]
        public string? CoverImage { get; set; } // Ex: "/uploads/covers/xyz789.png"

        // ATUALIZADO: Tornei nullable pra endereço opcional (se não tiver, fica null)
        public int? AddressId { get; set; } // Foreign key (nullable)
        [ForeignKey("AddressId")]
        public Address? Address { get; set; } // Navigation prop

        [StringLength(50)]
        public string? OperatingHours { get; set; } // Ex: "10:00-18:00"

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [Phone(ErrorMessage = "Telefone inválido. Use formato (11) 99999-9999.")]
        [StringLength(20)]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        [StringLength(255)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória.")]
        public string? PasswordHash { get; set; }

        public bool IsActive { get; set; } = true; // Mudei default pra true (ativa por default; ajuste se quiser false)

        // Coleções (EF navigation properties)
        public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public virtual ICollection<Service> Services { get; set; } = new List<Service>();
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>(); // ADICIONADO: Pra Include no Delete

        // Pra uploads em FromForm (não persiste no DB, só pra binding)
        [NotMapped] // Não mapeia pro DB
        public IFormFile? LogoFile { get; set; }

        [NotMapped]
        public IFormFile? CoverImageFile { get; set; }
    }
}