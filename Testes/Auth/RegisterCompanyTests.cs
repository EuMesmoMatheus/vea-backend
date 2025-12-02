using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VEA.API.Data;
using VEA.API.Services;
using Xunit;

namespace VEA.API.Testes.Auth;

public class RegisterCompanyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RegisterCompanyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Deve retornar BadRequest quando a logo estiver faltando")]
    public async Task Deve_Retornar_BadRequest_Sem_Logo()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Barbearia do Zé"), "name" },
            { new StringContent("ze503@teste.com"), "email" },
            { new StringContent("11999999999"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Praça da Sé"), "logradouro" },
            { new StringContent("100"), "numero" },
            { new StringContent("Centro"), "bairro" },
            { new StringContent("São Paulo"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Barbearia"), "businessType" },
            { new StringContent("{\"startTime\":\"09:00\",\"endTime\":\"18:00\"}"), "operatingHours" }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/register/company", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Logo");
    }

    [Fact(DisplayName = "Deve cadastrar empresa com sucesso quando todos os dados estiverem corretos")]
    public async Task Deve_Cadastrar_Empresa_Com_Sucesso()
    {
        // Reset mock do email
        _factory.MockEmailService.Reset();
        _factory.MockEmailService.Setup(x => x.SendConfirmationEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                                .Returns(Task.CompletedTask);

        var logoFile = new ByteArrayContent(Encoding.UTF8.GetBytes("fake png image"));
        logoFile.Headers.ContentType = new("image/png");

        var uniqueEmail = $"maria{System.Guid.NewGuid():N}@teste.com";

        var form = new MultipartFormDataContent
        {
            { new StringContent("Salão da Maria Teste"), "name" },
            { new StringContent(uniqueEmail), "email" },
            { new StringContent("11988887777"), "phone" },
            { new StringContent("senha123"), "password" },
            { new StringContent("01001-000"), "cep" },
            { new StringContent("Praça da Sé"), "logradouro" },
            { new StringContent("50"), "numero" },
            { new StringContent("Centro"), "bairro" },
            { new StringContent("São Paulo"), "cidade" },
            { new StringContent("SP"), "uf" },
            { new StringContent("Salão de Beleza"), "businessType" },
            { new StringContent("{\"startTime\":\"08:00\",\"endTime\":\"20:00\"}"), "operatingHours" },
            { logoFile, "logo", "logo.png" }
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/auth/register/company", form);
        
        // Debug: se falhar, mostra o conteúdo da resposta
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new System.Exception($"Registro falhou: {response.StatusCode} - {errorContent}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var empresa = await db.Companies.FirstOrDefaultAsync(c => c.Email == uniqueEmail);

        empresa.Should().NotBeNull();
        empresa!.Name.Should().Be("Salão da Maria Teste");
        empresa.IsActive.Should().BeFalse(); // Empresa começa inativa
        empresa.Logo.Should().NotBeNullOrEmpty();
    }
}
