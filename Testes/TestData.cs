// Testes/TestData.cs
using VEA.API.Models;

namespace VEA.API.Testes
{
    /// <summary>
    /// Classe helper para criar entidades válidas para testes.
    /// Preenche automaticamente os campos obrigatórios.
    /// NOTA: Os valores aqui são APENAS para testes automatizados e não representam dados reais.
    /// </summary>
    public static class TestData
    {
        // Hash BCrypt de exemplo para testes (não é uma senha real)
        // Equivale a "TestPassword123!" hasheado com BCrypt
        private const string TestPasswordHash = "$2a$11$K3g6XoTau.tTHYzGlBpkkuEMk8jGvoxPvqCqch0wvH3H7HRXm5xFO";

        public static Company CreateCompany(int id = 1, string name = "Empresa Teste", bool isActive = true)
        {
            return new Company
            {
                Id = id,
                Name = name,
                Email = $"empresa{id}@teste.com",
                Phone = "(11) 99999-9999",
                PasswordHash = TestPasswordHash,
                BusinessType = "Barbearia",
                OperatingHours = "08:00-18:00",
                IsActive = isActive,
                Logo = "/uploads/logos/default.png"
            };
        }

        public static Client CreateClient(int id = 1, string name = "Cliente Teste", int? companyId = null)
        {
            return new Client
            {
                Id = id,
                Name = name,
                Email = $"cliente{id}@teste.com",
                Phone = "(11) 88888-8888",
                PasswordHash = TestPasswordHash,
                IsActive = true,
                CompanyId = companyId
            };
        }

        public static Employee CreateEmployee(int id = 1, int companyId = 1, string name = "Funcionário Teste")
        {
            return new Employee
            {
                Id = id,
                CompanyId = companyId,
                Name = name,
                Email = $"funcionario{id}@teste.com",
                Phone = "(11) 77777-7777",
                PasswordHash = TestPasswordHash,
                IsActive = true,
                EmailVerified = true
            };
        }

        public static Service CreateService(int id = 1, int companyId = 1, string name = "Serviço Teste")
        {
            return new Service
            {
                Id = id,
                CompanyId = companyId,
                Name = name,
                Duration = 60,
                Price = 50m,
                Active = true
            };
        }

        public static Address CreateAddress(int id = 1, int companyId = 1, string cidade = "São Paulo", string bairro = "Centro")
        {
            return new Address
            {
                Id = id,
                CompanyId = companyId,
                Cep = "01310-100",
                Logradouro = "Avenida Paulista",
                Numero = "1000",
                Bairro = bairro,
                Cidade = cidade,
                Uf = "SP"
            };
        }
    }
}
