// Testes/Appointments/AppointmentsControllerTests.cs
using System.Net.Http;
using Xunit;

namespace VEA.API.Testes.Appointments
{
    /// <summary>
    /// Classe base para testes do AppointmentsController.
    /// Fornece acesso à factory e métodos helper para criar clientes autenticados.
    /// </summary>
    public class AppointmentsControllerTests : IClassFixture<CustomWebApplicationFactory>
    {
        protected readonly CustomWebApplicationFactory _factory;

        public AppointmentsControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        protected HttpClient CreateClientAs(int userId = 999, string role = "Client", int companyId = 1)
            => _factory.CreateAuthenticatedClient(userId, role, companyId);

        protected HttpClient CreateAdminClient(int adminId = 1, int companyId = 1)
            => _factory.CreateAdminClient(adminId, companyId);
    }
}
