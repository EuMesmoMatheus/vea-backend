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

namespace VEA.API.Testes.Companies;

public class GetCompaniesTests : CompaniesControllerTests
{
    public GetCompaniesTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact(DisplayName = "GetCompanies deve retornar todas as empresas com URLs completas")]
    public async Task Deve_Retornar_Todas_Empresas_Com_Logo_Completo()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var company = TestData.CreateCompany(701, "Barbearia do Zé Get", true);
        company.Logo = "/uploads/logos/123.png";
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/companies");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CompanyDto>>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        
        var comp = result.Data!.FirstOrDefault(c => c.Id == 701);
        comp.Should().NotBeNull();
        comp!.Name.Should().Be("Barbearia do Zé Get");
        comp.Logo.Should().StartWith("http");
    }

    [Fact(DisplayName = "GetCompanies deve filtrar por localização")]
    public async Task Deve_Filtrar_Por_Localizacao()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Empresa 1 com endereço no Centro
        var company1 = TestData.CreateCompany(702, "Empresa Centro", true);
        var address1 = TestData.CreateAddress(702, 702, "São Paulo", "Centro");
        db.Addresses.Add(address1);
        company1.AddressId = 702;
        db.Companies.Add(company1);
        
        // Empresa 2 com endereço em Pinheiros
        var company2 = TestData.CreateCompany(703, "Empresa Pinheiros", true);
        var address2 = TestData.CreateAddress(703, 703, "São Paulo", "Pinheiros");
        db.Addresses.Add(address2);
        company2.AddressId = 703;
        db.Companies.Add(company2);
        
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/companies?location=Pinheiros");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CompanyDto>>>();
        
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        
        // Deve encontrar apenas a empresa de Pinheiros
        var empresasPinheiros = result.Data!.Where(c => c.Id == 703).ToList();
        empresasPinheiros.Should().HaveCount(1);
        empresasPinheiros[0].Name.Should().Be("Empresa Pinheiros");
    }
}
