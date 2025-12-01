using Microsoft.Extensions.Options;           // ← novo (obrigatório)
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using VEA.API.Models;                        // ← ajuste se o seu SmtpSettings estiver em outra pasta

namespace VEA.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;   // ← mudou
        private readonly ILogger<EmailService> _logger;

        // ← mudou só essa linha (o resto do construtor é igual)
        public EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger)
        {
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        public async Task SendConfirmationEmail(string to, string subject, string body)
        {
            await SendEmailAsync(to, subject, body);
        }

        public async Task SendInviteEmail(string to, string subject, string body)
        {
            await SendEmailAsync(to, subject, body);
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            // ← agora pega tudo do _smtpSettings (a senha vem do Railway automaticamente)
            var host = _smtpSettings.Host;
            var port = _smtpSettings.Port;
            var username = _smtpSettings.Username;
            var password = _smtpSettings.Password;
            var from = _smtpSettings.From ?? "no-reply@vea.com";

            using var smtpClient = new SmtpClient(host, port);
            smtpClient.EnableSsl = true;
            smtpClient.Credentials = new NetworkCredential(username, password);

            using var mailMessage = new MailMessage(from, to, subject, body) { IsBodyHtml = true };

            await smtpClient.SendMailAsync(mailMessage); // ← exatamente igual ao seu código original
            _logger.LogInformation("E-mail enviado para {To}", to);
        }
    }
}