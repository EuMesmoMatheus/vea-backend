using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VEA.API.Data;
using Xunit;

namespace VEA.API.Testes.Auth;

public class ConfirmAccountTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConfirmAccountTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Confirmar conta deve ativar empresa")]
    public async Task Deve_Ativar_Empresa_Com_Token_Valido()
    {
        // Arrange - Cadastra empresa diretamente no banco
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var empresa = TestData.CreateCompany(501, "Empresa Confirm Test", false);
        empresa.Email = "confirmtest501@teste.com";
        db.Companies.Add(empresa);
        await db.SaveChangesAsync();

        // Gera token válido (mesmo formato que o controller usa)
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"confirmtest501@teste.com {DateTime.Now.Ticks}"));
        var encodedToken = Uri.EscapeDataString(token);

        // Act - Confirma a empresa
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/auth/confirm/company/{empresa.Id}?token={encodedToken}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verifica se empresa foi ativada
        db.ChangeTracker.Clear();
        var empresaAtivada = await db.Companies.FirstAsync(c => c.Id == empresa.Id);
        empresaAtivada.IsActive.Should().BeTrue();
    }
}
