using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VEA.API;
using VEA.API.Data;
using VEA.API.Services;
using Xunit;

namespace VEA.API.Testes.Auth;

public class RegisterCompanyTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    private readonly WebApplicationFactory<VEA.API.Program> _factory;
    private readonly HttpClient _client;

    public RegisterCompanyTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove DbContext real
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Company_" + Guid.NewGuid()));

                // Mock do email
                var mockEmail = new Mock<IEmailService>();
                mockEmail.Setup(x => x.SendConfirmationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                         .Returns(Task.CompletedTask);

                services.RemoveAll<IEmailService>();
                services.AddScoped(_ => mockEmail.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact(DisplayName = "Deve retornar BadRequest quando a logo estiver faltando")]
    public async Task Deve_Retornar_BadRequest_Sem_Logo()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Barbearia do Zé"), "name" },
            { new StringContent("ze@teste.com"), "email" },
            { new StringContent("11999999999"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Praça da Sé"), "logradouro" },
            { new StringContent("100"), "numero" },
            { new StringContent("Centro"), "bairro" },
            { new StringContent("São Paulo"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Barbearia"), "businessType" },
            { new StringContent("{\"startTime\":\"09:00\",\"endTime\":\"18:00\"}"), "operatingHours" }
        };

        var response = await _client.PostAsync("/api/auth/register/company", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<dynamic>();
        json?.Message.ToString().Should().Contain("Logo é obrigatório");
    }

    [Fact(DisplayName = "Deve cadastrar empresa com sucesso quando todos os dados estiverem corretos")]
    public async Task Deve_Cadastrar_Empresa_Com_Sucesso()
    {
        var logoFile = new ByteArrayContent(Encoding.UTF8.GetBytes("fake png image"));
        logoFile.Headers.ContentType = new("image/png");

        var form = new MultipartFormDataContent
        {
            { new StringContent("Salão da Maria"), "name" },
            { new StringContent("maria@teste.com"), "email" },
            { new StringContent("11988887777"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Praça da Sé"), "logradouro" },
            { new StringContent("50"), "numero" },
            { new StringContent("Centro"), "bairro" },
            { new StringContent("São Paulo"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Salão de Beleza"), "businessType" },
            { new StringContent("{\"startTime\":\"08:00\",\"endTime\":\"20:00\"}"), "operatingHours" },
            { logoFile, "logo", "logo.png" }
        };

        var response = await _client.PostAsync("/api/auth/register/company", form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var empresa = await db.Companies.FirstOrDefaultAsync(c => c.Email == "maria@teste.com");

        empresa.Should().NotBeNull();
        empresa!.Name.Should().Be("Salão da Maria");
        empresa.IsActive.Should().BeFalse();
        empresa.Logo.Should().NotBeNullOrEmpty();
    }
}