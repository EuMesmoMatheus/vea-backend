using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Models;
using Xunit;

namespace VEA.API.Testes.Companies;

public class GetCompanyTests : CompaniesControllerTests
{
    public GetCompanyTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact(DisplayName = "GetCompany deve retornar empresa com URL completa")]
    public async Task Deve_Retornar_Empresa_Com_Logo_Completo()
    {
        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        var company = TestData.CreateCompany(704, "Salão Bela Teste", true);
        company.Logo = "/uploads/logos/bela.png";
        company.CoverImage = "/uploads/covers/bela.jpg";
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        // Este endpoint requer autenticação (Admin ou Client)
        var httpClient = CreateClientWithClaims(userId: 1, role: "Client", companyId: 704);
        var response = await httpClient.GetAsync("/api/companies/704");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CompanyDto>>();
        result.Should().NotBeNull();
        result!.Data.Should().NotBeNull();
        result.Data!.Logo.Should().Contain("http");
        result.Data.CoverImage.Should().Contain("http");
    }
}
