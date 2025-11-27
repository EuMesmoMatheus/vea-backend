using System.ComponentModel.DataAnnotations;

namespace VEA.API.Models
{
    public class RegisterClientDto
    {
        [Required(ErrorMessage = "Nome é obrigatório")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "E-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "Formato de e-mail inválido")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Telefone é obrigatório")]
        [Phone(ErrorMessage = "Formato de telefone inválido")]  // Opcional: Valida formato BR (ex: (47) 98465-862)
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Senha é obrigatória")]
        [MinLength(8, ErrorMessage = "Senha deve ter pelo menos 8 caracteres")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*])[A-Za-z\d!@#$%^&*]{8,}$",
                           ErrorMessage = "Senha fraca: precisa de maiúscula, minúscula, número e especial (ex: Senha@123)")]  // Opcional: Regex pra critérios do frontend
        public string? Password { get; set; } // Plain text do JSON
    }
}