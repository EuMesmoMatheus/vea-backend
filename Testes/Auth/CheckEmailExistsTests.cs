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

public class CheckEmailExistsTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    private readonly HttpClient _client;

    public CheckEmailExistsTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_CheckEmail_" + Guid.NewGuid()));

                var mockEmail = new Mock<IEmailService>();
                services.RemoveAll<IEmailService>();
                services.AddScoped(_ => mockEmail.Object);
            });
        });

        _client = factory.CreateClient();
    }

    [Fact(DisplayName = "CheckEmail deve retornar false para e-mail inexistente")]
    public async Task Deve_Retornar_False_Quando_Email_Nao_Existe()
    {
        var response = await _client.GetAsync("/api/auth/check-email/naoexiste@teste.com");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<dynamic>();
        bool existe = json?.Data;
        existe.Should().BeFalse();
    }

    [Fact(DisplayName = "CheckEmail deve retornar true quando e-mail já está cadastrado")]
    public async Task Deve_Retornar_True_Quando_Email_Existe()
    {
        // Cadastra um cliente
        await _client.PostAsJsonAsync("/api/auth/register/client", new
        {
            name = "Existe",
            email = "existe@teste.com",
            password = "123456"
        });

        var response = await _client.GetAsync("/api/auth/check-email/existe@teste.com");
        var json = await response.Content.ReadFromJsonAsync<dynamic>();
        bool existe = json?.Data;
        existe.Should().BeTrue();
    }
}