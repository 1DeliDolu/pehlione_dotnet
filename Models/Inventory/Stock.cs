using Pehlione.Models.Catalog;

namespace Pehlione.Models.Inventory;

public sealed class Stock
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
}
