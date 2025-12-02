using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;
using Xunit;

namespace VEA.API.Testes.Appointments;

public class GetServicesByCompanyTests : AppointmentsControllerTests
{
    public GetServicesByCompanyTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "GetServices deve retornar serviços ativos da empresa")]
    public async Task Deve_Retornar_Servicos_Ativos()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.Add(new Company { Id = 1, Name = "Teste", IsActive = true, OperatingHours = "08:00-18:00" });
        db.Services.AddRange(
            new Service { Id = 1, Name = "Corte", Price = 50, Duration = 30, CompanyId = 1, Active = true },
            new Service { Id = 2, Name = "Barba", Price = 30, Duration = 20, CompanyId = 1, Active = false }
        );
        await db.SaveChangesAsync();

        // Act
        var response = await _client.GetAsync("/api/appointments/services?companyId=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ServiceDto>>>();
        result!.Data.Should().HaveCount(1);
        result.Data[0].Name.Should().Be("Corte");
    }
}