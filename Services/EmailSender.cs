using SendGrid;
using SendGrid.Helpers.Mail;

namespace RSS_Reader.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string plainBody, string htmlBody);
}

public class SendGridEmailSender : IEmailSender
{
    private readonly ISendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailSender(string apiKey, string fromEmail, string fromName)
    {
        _client = new SendGridClient(apiKey);
        _fromEmail = fromEmail;
        _fromName = fromName;
    }

    public async Task SendAsync(string to, string subject, string plainBody, string htmlBody)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress(_fromEmail, _fromName),
            Subject = subject,
            PlainTextContent = plainBody,
            HtmlContent = htmlBody
        };
        msg.AddTo(new EmailAddress(to));

        var response = await _client.SendEmailAsync(msg);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            throw new Exception($"SendGrid error {(int)response.StatusCode}: {errorBody}");
        }
    }
}
