namespace Pehlione.Services;

public sealed class NullEmailSender : IAppEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
        => Task.CompletedTask;
}
