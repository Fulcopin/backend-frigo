namespace FormBuilder.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendAlertEmailAsync(string toEmail, string subject, string body);
        Task<bool> SendTestEmailAsync(string toEmail);
    }
}
