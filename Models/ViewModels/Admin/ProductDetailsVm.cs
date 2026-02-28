namespace Pehlione.Models.ViewModels.Admin;

public sealed class ProductDetailsVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public IReadOnlyList<string> ImageUrls { get; set; } = Array.Empty<string>();
}
