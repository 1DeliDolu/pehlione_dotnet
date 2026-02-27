using Pehlione.Models.Catalog;

namespace Pehlione.Models.Inventory;

public enum StockMovementType
{
    In = 1,
    Out = 2
}

public sealed class StockMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
