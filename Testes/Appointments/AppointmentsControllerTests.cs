// Testes/Appointments/AppointmentsControllerTests.cs
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using VEA.API;
using VEA.API.Data;
using VEA.API.Testes; // ← necessário para achar TestWebApplicationFactory
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class AppointmentsControllerTests : IClassFixture<TestWebApplicationFactory>
    {
        protected readonly TestWebApplicationFactory _factory;
        protected readonly HttpClient _client;

        public AppointmentsControllerTests(TestWebApplicationFactory factory)
        {
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove o DbContext real (SQL Server, etc.)
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Troca por InMemory com nome único por teste
                    services.AddDbContext<ApplicationDbContext>(options =>
                        options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

                    // Seu handler de autenticação de teste continua funcionando perfeitamente
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                });
            });

            _client = _factory.CreateClient();
        }

        protected HttpClient CreateClientAs(int userId = 999, string role = "Client", int companyId = 1)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
            client.DefaultRequestHeaders.Add("X-Test-Claims", $"{userId},{role},{companyId}");
            return client;
        }

        protected HttpClient CreateAdminClient(int adminId = 1, int companyId = 1)
            => CreateClientAs(adminId, "Admin", companyId);
    }
}