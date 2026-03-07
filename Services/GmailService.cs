using System.Net;
using System.Net.Mail;

namespace FormBuilder.API.Services
{
    public class GmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GmailService> _logger;

        public GmailService(IConfiguration configuration, ILogger<GmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendAlertEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpServer = _configuration["GmailSettings:SmtpServer"];
                var smtpPort = int.Parse(_configuration["GmailSettings:SmtpPort"] ?? "587");
                var enableSslRaw = _configuration["GmailSettings:EnableSsl"];
                var enableSsl = true;
                if (!string.IsNullOrWhiteSpace(enableSslRaw) && bool.TryParse(enableSslRaw, out var parsedEnableSsl))
                {
                    enableSsl = parsedEnableSsl;
                }
                var senderEmail = _configuration["GmailSettings:SenderEmail"];
                var senderPassword = _configuration["GmailSettings:SenderPassword"];
                var senderName = _configuration["GmailSettings:SenderName"];

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    _logger.LogError("Gmail settings not configured properly");
                    return false;
                }

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(senderEmail, senderName);
                    message.To.Add(toEmail);
                    message.Subject = subject;
                    message.Body = body;
                    message.IsBodyHtml = true;

                    using (var client = new SmtpClient(smtpServer, smtpPort))
                    {
                        client.UseDefaultCredentials = false;
                        client.EnableSsl = enableSsl;
                        client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                        client.DeliveryMethod = SmtpDeliveryMethod.Network;
                        await client.SendMailAsync(message);
                    }
                }

                _logger.LogInformation($"Email sent successfully to {toEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {toEmail}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendTestEmailAsync(string toEmail)
        {
            var subject = "🧪 Prueba de Alertas - Frigolab";
            var body = @"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2 style='color: #3b82f6;'>✅ Email de Prueba</h2>
                    <p>Este es un email de prueba del sistema de alertas de Frigolab.</p>
                    <p>Si recibes este mensaje, la configuración de Gmail está funcionando correctamente.</p>
                    <hr>
                    <p style='color: #6b7280; font-size: 12px;'>
                        Enviado automáticamente por el Sistema de Gestión de Frigolab
                    </p>
                </body>
                </html>
            ";

            return await SendAlertEmailAsync(toEmail, subject, body);
        }
    }
}
