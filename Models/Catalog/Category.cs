namespace Pehlione.Models.Catalog;

public sealed class Category
{
    public int Id { get; set; }

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }

    public string? Code { get; set; }
    public string Name { get; set; } = "";

    // URL icin (or: "erkek-ayakkabi")
    public string Slug { get; set; } = "";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
