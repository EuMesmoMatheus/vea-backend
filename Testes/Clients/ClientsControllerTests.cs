using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using VEA.API;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Clients;

public class ClientsControllerTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    protected readonly WebApplicationFactory<VEA.API.Program> _factory;
    protected readonly HttpClient _client;

    public ClientsControllerTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove DbContext real
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Clients_" + Guid.NewGuid()));

                // Autenticação fake
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                services.AddAuthorization();
            });
        });

        _client = _factory.CreateClient();
    }

    protected HttpClient CreateClientWithClaims(string userId = "999", string role = "Client", string companyId = "1")
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Test");
        client.DefaultRequestHeaders.Add("X-Test-Claims", $"{userId},{role},{companyId}");
        return client;
    }
}