using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class CreateAppointmentTests : AppointmentsControllerTests
    {
        public CreateAppointmentTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact(DisplayName = "Cliente autenticado deve criar agendamento com sucesso")]
        public async Task Deve_Criar_Agendamento_Com_Sucesso_Quando_Cliente_Logado()
        {
            // Arrange
            using var scope = _factory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = TestData.CreateCompany(604, "Barbearia Create", true);
            company.OperatingHours = "08:00-18:00";
            var employee = TestData.CreateEmployee(604, 604, "João Create");
            employee.EmailVerified = true;
            var service = TestData.CreateService(604, 604, "Corte de Cabelo");
            service.Duration = 60;
            var client = TestData.CreateClient(604, "Ana Carolina", null);

            db.Companies.Add(company);
            db.Employees.Add(employee);
            db.Services.Add(service);
            db.Clients.Add(client);
            await db.SaveChangesAsync();

            // Cliente autenticado como ID 604
            var httpClient = CreateClientAs(userId: 604, role: "Client", companyId: 604);

            var dto = new CreateAppointmentDto
            {
                CompanyId = 604,
                EmployeeId = 604,
                ClientId = 604,
                StartDateTime = new DateTime(2025, 12, 24, 10, 0, 0),
                ServiceIds = new[] { 604 }
            };

            // Act
            var response = await httpClient.PostAsJsonAsync("/api/appointments", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Appointment>>();
            result!.Success.Should().BeTrue();
            result.Data!.ClientId.Should().Be(604);
            result.Data.Status.Should().Be("Scheduled");
        }
    }
}
