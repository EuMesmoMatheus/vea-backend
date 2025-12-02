using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VEA.API.Testes;

/// <summary>
/// Handler de autenticação para testes de integração.
/// Permite simular usuários autenticados sem necessidade de JWT real.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Verifica se há header de autorização com "Test"
        var authHeader = Context.Request.Headers.Authorization.ToString();
        
        if (string.IsNullOrEmpty(authHeader) || !authHeader.Contains("Test"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Lê claims customizadas do header X-Test-Claims
        var testClaimsHeader = Context.Request.Headers["X-Test-Claims"].FirstOrDefault();

        // Claims padrão
        var userId = "1";
        var role = "Client";
        var companyId = "1";

        if (!string.IsNullOrEmpty(testClaimsHeader))
        {
            var parts = testClaimsHeader.Split(',');
            userId = parts.Length > 0 ? parts[0].Trim() : "1";
            role = parts.Length > 1 ? parts[1].Trim() : "Client";
            companyId = parts.Length > 2 ? parts[2].Trim() : "1";
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role),
            new Claim("companyId", companyId),
            new Claim("CompanyId", companyId), // Duplicado para compatibilidade
            new Claim("ClientId", userId),      // Para policies que verificam ClientId
            new Claim("sub", userId),           // Padrão JWT
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
