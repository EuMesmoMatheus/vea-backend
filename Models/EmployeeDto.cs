using System;

namespace VEA.API.Models.Dtos  // Ajuste o namespace pro seu projeto se precisar
{
    public class EmployeeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public int RoleId { get; set; }  // Non-nullable, mas mapeamos com fallback
        public bool EmailVerified { get; set; }
        public string? RoleName { get; set; }  // Nome do Role, pra não precisar de Include extra no front
        public string? FullPhotoUrl { get; set; }  // URL completa da foto (resolve o 404!)
    }
}