using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Clients;

public class GetClientTests : ClientsControllerTests
{
    public GetClientTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact(DisplayName = "Admin deve ver dados de cliente")]
    public async Task Admin_Deve_Ver_Dados_Cliente()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var client = TestData.CreateClient(804, "Ana Teste", 804);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        // Admin pode ver qualquer cliente (o controller tem [Authorize(Roles = "Admin")] na classe)
        var httpClient = CreateAdminClient(adminId: 1, companyId: 804);
        var response = await httpClient.GetAsync("/api/clients/804");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Client>>();
        result!.Data!.Name.Should().Be("Ana Teste");
    }

    [Fact(DisplayName = "Cliente não autenticado como Admin não deve acessar endpoint")]
    public async Task Cliente_Nao_Admin_Nao_Deve_Acessar()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var client = TestData.CreateClient(805, "Outro Cliente", 805);
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        // Cliente comum (não Admin) não deve conseguir acessar 
        // (mesmo com [Authorize(Roles = "Admin,Client")] no método, 
        // a classe tem [Authorize(Roles = "Admin")] que prevalece)
        var httpClient = CreateClientWithClaims("805", "Client", "805");
        var response = await httpClient.GetAsync("/api/clients/805");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
