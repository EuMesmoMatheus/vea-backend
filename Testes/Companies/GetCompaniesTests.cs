using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Companies;

public class GetCompaniesTests : CompaniesControllerTests
{
    public GetCompaniesTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "GetCompanies deve retornar todas as empresas com URLs completas")]
    public async Task Deve_Retornar_Todas_Empresas_Com_Logo_Completo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.Add(new Company
        {
            Id = 1,
            Name = "Barbearia do Zé",
            Email = "ze@teste.com",
            Logo = "/uploads/logos/123.png",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/companies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CompanyDto>>>();
        var company = result!.Data![0];
        company.Name.Should().Be("Barbearia do Zé");
        company.Logo.Should().StartWith("http");
    }

    [Fact(DisplayName = "GetCompanies deve filtrar por localização")]
    public async Task Deve_Filtrar_Por_Localizacao()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.AddRange(
            new Company { Id = 1, Name = "Centro", Address = new Address { Cidade = "São Paulo" } },
            new Company { Id = 2, Name = "Zona Sul", Address = new Address { Bairro = "Moema" } }
        );
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/companies?location=Moema");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CompanyDto>>>();
        result!.Data.Should().HaveCount(1);
        result.Data[0].Name.Should().Be("Zona Sul");
    }
}