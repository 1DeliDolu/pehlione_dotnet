namespace Pehlione.Models.ViewModels.Customer;

public sealed class CatalogCategoryDetailsVm
{
    public CatalogCategoryVm Category { get; set; } = new();
    public IReadOnlyList<CatalogCategoryListItemVm> ChildCategories { get; set; } = Array.Empty<CatalogCategoryListItemVm>();
    public IReadOnlyList<CatalogProductListItemVm> Products { get; set; } = Array.Empty<CatalogProductListItemVm>();
}

public sealed class CatalogCategoryVm
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
}
