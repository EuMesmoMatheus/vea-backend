// Testes/Clients/ClientsControllerTests.cs
using System.Net.Http;
using Xunit;

namespace VEA.API.Testes.Clients;

/// <summary>
/// Classe base para testes do ClientsController.
/// </summary>
public class ClientsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    protected readonly CustomWebApplicationFactory _factory;

    public ClientsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    protected HttpClient CreateClientWithClaims(string userId = "999", string role = "Client", string companyId = "1")
        => _factory.CreateAuthenticatedClient(int.Parse(userId), role, int.Parse(companyId));

    protected HttpClient CreateAdminClient(int adminId = 1, int companyId = 1)
        => _factory.CreateAdminClient(adminId, companyId);
}
