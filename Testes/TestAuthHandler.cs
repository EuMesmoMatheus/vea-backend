using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VEA.API.Testes;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = Context.Request.Headers["X-Test-Claims"].FirstOrDefault()?.Split(',')
                     ?? new[] { "1" };

        var claimsList = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, claims[0]),
            new Claim(ClaimTypes.Role, claims.Length > 1 ? claims[1] : "Client"),
            new Claim("companyId", claims.Length > 2 ? claims[2] : "1")
        };

        var identity = new ClaimsIdentity(claimsList, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}