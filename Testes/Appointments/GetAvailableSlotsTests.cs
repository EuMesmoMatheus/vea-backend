using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using VEA.API.Models.Dtos;
using VEA.API.Testes;           // ESSENCIAL para achar CustomWebApplicationFactory
using Xunit;

namespace VEA.API.Testes.Appointments
{
    public class GetAvailableSlotsTests : AppointmentsControllerTests
    {
        // MUDANÇA AQUI: recebe CustomWebApplicationFactory, não o genérico antigo
        public GetAvailableSlotsTests(CustomWebApplicationFactory factory)
            : base(factory) { }

        [Fact(DisplayName = "Deve retornar slots disponíveis quando cliente autenticado")]
        public async Task Deve_Retornar_Slots_Disponiveis_Quando_Cliente_Autenticado()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = new Company { Id = 1, IsActive = true, OperatingHours = "08:00-18:00" };
            var employee = new Employee { Id = 1, CompanyId = 1, IsActive = true, EmailVerified = true };
            var service = new Service { Id = 1, CompanyId = 1, Duration = 60, Active = true, Name = "Corte de Cabelo" };

            db.Companies.Add(company);
            db.Employees.Add(employee);
            db.Services.Add(service);
            await db.SaveChangesAsync();

            var client = CreateClientAs(userId: 777, role: "Client", companyId: 1);

            // Act
            var response = await client.GetAsync(
                "/api/appointments/available-slots?companyId=1&employeeId=1&date=2025-12-20&serviceIds=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var slots = await response.Content.ReadFromJsonAsync<List<DateTime>>();
            slots.Should().NotBeNull();
            slots.Should().HaveCount(10);
            slots![0].Should().Be(new DateTime(2025, 12, 20, 8, 0, 0));
            slots![9].Should().Be(new DateTime(2025, 12, 20, 17, 0, 0));
        }

        [Fact(DisplayName = "Deve excluir horário já agendado dos slots disponíveis")]
        public async Task Deve_Excluir_Horario_Ja_Agendado()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var company = new Company { Id = 1, IsActive = true, OperatingHours = "08:00-18:00" };
            var employee = new Employee { Id = 1, CompanyId = 1, IsActive = true, EmailVerified = true };
            var service = new Service { Id = 1, CompanyId = 1, Duration = 60, Active = true };
            var client = new Client { Id = 999, Name = "José", IsActive = true };

            db.Companies.Add(company);
            db.Employees.Add(employee);
            db.Services.Add(service);
            db.Clients.Add(client);

            // Cria um agendamento já existente às 10h
            db.Appointments.Add(new Appointment
            {
                CompanyId = 1,
                EmployeeId = 1,
                ClientId = 999,
                StartDateTime = new DateTime(2025, 12, 20, 10, 0, 0),
                EndDateTime = new DateTime(2025, 12, 20, 11, 0, 0),
                Status = "Scheduled",
                ServicesJson = "[{\"ServiceId\":1}]"
            });

            await db.SaveChangesAsync();

            var httpClient = CreateClientAs(userId: 888, role: "Client", companyId: 1);

            // Act
            var response = await httpClient.GetAsync(
                "/api/appointments/available-slots?companyId=1&employeeId=1&date=2025-12-20&serviceIds=1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var slots = await response.Content.ReadFromJsonAsync<List<DateTime>>();
            slots.Should().NotBeNull();
            slots!.Should().NotContain(new DateTime(2025, 12, 20, 10, 0, 0));
            slots.Should().Contain(new DateTime(2025, 12, 20, 9, 0, 0));
            slots.Should().Contain(new DateTime(2025, 12, 20, 11, 0, 0));
        }

        [Fact(DisplayName = "Deve retornar BadRequest se parâmetros faltando")]
        public async Task Deve_Retornar_BadRequest_Quando_Parametros_Faltando()
        {
            var client = CreateClientAs(123, "Client", 1);
            var response = await client.GetAsync("/api/appointments/available-slots");
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}