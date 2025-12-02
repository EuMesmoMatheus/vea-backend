using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VEA.API.Testes;

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
        // 1ª CORREÇÃO: Verifica se o scheme é "Test" (senão ele roda em todo request)
        if (!Context.Request.Headers.Authorization.ToString().Contains("Test"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var testClaimsHeader = Context.Request.Headers["X-Test-Claims"].FirstOrDefault();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, "Client"),
            new Claim("companyId", "1"),
            new Claim("ClientId", "1") // ESSA LINHA É OBRIGATÓRIA pro seu [Authorize(Policy = "ClientOnly")]
        };

        if (!string.IsNullOrEmpty(testClaimsHeader))
        {
            var parts = testClaimsHeader.Split(',');
            var userId = parts.Length > 0 ? parts[0].Trim() : "1";
            var role = parts.Length > 1 ? parts[1].Trim() : "Client";
            var companyId = parts.Length > 2 ? parts[2].Trim() : "1";

            claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
                new Claim("companyId", companyId),
                new Claim("ClientId", userId) // ESSENCIAL para o seu controller saber quem é o cliente
            };
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}