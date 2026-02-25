namespace Pehlione.Models.Catalog;

public sealed class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    // URL icin (or: "erkek-ayakkabi")
    public string Slug { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
