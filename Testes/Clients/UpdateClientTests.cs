using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Clients;

public class UpdateClientTests : ClientsControllerTests
{
    public UpdateClientTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "Cliente deve atualizar seus próprios dados")]
    public async Task Cliente_Deve_Atualizar_Proprios_Dados()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Clients.Add(new Client { Id = 999, Name = "Antigo", Phone = "111", CompanyId = 1 });
        await db.SaveChangesAsync();

        var client = CreateClientWithClaims("999", "Client", "1");
        var updated = new Client { Id = 999, Name = "Novo Nome", Phone = "999999999" };

        var response = await client.PutAsJsonAsync("/api/clients/999", updated);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dbClient = await db.Clients.FindAsync(999);
        dbClient!.Name.Should().Be("Novo Nome");
        dbClient.Phone.Should().Be("999999999");
    }
}