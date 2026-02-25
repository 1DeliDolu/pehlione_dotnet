namespace Pehlione.Models.ViewModels.Customer;

public sealed class CartVm
{
    public IReadOnlyList<CartLineVm> Lines { get; set; } = Array.Empty<CartLineVm>();
    public decimal Total { get; set; }
}

public sealed class CartLineVm
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? Color { get; set; }
    public string? Size { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}
