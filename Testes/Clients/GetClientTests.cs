using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Clients;

public class GetClientTests : ClientsControllerTests
{
    public GetClientTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "Cliente deve ver seus próprios dados")]
    public async Task Cliente_Deve_Ver_Proprios_Dados()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Clients.Add(new Client { Id = 999, Name = "Ana", Email = "ana@teste.com", CompanyId = 1 });
        await db.SaveChangesAsync();

        var client = CreateClientWithClaims("999", "Client", "1");
        var response = await client.GetAsync("/api/clients/999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Client>>();
        result!.Data!.Name.Should().Be("Ana");
    }

    [Fact(DisplayName = "Cliente não deve ver dados de outro cliente")]
    public async Task Cliente_Nao_Deve_Ver_Outro_Cliente()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Clients.Add(new Client { Id = 888, Name = "Outro", Email = "outro@teste.com", CompanyId = 1 });
        await db.SaveChangesAsync();

        var client = CreateClientWithClaims("999", "Client", "1");
        var response = await client.GetAsync("/api/clients/888");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}