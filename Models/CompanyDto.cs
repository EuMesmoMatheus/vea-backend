using Microsoft.AspNetCore.Http; // ← ADICIONE esta using para IFormFile

namespace VEA.API.Models
{
    public class CompanyDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? OperatingHours { get; set; }
        public string? BusinessType { get; set; }
        public bool IsActive { get; set; }
        public string? Logo { get; set; }
        public string? CoverImage { get; set; }
        // Campos de Address flatten
        public string? Cep { get; set; }
        public string? Logradouro { get; set; }
        public string? Numero { get; set; }
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Uf { get; set; }

        // NOVO: Para suportar upload de files no update/register
        public IFormFile? LogoFile { get; set; }
        public IFormFile? CoverImageFile { get; set; }

        // NOVO: Para senha no register/update (opcional no update)
        public string? Password { get; set; }
    }
}