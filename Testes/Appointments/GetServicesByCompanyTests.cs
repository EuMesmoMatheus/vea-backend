using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class GetServicesByCompanyTests : AppointmentsControllerTests
    {
        public GetServicesByCompanyTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact(DisplayName = "GetServices deve retornar apenas serviços ativos da empresa")]
        public async Task Deve_Retornar_Apenas_Servicos_Ativos_Da_Empresa()
        {
            // Arrange
            using var scope = _factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = TestData.CreateCompany(603, "Barbearia Serviços", true);
            db.Companies.Add(company);

            db.Services.AddRange(
                new Service { Id = 6031, Name = "Corte de Cabelo", Price = 60m, Duration = 40, CompanyId = 603, Active = true },
                new Service { Id = 6032, Name = "Barba Completa", Price = 40m, Duration = 25, CompanyId = 603, Active = true },
                new Service { Id = 6033, Name = "Sobrancelha", Price = 20m, Duration = 15, CompanyId = 603, Active = false } // inativo
            );

            await db.SaveChangesAsync();

            var httpClient = CreateClientAs(userId: 999, role: "Client", companyId: 603);

            // Act
            var response = await httpClient.GetAsync("/api/appointments/services?companyId=603");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ServiceDto>>>();

            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(2); // só os ativos
            var nomes = result.Data!.Select(s => s.Name).OrderBy(n => n).ToList();
            nomes.Should().ContainInOrder("Barba Completa", "Corte de Cabelo");
        }

        [Fact(DisplayName = "GetServices deve retornar vazio se companyId inválido")]
        public async Task Deve_Retornar_Vazio_Quando_CompanyId_Invalido()
        {
            var httpClient = CreateClientAs(111, "Client", 1);

            var response = await httpClient.GetAsync("/api/appointments/services?companyId=99999");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ServiceDto>>>();

            result!.Data.Should().BeEmpty();
        }
    }
}
