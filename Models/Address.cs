using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VEA.API.Models
{
    public class Address
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "CEP é obrigatório")]
        [RegularExpression(@"^\d{5}-\d{3}$", ErrorMessage = "Formato de CEP inválido: 00000-000")]
        public string? Cep { get; set; }

        [Required(ErrorMessage = "Logradouro é obrigatório")]
        public string? Logradouro { get; set; }

        [Required(ErrorMessage = "Número é obrigatório")]
        public string? Numero { get; set; }

        public string? Complemento { get; set; } // Opcional

        [Required(ErrorMessage = "Bairro é obrigatório")]
        public string? Bairro { get; set; }

        [Required(ErrorMessage = "Cidade é obrigatória")]
        public string? Cidade { get; set; }

        [Required(ErrorMessage = "UF é obrigatória")]
        [RegularExpression("^[A-Z]{2}$", ErrorMessage = "UF deve ser 2 letras maiúsculas (ex: SP)")]
        public string? Uf { get; set; }

        // Foreign key pra Company
        public int CompanyId { get; set; }  // FK obrigatório (não nullable)

        [ForeignKey("CompanyId")]  // <<< FIX: Aponta pro FK correto (não pro navigation "Company")
        public Company? Company { get; set; } // Navigation prop
    }
}