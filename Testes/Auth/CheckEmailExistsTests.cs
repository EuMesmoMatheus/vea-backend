using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Auth;

public class CheckEmailExistsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public CheckEmailExistsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "CheckEmail deve retornar false para e-mail inexistente")]
    public async Task Deve_Retornar_False_Quando_Email_Nao_Existe()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/check-email/emailinexistente123@teste.com");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        result!.Data.Should().BeFalse();
    }

    [Fact(DisplayName = "CheckEmail deve retornar true quando e-mail já está cadastrado")]
    public async Task Deve_Retornar_True_Quando_Email_Existe()
    {
        // Arrange - Cadastra um cliente direto no banco
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var existingClient = TestData.CreateClient(500, "Cliente Existe");
        existingClient.Email = "clienteexiste500@teste.com";
        db.Clients.Add(existingClient);
        await db.SaveChangesAsync();

        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/auth/check-email/clienteexiste500@teste.com");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        result!.Data.Should().BeTrue();
    }
}
