using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class GetAvailableSlotsTests : AppointmentsControllerTests
    {
        public GetAvailableSlotsTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact(DisplayName = "Deve retornar slots disponíveis quando cliente autenticado")]
        public async Task Deve_Retornar_Slots_Disponiveis_Quando_Cliente_Autenticado()
        {
            // Arrange
            using var scope = _factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = TestData.CreateCompany(601, "Barbearia Slots", true);
            company.OperatingHours = "08:00-18:00";
            var employee = TestData.CreateEmployee(601, 601, "João Slots");
            employee.EmailVerified = true;
            var service = TestData.CreateService(601, 601, "Corte de Cabelo");
            service.Duration = 60;

            db.Companies.Add(company);
            db.Employees.Add(employee);
            db.Services.Add(service);
            await db.SaveChangesAsync();

            var httpClient = CreateClientAs(userId: 777, role: "Client", companyId: 601);

            // Act
            var response = await httpClient.GetAsync(
                "/api/appointments/available-slots?companyId=601&employeeId=601&dateStr=2025-12-20&serviceIds=601");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Count.Should().BeGreaterThan(0);
            result.Data.Should().Contain("08:00");
        }

        [Fact(DisplayName = "Deve excluir horário já agendado dos slots disponíveis")]
        public async Task Deve_Excluir_Horario_Ja_Agendado()
        {
            // Arrange
            using var scope = _factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = TestData.CreateCompany(602, "Barbearia Ocupada", true);
            company.OperatingHours = "08:00-18:00";
            var employee = TestData.CreateEmployee(602, 602, "João Ocupado");
            employee.EmailVerified = true;
            var service = TestData.CreateService(602, 602, "Corte");
            service.Duration = 60;
            var testClient = TestData.CreateClient(602, "José Cliente", null);

            db.Companies.Add(company);
            db.Employees.Add(employee);
            db.Services.Add(service);
            db.Clients.Add(testClient);

            // Cria um agendamento já existente às 10h
            db.Appointments.Add(new Appointment
            {
                CompanyId = 602,
                EmployeeId = 602,
                ClientId = 602,
                StartDateTime = new DateTime(2025, 12, 20, 10, 0, 0),
                EndDateTime = new DateTime(2025, 12, 20, 11, 0, 0),
                TotalDurationMinutes = 60,
                Status = "Scheduled",
                ServicesJson = "[{\"ServiceId\":602}]"
            });

            await db.SaveChangesAsync();

            var httpClient = CreateClientAs(userId: 888, role: "Client", companyId: 602);

            // Act
            var response = await httpClient.GetAsync(
                "/api/appointments/available-slots?companyId=602&employeeId=602&dateStr=2025-12-20&serviceIds=602");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<string>>>();
            result.Should().NotBeNull();
            result!.Data.Should().NotBeNull();
            result.Data!.Should().NotContain("10:00"); // Este horário está ocupado
            result.Data.Should().Contain("09:00");
            result.Data.Should().Contain("11:00");
        }

        [Fact(DisplayName = "Deve retornar BadRequest se parâmetros faltando")]
        public async Task Deve_Retornar_BadRequest_Quando_Parametros_Faltando()
        {
            var httpClient = CreateClientAs(123, "Client", 1);
            var response = await httpClient.GetAsync("/api/appointments/available-slots");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
