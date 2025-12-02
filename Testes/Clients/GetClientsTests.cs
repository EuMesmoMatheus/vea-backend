using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Clients;

public class GetClientsTests : ClientsControllerTests
{
    public GetClientsTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact(DisplayName = "Admin deve listar clientes da própria empresa")]
    public async Task Admin_Deve_Listar_Clientes_Da_Empresa()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        db.Clients.AddRange(
            TestData.CreateClient(801, "João Cliente", 801),
            TestData.CreateClient(802, "Maria Cliente", 801),
            TestData.CreateClient(803, "Pedro Cliente", 802) // Outra empresa
        );
        await db.SaveChangesAsync();

        var httpClient = CreateAdminClient(adminId: 10, companyId: 801);
        var response = await httpClient.GetAsync("/api/clients?companyId=801");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<Client>>>();
        
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        
        // Filtra apenas os clientes da empresa 801
        var clientsDaEmpresa = result.Data!.Where(c => c.CompanyId == 801).ToList();
        clientsDaEmpresa.Should().HaveCount(2);
        clientsDaEmpresa.Should().Contain(c => c.Name == "João Cliente");
        clientsDaEmpresa.Should().Contain(c => c.Name == "Maria Cliente");
    }

    [Fact(DisplayName = "Não Admin não deve acessar lista de clientes")]
    public async Task Nao_Admin_Nao_Deve_Acessar_Lista()
    {
        var httpClient = CreateClientWithClaims("999", "Client", "1");
        var response = await httpClient.GetAsync("/api/clients?companyId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
