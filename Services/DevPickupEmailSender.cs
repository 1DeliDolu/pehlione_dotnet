using System.Net.Mail;
using System.Net;

namespace Pehlione.Services;

public sealed class DevPickupEmailSender : IAppEmailSender
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly ILogger<DevPickupEmailSender> _logger;

    public DevPickupEmailSender(
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<DevPickupEmailSender> logger)
    {
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var from = _config["Mail:From"] ?? "no-reply@pehlione.local";
        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        var smtpHost = _config["Mail:Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            var smtpPort = int.TryParse(_config["Mail:Smtp:Port"], out var parsedPort) ? parsedPort : 1025;
            var enableSsl = bool.TryParse(_config["Mail:Smtp:EnableSsl"], out var parsedSsl) && parsedSsl;
            var userName = _config["Mail:Smtp:Username"];
            var password = _config["Mail:Smtp:Password"];

            using var smtpClient = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(userName))
            {
                smtpClient.Credentials = new NetworkCredential(userName, password ?? "");
            }

            await smtpClient.SendMailAsync(message, ct);
            _logger.LogInformation("DEV email sent via SMTP: {Host}:{Port} -> {ToEmail}", smtpHost, smtpPort, toEmail);
            return;
        }

        var pickup = _config["Mail:PickupDirectory"] ?? "App_Data/MailPickup";
        var pickupPath = Path.IsPathRooted(pickup)
            ? pickup
            : Path.Combine(_env.ContentRootPath, pickup);

        Directory.CreateDirectory(pickupPath);

        using var pickupClient = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupPath
        };

        await pickupClient.SendMailAsync(message, ct);
        _logger.LogInformation("DEV email queued to pickup directory: {PickupPath} -> {ToEmail}", pickupPath, toEmail);
    }
}
