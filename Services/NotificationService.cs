using Pehlione.Data;
using Pehlione.Models.Communication;

namespace Pehlione.Services;

public sealed class NotificationService : INotificationService
{
    private readonly PehlioneDbContext _db;

    public NotificationService(PehlioneDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(
        string department,
        string title,
        string message,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(department) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            return;

        _db.Notifications.Add(new Notification
        {
            Department = department.Trim(),
            Title = title.Trim(),
            Message = message.Trim(),
            RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim(),
            RelatedEntityId = string.IsNullOrWhiteSpace(relatedEntityId) ? null : relatedEntityId.Trim()
        });

        await _db.SaveChangesAsync(ct);
    }
}
