namespace Pehlione.Models.Catalog;

public sealed class CmsPage
{
    public int Id { get; set; }

    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Content { get; set; }

    public bool IsActive { get; set; } = true;
}
