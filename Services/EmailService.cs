using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VEA.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;
        private readonly string _apiKey;
        private readonly string _fromEmail;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _httpClient = new HttpClient();
            _logger = logger;
            _apiKey = config["Resend:ApiKey"] ?? "";
            _fromEmail = config["Resend:From"] ?? "VEA <onboarding@resend.dev>";
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
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogWarning("[EMAIL] API Key do Resend está vazia! Email não será enviado.");
                    return;
                }

                _logger.LogInformation("[EMAIL] Enviando email para {To} via Resend", to);

                var payload = new
                {
                    from = _fromEmail,
                    to = new[] { to },
                    subject = subject,
                    html = body
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[EMAIL] Email enviado com sucesso para {To}. Response: {Response}", to, responseBody);
                }
                else
                {
                    _logger.LogError("[EMAIL] Erro ao enviar email para {To}. Status: {Status}, Response: {Response}", 
                        to, response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMAIL] Erro ao enviar email para {To}. Detalhes: {Message}", to, ex.Message);
            }
        }
    }
}
