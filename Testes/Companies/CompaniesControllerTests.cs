using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VEA.API;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Companies;

public class CompaniesControllerTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    protected readonly WebApplicationFactory<VEA.API.Program> _factory;
    protected readonly HttpClient _client;

    public CompaniesControllerTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(m => m.WebRootPath).Returns(Path.GetTempPath());

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Companies_" + Guid.NewGuid()));

                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                services.AddAuthorization();

                // Substitui o IWebHostEnvironment pelo mock
                services.RemoveAll<IWebHostEnvironment>();
                services.AddSingleton(mockEnv.Object);
            });
        });

        _client = _factory.CreateClient();
    }

    protected HttpClient CreateAdminClient(int companyId = 1)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Test");
        client.DefaultRequestHeaders.Add("X-Test-Claims", $"1,Admin,{companyId}");
        return client;
    }
}