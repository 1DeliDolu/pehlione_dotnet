namespace Pehlione.Models.Commerce;

public sealed class OrderStatusLog
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? ChangedByUserId { get; set; }
    public string? ChangedByDepartment { get; set; }
}
