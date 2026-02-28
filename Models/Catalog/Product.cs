namespace Pehlione.Models.Catalog;

public sealed class Product
{
    public int Id { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Name { get; set; } = "";
    public string Sku { get; set; } = "";

    public decimal Price { get; set; }

    public List<string> ImageUrls { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
