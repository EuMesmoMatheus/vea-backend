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
using System.Text;
using System.Threading.Tasks;
using VEA.API;
using VEA.API.Data;
using VEA.API.Services;
using Xunit;

namespace VEA.API.Testes.Auth;

public class ConfirmAccountTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<VEA.API.Program> _factory;

    public ConfirmAccountTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Confirm_" + Guid.NewGuid()));

                var mockEmail = new Mock<IEmailService>();
                services.RemoveAll<IEmailService>();
                services.AddScoped(_ => mockEmail.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact(DisplayName = "Confirmar conta deve ativar empresa")]
    public async Task Deve_Ativar_Empresa_Com_Token_Valido()
    {
        // Cadastra empresa
        var form = new MultipartFormDataContent
        {
            { new StringContent("Empresa Teste"), "name" },
            { new StringContent("confirm@teste.com"), "email" },
            { new StringContent("11999999999"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Rua X"), "logradouro" },
            { new StringContent("1"), "numero" },
            { new StringContent("Centro"), "bairro" },
            { new StringContent("São Paulo"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Barbearia"), "businessType" },
            { new StringContent("{\"startTime\":\"09:00\",\"endTime\":\"18:00\"}"), "operatingHours" },
            { new ByteArrayContent(new byte[] { 1 }), "logo", "logo.png" }
        };

        await _client.PostAsync("/api/auth/register/company", form);

        // Pega o ID da empresa
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var empresa = await db.Companies.FirstAsync(c => c.Email == "confirm@teste.com");

        // Gera token válido (mesmo formato que o controller usa)
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"confirm@teste.com {DateTime.Now.Ticks}"));

        // Confirma
        var response = await _client.GetAsync($"/api/auth/confirm/company/{empresa.Id}?token={token}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var empresaAtivada = await db.Companies.FirstAsync(c => c.Id == empresa.Id);
        empresaAtivada.IsActive.Should().BeTrue();
    }
}