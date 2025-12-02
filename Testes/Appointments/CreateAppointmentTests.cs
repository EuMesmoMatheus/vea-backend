using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;
using Xunit;

namespace VEA.API.Testes.Appointments;

public class CreateAppointmentTests : AppointmentsControllerTests
{
    public CreateAppointmentTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "Deve criar agendamento com sucesso")]
    public async Task Deve_Criar_Agendamento_Com_Sucesso()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.Add(new Company { Id = 1, IsActive = true, OperatingHours = "08:00-18:00" });
        db.Employees.Add(new Employee { Id = 1, CompanyId = 1, IsActive = true, EmailVerified = true });
        db.Services.Add(new Service { Id = 1, Duration = 60, CompanyId = 1, Active = true });
        await db.SaveChangesAsync();

        var dto = new CreateAppointmentDto
        {
            CompanyId = 1,
            EmployeeId = 1,
            ClientId = 100,
            StartDateTime = new DateTime(2025, 12, 10, 10, 0, 0),
            ServiceIds = new[] { 1 }
        };

        var response = await _client.PostAsJsonAsync("/api/appointments", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<Appointment>>();
        result!.Success.Should().BeTrue();
        result.Data.Status.Should().Be("Scheduled");
    }
}