using FluentAssertions;
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
    public UpdateClientTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact(DisplayName = "Cliente não Admin não deve atualizar dados")]
    public async Task Cliente_Nao_Admin_Nao_Deve_Atualizar()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var client = TestData.CreateClient(806, "Nome Antigo", 806);
        client.Phone = "(11) 11111-1111";
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        // Cliente comum não Admin não pode acessar o endpoint de update
        // (a classe tem [Authorize(Roles = "Admin")] que restringe acesso)
        var httpClient = CreateClientWithClaims("806", "Client", "806");
        
        var updated = new Client 
        { 
            Id = 806, 
            Name = "Nome Novo", 
            Email = "cliente806@teste.com",
            Phone = "(99) 99999-9999",
            PasswordHash = "hashedpassword123",
            IsActive = true,
            CompanyId = 806
        };

        var response = await httpClient.PutAsJsonAsync("/api/clients/806", updated);

        // Como a classe usa [Authorize(Roles = "Admin")], deve retornar Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
