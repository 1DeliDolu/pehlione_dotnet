namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryListItemVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool IsActive { get; set; }
}
