using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            try
            {
                var host = _smtpSettings.Host;
                var port = _smtpSettings.Port;
                var username = _smtpSettings.Username;
                var password = _smtpSettings.Password;
                var from = _smtpSettings.From ?? "no-reply@vea.com";

                _logger.LogInformation("[EMAIL] Tentando enviar email para {To}. Host={Host}, Port={Port}, From={From}", to, host, port, from);

                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("[EMAIL] Password do SMTP está vazio! Email não será enviado.");
                    return;
                }

                using var smtpClient = new SmtpClient(host, port);
                smtpClient.EnableSsl = true;
                smtpClient.Credentials = new NetworkCredential(username, password);

                using var mailMessage = new MailMessage(from, to, subject, body) { IsBodyHtml = true };

                await smtpClient.SendMailAsync(mailMessage);
                _logger.LogInformation("[EMAIL] E-mail enviado com sucesso para {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL] Erro ao enviar email para {To}. Detalhes: {Message}", to, ex.Message);
            }
        }
    }
}