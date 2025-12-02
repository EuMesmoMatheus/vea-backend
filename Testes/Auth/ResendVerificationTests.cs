using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Services;
using Xunit;

namespace VEA.API.Testes.Auth;

public class ResendVerificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ResendVerificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Reenviar verificação deve chamar serviço de e-mail")]
    public async Task Deve_Reenviar_Email_De_Verificacao()
    {
        // Arrange - Cadastra empresa inativa direto no banco
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var empresa = TestData.CreateCompany(502, "Resend Teste", false);
        empresa.Email = "resend502@teste.com";
        db.Companies.Add(empresa);
        await db.SaveChangesAsync();

        // Reset mock antes do teste
        _factory.MockEmailService.Reset();
        _factory.MockEmailService.Setup(x => x.SendConfirmationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                .Returns(Task.CompletedTask);

        // Act
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/resend-verification", "resend502@teste.com");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.MockEmailService.Verify(m => m.SendConfirmationEmail(
            "resend502@teste.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }
}
