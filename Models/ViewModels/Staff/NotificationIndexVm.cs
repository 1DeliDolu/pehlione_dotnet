namespace Pehlione.Models.ViewModels.Staff;

public sealed class NotificationIndexVm
{
    public bool IncludeRead { get; set; }
    public string Query { get; set; } = "";
    public string Department { get; set; } = "";
    public IReadOnlyList<string> DepartmentOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<NotificationListItemVm> Items { get; set; } = Array.Empty<NotificationListItemVm>();
}

public sealed class NotificationListItemVm
{
    public long Id { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
