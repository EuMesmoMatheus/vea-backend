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

namespace VEA.API.Testes.Clients;

public class GetClientsTests : ClientsControllerTests
{
    public GetClientsTests(WebApplicationFactory<VEA.API.Program> factory) : base(factory) { }

    [Fact(DisplayName = "Admin deve listar clientes da própria empresa")]
    public async Task Admin_Deve_Listar_Clientes_Da_Empresa()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Clients.AddRange(
            new Client { Id = 1, Name = "João", Email = "joao@teste.com", CompanyId = 1 },
            new Client { Id = 2, Name = "Maria", Email = "maria@teste.com", CompanyId = 1 },
            new Client { Id = 3, Name = "Pedro", Email = "pedro@teste.com", CompanyId = 2 }
        );
        await db.SaveChangesAsync();

        var client = CreateClientWithClaims("10", "Admin", "1");
        var response = await client.GetAsync("/api/clients?companyId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<Client>>>();
        result!.Data.Should().HaveCount(2);
        result.Data.Should().Contain(c => c.Name == "João");
    }

    [Fact(DisplayName = "Não Admin não deve acessar lista de clientes")]
    public async Task Nao_Admin_Nao_Deve_Acessar_Lista()
    {
        var client = CreateClientWithClaims("999", "Client", "1");
        var response = await client.GetAsync("/api/clients?companyId=1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}