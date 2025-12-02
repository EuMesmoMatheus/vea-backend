using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;
using VEA.API.Testes;           // necessário para achar a CustomWebApplicationFactory
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class CreateAppointmentTests : AppointmentsControllerTests
    {
        // MUDANÇA AQUI: recebe CustomWebApplicationFactory, não WebApplicationFactory<Program>
        public CreateAppointmentTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact(DisplayName = "Cliente autenticado deve criar agendamento com sucesso")]
        public async Task Deve_Criar_Agendamento_Com_Sucesso_Quando_Cliente_Logado()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            db.Companies.Add(new Company { Id = 1, IsActive = true, OperatingHours = "08:00-18:00" });
            db.Employees.Add(new Employee { Id = 1, CompanyId = 1, IsActive = true, EmailVerified = true });
            db.Services.Add(new Service { Id = 1, CompanyId = 1, Duration = 60, Active = true });
            db.Clients.Add(new Client
            {
                Id = 777,
                Name = "Ana Carolina",
                Email = "ana@cliente.com",
                Phone = "(47) 98888-7777",
                IsActive = true
            });

            await db.SaveChangesAsync();

            // Cliente autenticado como ID 777
            var client = CreateClientAs(userId: 777, role: "Client", companyId: 1);

            var dto = new CreateAppointmentDto
            {
                CompanyId = 1,
                EmployeeId = 1,
                StartDateTime = new DateTime(2025, 12, 24, 10, 0, 0),
                ServiceIds = new[] { 1 }
            };

            // Act
            var response = await client.PostAsJsonAsync("/api/appointments", dto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Appointment>>();
            result!.Success.Should().BeTrue();
            result.Data!.ClientId.Should().Be(777);
            result.Data.Status.Should().Be("Scheduled");
        }
    }
}