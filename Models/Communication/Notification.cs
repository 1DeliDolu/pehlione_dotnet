namespace Pehlione.Models.Communication;

public sealed class Notification
{
    public long Id { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
