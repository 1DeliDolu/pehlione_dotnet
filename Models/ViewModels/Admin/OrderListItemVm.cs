namespace Pehlione.Models.ViewModels.Admin;

public sealed class OrderListItemVm
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CustomerEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ShippingCarrier { get; set; }
    public string? TrackingCode { get; set; }
    public IReadOnlyList<string> NextStatusOptions { get; set; } = Array.Empty<string>();
    public bool CanRestock { get; set; }
    public bool IsRestocked { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "";
}
