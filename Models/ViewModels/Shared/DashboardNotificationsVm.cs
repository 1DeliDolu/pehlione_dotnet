namespace Pehlione.Models.ViewModels.Shared;

public sealed class DashboardNotificationsVm
{
    public bool IsAdmin { get; set; }
    public int UnreadCount { get; set; }
    public string ReturnUrl { get; set; } = "";
    public IReadOnlyList<string> CreateEventDepartmentOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DashboardNotificationItemVm> Items { get; set; } = Array.Empty<DashboardNotificationItemVm>();
}

public sealed class DashboardNotificationItemVm
{
    public long Id { get; set; }
    public string Department { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
