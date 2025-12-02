using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Appointments;

public class GetAvailableSlotsTests : AppointmentsControllerTests
{
    public GetAvailableSlotsTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "GetAvailableSlots deve retornar horários livres")]
    public async Task Deve_Retornar_Horarios_Livres()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Companies.Add(new Company { Id = 1, Name = "Barbearia", IsActive = true, OperatingHours = "09:00-18:00" });
        db.Employees.Add(new Employee { Id = 1, CompanyId = 1, Name = "João", IsActive = true, EmailVerified = true });
        db.Services.Add(new Service { Id = 1, Name = "Corte", Duration = 60, CompanyId = 1, Active = true });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/appointments/available-slots?companyId=1&employeeId=1&dateStr=2025-12-10&serviceIds=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
        result!.Data.Should().Contain("09:00");
        result.Data.Should().Contain("10:00");
        result.Data.Should().NotContain("18:00");
    }
}