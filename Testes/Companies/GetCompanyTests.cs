using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace VEA.API.Testes.Companies;

public class GetCompanyTests : CompaniesControllerTests
{
    public GetCompanyTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "GetCompany deve retornar empresa com URL completa")]
    public async Task Deve_Retornar_Empresa_Com_Logo_Completo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Companies.Add(new Company
        {
            Id = 99,
            Name = "Salão Bela",
            Logo = "/uploads/logos/bela.png",
            CoverImage = "/uploads/covers/bela.jpg"
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/companies/99");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<CompanyDto>>();
        result!.Data!.Logo.Should().Contain("http");
        result.Data.CoverImage.Should().Contain("http");
    }
}