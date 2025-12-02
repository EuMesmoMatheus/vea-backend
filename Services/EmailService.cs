using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using VEA.API.Models;

namespace VEA.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;

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
                var host = _smtpSettings.Host ?? "smtp.gmail.com";
                var port = _smtpSettings.Port > 0 ? _smtpSettings.Port : 587;
                var username = _smtpSettings.Username;
                var password = _smtpSettings.Password;
                var from = _smtpSettings.From ?? username ?? "no-reply@vea.com";

                _logger.LogInformation("[EMAIL] Tentando enviar email para {To}. Host={Host}, Port={Port}, From={From}", to, host, port, from);

                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("[EMAIL] Password do SMTP está vazio! Email não será enviado.");
                    return;
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("VEA - Veja, Explore e Agende", from));
                message.To.Add(new MailboxAddress("", to));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = body };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new MailKit.Net.Smtp.SmtpClient();
                
                // Tenta conectar com StartTLS (porta 587) ou SSL (porta 465)
                var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                
                _logger.LogInformation("[EMAIL] Conectando ao SMTP {Host}:{Port} com {Security}", host, port, secureOption);
                
                await client.ConnectAsync(host, port, secureOption);
                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("[EMAIL] E-mail enviado com sucesso para {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL] Erro ao enviar email para {To}. Detalhes: {Message}", to, ex.Message);
            }
        }
    }
}
