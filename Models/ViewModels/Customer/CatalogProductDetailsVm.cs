namespace Pehlione.Models.ViewModels.Customer;

public sealed class CatalogProductDetailsVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();

    public string CategoryName { get; set; } = "";
    public string CategorySlug { get; set; } = "";
}
