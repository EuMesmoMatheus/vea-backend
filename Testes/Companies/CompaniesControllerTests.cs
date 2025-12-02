// Testes/Companies/CompaniesControllerTests.cs
using System.Net.Http;
using Xunit;

namespace VEA.API.Testes.Companies
{
    /// <summary>
    /// Classe base para testes do CompaniesController.
    /// </summary>
    public class CompaniesControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        protected readonly CustomWebApplicationFactory _factory;

        public CompaniesControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        protected HttpClient CreateClientWithClaims(int userId = 1, string role = "Client", int companyId = 1)
            => _factory.CreateAuthenticatedClient(userId, role, companyId);

        protected HttpClient CreateAdminClient(int adminId = 1, int companyId = 1)
            => _factory.CreateAdminClient(adminId, companyId);
    }
}
