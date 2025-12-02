using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

namespace VEA.API.Testes.Appointments;

public class AppointmentsControllerTests : IClassFixture<WebApplicationFactory<VEA.API.Program>>
{
    private readonly WebApplicationFactory<VEA.API.Program> _factory;
    private readonly HttpClient _client;

    public AppointmentsControllerTests(WebApplicationFactory<VEA.API.Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove DbContext real
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb_Appointments_" + Guid.NewGuid()));

                // Autenticação fake para endpoints protegidos
                services.AddAuthentication("Test")
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>(
                        "Test", _ => { });

                services.AddAuthorization();
            });
        });

        _client = _factory.CreateClient();
    }

    // Helper: autentica como Admin (companyId = 1)
    private HttpClient CreateAdminClient(int companyId = 1)
    {
        var client = _factory.CreateClient();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("companyId", companyId.ToString()),
            new Claim("role", "Admin")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        client.DefaultRequestHeaders.Authorization = new("Test");
        // Simula o User no controller
        var context = new DefaultHttpContext { User = principal };
        client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton(context))).CreateClient();
        return client;
    }

    // Helper: autentica como Client
    private HttpClient CreateClientUser(int clientId = 999)
    {
        var client = _factory.CreateClient();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, clientId.ToString()),
            new Claim(ClaimTypes.Role, "Client")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var context = new DefaultHttpContext { User = principal };
        client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton(context))).CreateClient();
        return client;
    }
}