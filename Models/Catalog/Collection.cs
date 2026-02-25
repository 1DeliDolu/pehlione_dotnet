namespace Pehlione.Models.Catalog;

public enum CollectionKind
{
    Manual = 1,
    Rule = 2
}

public sealed class Collection
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";

    public CollectionKind Kind { get; set; } = CollectionKind.Rule;
    public string? RuleJson { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CollectionProduct> CollectionProducts { get; set; } = new List<CollectionProduct>();
}
