namespace Pehlione.Models.ViewModels.Admin;

public sealed class CategoryDeleteVm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool HasProducts { get; set; }
}
