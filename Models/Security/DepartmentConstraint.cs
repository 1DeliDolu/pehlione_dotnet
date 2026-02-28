namespace Pehlione.Models.Security;

public sealed class DepartmentConstraint
{
    public long Id { get; set; }
    public string Department { get; set; } = "";
    public bool CanIncreaseStock { get; set; }
    public bool CanDeleteStock { get; set; }
    public int? MaxReceiveQuantity { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
