using System.Net.Mail;

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
        var pickup = _config["Mail:PickupDirectory"] ?? "App_Data/MailPickup";

        var pickupPath = Path.IsPathRooted(pickup)
            ? pickup
            : Path.Combine(_env.ContentRootPath, pickup);

        Directory.CreateDirectory(pickupPath);

        using var message = new MailMessage(from, toEmail)
        {
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };

        using var client = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = pickupPath
        };

        await client.SendMailAsync(message);

        _logger.LogInformation("DEV email queued to pickup directory: {PickupPath} -> {ToEmail}", pickupPath, toEmail);
    }
}
