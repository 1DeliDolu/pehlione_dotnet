namespace Pehlione.Models.ViewModels.Customer;

public sealed class CatalogProductListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
}
