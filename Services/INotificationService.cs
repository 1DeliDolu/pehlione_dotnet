namespace Pehlione.Services;

public interface INotificationService
{
    Task CreateAsync(
        string department,
        string title,
        string message,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken ct = default);
}
