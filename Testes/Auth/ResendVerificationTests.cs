using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API;
using VEA.API.Data;
using VEA.API.Services;
using Xunit;

namespace VEA.API.Testes.Auth;

public class ResendVerificationTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IEmailService> _mockEmail;

    public ResendVerificationTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        _mockEmail = new Mock<IEmailService>();

        factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Resend_" + Guid.NewGuid()));

                services.RemoveAll<IEmailService>();
                services.AddScoped(_ => _mockEmail.Object);
            });
        });

        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "Reenviar verificação deve chamar serviço de e-mail")]
    public async Task Deve_Reenviar_Email_De_Verificacao()
    {
        // Cadastra empresa inativa
        var form = new MultipartFormDataContent
        {
            { new StringContent("Reenviar Teste"), "name" },
            { new StringContent("resend@teste.com"), "email" },
            { new StringContent("11999999999"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Rua"), "logradouro" },
            { new StringContent("1"), "numero" },
            { new StringContent("Bairro"), "bairro" },
            { new StringContent("Cidade"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Barbearia"), "businessType" },
            { new StringContent("{\"startTime\":\"09:00\",\"endTime\":\"18:00\"}"), "operatingHours" },
            { new ByteArrayContent(new byte[] { 1 }), "logo", "logo.png" }
        };
        await _client.PostAsync("/api/auth/register/company", form);

        var response = await _client.PostAsJsonAsync("/api/auth/resend-verification", "resend@teste.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _mockEmail.Verify(m => m.SendConfirmationEmail(
            "resend@teste.com",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }
}